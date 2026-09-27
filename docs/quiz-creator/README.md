# Quiz Creator (`/create`)

A browser-based editor for quiz templates. It runs entirely in the browser (Blazor WebAssembly in
`topg.Web.Client`) and never writes to the game database. It produces a ZIP that holds a SQL script plus the images,
named exactly as they have to be uploaded to the CDN. The UI/UX specification (screens `S-*`, components `C-*`,
principles `UX-*`) is in [ui-concept.md](ui-concept.md). The end-user workflow and reverse-proxy protection are in the
repository `README.md`.

## Design decisions

| Topic | Decision |
|---|---|
| Hosting | Sub-routes of `/create` inside `topg.Web`. `App.razor` renders `CreatorRoutes` as InteractiveWebAssembly with prerendering disabled (IndexedDB is only available in the browser). The rest of the app stays InteractiveServer. |
| Storage | Browser only (IndexedDB), autosaved. Project files move a project to another browser. There is no server-side storage and no DB write access. |
| Access protection | None in the app. `/create` is protected at the reverse proxy (`Path(/create) \|\| PathPrefix(/create/)`, because `PathPrefix(/create)` would also match `/creator.css` and `/creator-*.js`). |
| Image naming | Content hash: `{baseUrl}/{folder}/{sha256-prefix}.{ext}`. |
| Base URL | Stored per project. New projects take it from a browser-wide default. |
| SQL | Insert, or "insert or replace" (first delete templates with the same name). |
| Question types | Text and Image. The model is polymorphic so Sound questions and hints can be added later. |
| Ordering | The editor always shows the game's order (categories alphabetical, questions by points). Only boards can be reordered by hand. |

## Game rules the creator respects

The game reads templates through `topg.Web/Templating`, and these rules come from how it renders them:

- **Categories are not an entity.** The game groups a board's questions by the exact `Category` string, so a
  category name has to be unique per board. The creator models categories as `CategoryDraft` and writes the name onto
  every question on export.
- **Category order is alphabetical, and questions are ordered by points.** Duplicate points in one category give an
  undefined order. `GameOrder` reproduces the game's sorting for the editor and must stay consistent with it.
- **Boards are ordered by `Board.Order`.** `QuizExecution` sorts on it, and the creator writes `Order` as 0..n-1
  from the board's position in the project.
- **At most 5 categories fit on a board.** Each column is 20% wide.
- **Image URLs must be absolute.** `QuestionImageUri` is parsed with `new Uri(...)`, so the base URL has to be an
  absolute http(s) URL.
- **An empty answer image is written as `''`, not `NULL`.** EF maps `AnswerImageUri` as a non-nullable string.
- **Deleting a board doesn't cascade to its questions.** The `Questions → Boards` FK is `NO ACTION`, so replace mode
  deletes questions explicitly before it deletes templates.

## Screens and functions

| Route | Screen | Functions |
|---|---|---|
| `/create` | Projects (S-1) | List projects. New project dialog (S-9). Open, rename, duplicate, delete (also removes images used only by that project). Import project file (S-8). Export project file. |
| `/create/settings` | Settings (S-2) | Default base URL, default points list (standard `100, 200, 300, 400, 500`), theme (dark / light / system), storage usage and "request persistent storage", clean up unused images. |
| `/create/{id}` | – | Redirects to the first board. |
| `/create/{id}/board/{n}` | Board editor (S-3) | Three panes: project outline, board tabs with the board grid (C-9), inspector. |
| `/create/{id}/quiz` | Quiz settings (S-4) | Name, base URL (with "use default"), CDN folder (slug) with a live example URL, replace-mode switch, backup section (last project-file export). |
| `/create/{id}/export` | Export (S-7) | Stepper: Check → Review → Download. |

The app bar (C-1) shows the project name, section tabs, the save status (C-2) and a validation summary (C-4/C-5).
Clicking an issue selects the affected item.

### Board editor

- **Boards:** add, delete, duplicate, move left/right. A new project starts with one board.
- **Categories:** add, rename, delete, move to another board. The category inspector (S-3c) shows the category's details.
- **Questions:** add a question in a category cell (it gets the next value from the default points list),
  duplicate, move to another category, delete, and change the type (asks for confirmation when data would be lost).
- **Text question inspector (S-3a):** question text, correct answer, answer type (buzzer / text), points.
- **Image question inspector (S-3b):** question text, question image (required), display size S/M/L, "Start obscured"
  switch, answer text, answer image (optional). Grid cells show a thumbnail of the question image.
- **Selection** is shared by the grid, outline, route and inspector, and lives in `ProjectSession`.
- **Undo:** destructive edits take a snapshot first and show an Undo snackbar.
- **Shortcuts:** `Ctrl+S` save, `Ctrl+E` export, `Ctrl+Z` undo (outside text fields), `Alt+Arrow` move the selection,
  `Esc` clear the selection. Shortcuts are ignored while a dialog is open.

### Images

- PNG, JPG/JPEG, WEBP and GIF, up to 10 MB. The browser's content type wins, and the file extension is the fallback.
- File name = first 16 hex characters of the SHA-256 of the content + the normalized extension (`jpeg` → `jpg`).
  The same image uploaded twice is stored once and keeps its URL.
- Images are stored in IndexedDB and shared between projects. The drop zone (C-3) shows a preview and the final URL.

### Validation

`ProjectValidator` runs after every change. Errors block export. Warnings have to be acknowledged in the export step.

| Severity | Rule |
|---|---|
| Error | Empty quiz name. Invalid folder (lowercase letters, digits, dashes). Base URL not an absolute http(s) URL. No boards. A board without categories. Empty or duplicate category name on a board. Empty question text. A text question without a correct answer. An image question without a question image. Points ≤ 0. |
| Warning | More than 5 categories on a board. A category without questions. Duplicate points in a category. |

### Export package (`{folder}-export.zip`)

```
{folder}-export.zip
├─ import.sql
└─ {folder}/
   └─ {hash}.{ext}   (only images the project references)
```

`import.sql` (`SqlExport`) is one atomic `DO $tag$ … $tag$;` block that captures generated IDs with
`INSERT … RETURNING "Id" INTO`:

- In replace mode it first deletes the questions of every template with the same name, then the templates
  (boards cascade).
- Text rows fill only the text columns, and image rows fill only the image columns (TPH).
- String literals escape `'` as `''`. The dollar-quote tag is chosen so that no project text contains it
  (`$topg$`, `$topg1$`, …). Line breaks in names are flattened inside SQL comments, NUL characters are removed, and
  the script always uses LF.
- A header comment records the project name, generation time, question count and replace mode.

Uploading `{folder}/` to the CDN root under the base URL makes every URL in the script resolve. Export is
deterministic: ZIP entries are sorted, images are stored uncompressed, and entry times are the generation time.

### Project file (`*.topgquiz`)

A ZIP containing `project.json` (with `schemaVersion`) and `images/{hash}.{ext}`. It's the backup and transfer
format and isn't read by the game.

- On import the dialog checks the structure and version and recomputes every image hash (a mismatch means a corrupt
  file).
- If a project with the same ID already exists, it offers "Import as copy" (the default) or "Overwrite" (with a
  confirmation). Overwrite keeps `UpdatedAt`, so two browsers produce byte-identical exports.
- Older schema versions are upgraded step by step in `ProjectFile`, and newer versions are rejected.

## Code map (`topg.Web.Client/Creator`)

| Folder | Contents |
|---|---|
| `Model/` | `QuizProject`, `BoardDraft`, `CategoryDraft`, `QuestionDraft` (JSON `type` discriminator), `ImageRef`, `Slug`, `GameOrder`, and the edit operations in `ProjectEditing`. |
| `Storage/` | `ICreatorStorage` / `IndexedDbCreatorStorage` (+ `wwwroot/creator-storage.js`; stores `projects`, `images`, `settings`), `ProjectStore`, `ProjectSession` (open project, selection; edits go through `Change` / `ChangeWithUndo`), `ProjectAutosave` (1 s debounce). |
| `Images/` | `ImageIntake`, `ImageNaming`, `ImagePreviewCache`. |
| `Validation/` | `ProjectValidator`, `ValidationIssue`, `CreatorValidation` (field validation functions, used because MudBlazor's own validation resets `Error` while typing). |
| `Export/` | `SqlExport`, `ExportPackage`, `ProjectFile`, `FileDownloader` (+ `wwwroot/creator-files.js`). |
| `Pages/`, `Components/`, `Layout/` | Screens and components from [ui-concept.md](ui-concept.md). The three editor panes are a CSS grid, not `MudDrawer`s. |

Client static assets are served from the site root (`/creator.css`, `/creator-*.js`), because the client uses
`StaticWebAssetProjectMode=Default`.

## Tests

- `topg.Web.Client.Tests`: validator rules, slugs, hash naming, JSON polymorphism and project-file round trip,
  editing operations, and SQL golden files in `Golden/` (regenerate with `UPDATE_GOLDEN=1`).
- `topg.Web.IntegrationTests` (Testcontainers PostgreSQL, skipped without Docker): applies the EF migrations, runs
  the generated SQL, loads it with `TemplateService` and builds a `QuizExecution`. It covers replace twice (one
  template), insert-only twice (two), other templates staying untouched, unicode/quote/backslash/newline round trips
  and texts containing the dollar-quote tag.

## Not supported yet

- Sound questions (`SoundQuestion` has no fields yet).
- Question hints.
- Loading existing templates from the live database into the creator.
