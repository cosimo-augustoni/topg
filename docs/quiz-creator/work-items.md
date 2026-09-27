# Quiz Creator (`/create`) – Analysis & Work Items

Replaces the MAUI desktop tool `src/Tools/QuizMaker` with a browser-based quiz creator inside `topg.Web`.
The creator never writes to the live database: it produces a ZIP containing a SQL script plus the images,
named exactly as they must be uploaded to the CDN.

**UI/UX specification:** [ui-concept.md](ui-concept.md). Every UI work item points to the relevant sections
(`UX-*` principles/patterns, `S-*` screens, `C-*` reusable components). Implement the UI as specified there.

## Decisions

| Topic | Decision |
|---|---|
| Location | Sub-routes of `/create` inside `topg.Web` |
| Render mode | `/create` runs as **Interactive WebAssembly** (new `topg.Web.Client` project). The rest of the app stays Interactive Server. Images, hashing, ZIP and SQL generation all stay in the browser. |
| Draft storage | **Browser only** (IndexedDB), autosaved. Users can export a **project file** and import it again, e.g. in another browser. No server-side storage and no DB write access. |
| Access protection | None in the app. `/create` is protected at the **reverse proxy** (documented only). |
| Image naming | **Content hash**: `{baseUrl}/{quizFolder}/{sha256-prefix}.{ext}` |
| Base URL | Stored **per project** and pre-filled from a **browser-wide default** |
| SQL | **Insert or replace**: optionally delete existing templates with the same name, then insert |
| Question types (v1) | **Text + Image**. Model and editor must be extensible for Sound and the planned *hints*. |
| Old tool | Delete `src/Tools/QuizMaker`. No data migration. |
| UI | MudBlazor, **workbench style** (dense, desktop-first, three panes: outline / board grid / inspector). The editor always shows categories alphabetically and questions by points, like the game, so there's no manual reordering except boards. See [ui-concept.md](ui-concept.md). |

---

## Analysis of the current data structure

### Live database (source of truth: `topg.Web/Templating`, `QuizContextModelSnapshot.cs`)

```
Templates (Id bigint identity PK, Name text NOT NULL)            -- Name is NOT unique
   1 ─── n
Boards    (Id bigint identity PK, TemplateId bigint NOT NULL FK → Templates ON DELETE CASCADE,
           Order int NOT NULL)
   1 ─── n
Questions (TPH, one table; discriminator = QuestionType int)
   Id bigint identity PK
   BoardId bigint NULL FK → Boards  ON DELETE NO ACTION   (!)
   QuestionType int NOT NULL   0 = Text, 1 = Sound, 2 = Image
   AnswerType   int NOT NULL   0 = Buzzer, 1 = Text
   Points       int NOT NULL
   Category     text NOT NULL  -- a plain string, there is no category table
   -- TextQuestion (0)
   "TextQuestion_QuestionText" text
   "CorrectAnswer"             text  (DEFAULT '')
   -- ImageQuestion (2)
   "QuestionText"     text
   "QuestionImageUri" text   -- parsed with new Uri(...) → must be ABSOLUTE
   "AnswerText"       text
   "AnswerImageUri"   text   -- optional since 49d13e8: empty/NULL = no answer image
   "ImageSize"        int    0 = Small, 1 = Medium, 2 = Large
   -- SoundQuestion (1): no fields yet
```

### Implicit rules the creator must respect (from the rendering code)

- **Categories are not a separate entity.** A board's columns are built with `GroupBy(q => q.Category)`
  (`Components/Pages/Board.razor`), so the category name is repeated on each question and must match exactly.
- **Category order is alphabetical** (`OrderBy(g => g.Category)`). Nothing stores the order you create them in.
  The editor grid has to show this order, and the editor should warn about it.
- **Maximum of 5 categories per board.** Columns are hard-coded to `width: 20%`.
- **Questions in a column are ordered by `Points`.** `Question.Order` is a computed property that falls back
  to `Points` and has no column, so it is never persisted. Duplicate points in the same category give an
  undefined order.
- **Board order is not applied at runtime.** `QuizExecution` uses `template.Boards` in whatever order the DB returns them,
  and `Board.Order` is never sorted on. It only works now because boards are inserted in order. See WI-19.
- `ImageQuestion.QuestionImageUri` is turned into a `Uri`, so a relative URL or empty base URL crashes hosting.
  The base URL must be an absolute `http(s)` URL.
- **Deletes don't cascade from Boards to Questions**: the FK is `NO ACTION`. A "replace" must delete Questions
  explicitly before deleting Templates.
- In EF, `AnswerImageUri` is mapped as a non-nullable `string` and the runtime checks `IsNullOrEmpty`.
  So write `''`, not `NULL`, when there is no answer image.

### The current QuizMaker (to be replaced)

- A local SQLite model with an extra `Category` entity (Template → Board → Category → Question) that is
  flattened into `Questions.Category` on export. This is useful in the editor and we keep the idea.
- Media are named `{counter}.{ext}` in `%AppData%/QuizMaker/Media/{SafeFolderName}`. The URL is `baseUrl/SafeFolderName/file`.
- The SQL export is a `DO $$ … $$` block that uses `RETURNING … INTO` variables for the generated IDs. It is **insert-only**,
  writes `NULL` for a missing answer image, and has no validation.
- The base URL is a global setting in a `Metadata` table.

---

## Target architecture (summary)

```
topg.Web (server)                         topg.Web.Client (WASM, new)
├─ App.razor: per-route render mode       ├─ Creator/Pages/*          (@page "/create…")
│   /create* → InteractiveWebAssembly     ├─ Creator/Layout/CreatorLayout
│   else     → InteractiveServer          ├─ Creator/Model/*          (editable draft model)
├─ Router AdditionalAssemblies = Client   ├─ Creator/Storage/*        (IndexedDB via JS interop)
└─ uses shared enums from Client/Shared   ├─ Creator/Export/*         (SQL, ZIP, project file)
                                          ├─ Shared/Enums             (QuestionType, AnswerType, ImageSize)
                                          └─ wwwroot/creator-storage.js
```

**Project file format (`*.topgquiz`)**: a ZIP containing `project.json` (with a `schemaVersion`) and `images/{hash}.{ext}`.
**Export package (`{quiz-folder}-export.zip`)**: `import.sql` plus `{quizFolder}/{hash}.{ext}`, laid out like the CDN.

---

## Work items

Estimates: S ≈ < ½ day, M ≈ 1 day, L ≈ 2–3 days.

### Epic A – Foundation

#### WI-01 · Add `topg.Web.Client` WebAssembly project and per-route render mode — **L**
- Create a `topg.Web.Client` Blazor WebAssembly project (net10.0, MudBlazor) and add it to `topg.slnx`.
- `topg.Web` references the client project and uses `AddInteractiveWebAssemblyComponents()` /
  `AddInteractiveWebAssemblyRenderMode()` with `AddAdditionalAssemblies(typeof(Client._Imports).Assembly)`.
- `App.razor` currently forces `InteractiveServer` on `<Routes>` and `<HeadOutlet>`. Change it so paths under `/create`
  get `InteractiveWebAssembly` and everything else keeps `InteractiveServer`, for example by picking the mode from
  `HttpContext.Request.Path`.
- The `Router` needs `AdditionalAssemblies` so it finds pages in the client assembly.
- Register MudBlazor services in the client's `Program.cs`.
- Make sure the Aspire run and the Docker/publish output still work, including that `_framework` WASM assets are served.

**Acceptance:** a placeholder `/create` page renders in WASM (no SignalR circuit for it). `/`, `/host` and `/play/*`
behave as before. The container image builds.

#### WI-02 · Move shared enums into the client assembly — **S**
- Move `QuestionType`, `AnswerType` and `ImageSize` (numeric values unchanged) to a shared namespace in `topg.Web.Client`.
  Server code (`Templating`, `Quiz.Execution`, EF model) references them, so the creator and the DB can't get out of sync.
- Check that no migration is generated (`dotnet ef migrations add Check` → empty).

**Acceptance:** one definition of each enum. Build is green, and the EF model snapshot is unchanged.

#### WI-03 · Creator layout and routing skeleton — **S**
- `CreatorLayout` in the client project, as in **UX-5** (shell) and **UX-3** (theme, light/dark toggle).
- `C-1 CreatorAppBar` with the parts that don't need a project yet (title, theme toggle, settings link).
  The project parts are completed in WI-10.
- All routes from **UX-2**: `/create`, `/create/settings`, `/create/{id}`, `/create/{id}/board/{n}`, `/create/{id}/quiz`,
  `/create/{id}/export`.

**Acceptance:** you can navigate between the empty pages, they use the creator layout, and the theme toggle works.
Depends on: WI-01

### Epic B – Model & browser storage

#### WI-04 · Creator draft model and validation — **M**
Mutable model in the client project, separate from the EF records, which have required navigations like
`Board.Template`:
```
QuizProject { Id (Guid), SchemaVersion, Name, Folder (CDN folder slug), BaseUrl,
              ReplaceExisting (bool), CreatedAt, UpdatedAt, Boards[] }
BoardDraft    { Id, Order, Categories[] }
CategoryDraft { Id, Name, Questions[] }
QuestionDraft (abstract) { Id, Type, AnswerType, Points }
  TextQuestionDraft  { QuestionText, CorrectAnswer }
  ImageQuestionDraft { QuestionText, QuestionImage (ImageRef), AnswerText, AnswerImage (ImageRef?), ImageSize }
ImageRef { Hash, Extension, ContentType, OriginalFileName }
```
- Polymorphic JSON (System.Text.Json `[JsonDerivedType]` with a `type` discriminator), so Sound and hints can be
  added later without breaking old files.
- `Folder` defaults to a slug of `Name` (lowercase, `a-z0-9-`). It can be edited, and it is **not** renamed
  automatically after the first export, so CDN paths stay stable.
- A validator that returns errors (block export) and warnings:
  - Errors: empty project name, folder or base URL; base URL not an absolute http(s) URI; no boards; board without categories;
    empty or duplicate category name in a board; question without text; text question without an answer;
    image question without a question image; points ≤ 0.
  - Warnings: more than 5 categories; duplicate points in a category; category without questions.
  - Use the codes, severities, targets and texts from the **UX-7** message catalog. Each issue carries a `Target` id
    (project/board/category/question) so `C-4`/`C-5` can attach it in the right place.
- Also store `LastExportedAt` and `LastProjectFileExportAt` on the project (used by S-4 and S-7).

**Acceptance:** unit tests cover the validator rules and JSON round-trip (WI-18).

#### WI-05 · IndexedDB storage (JS module + C# service) — **M**
- `wwwroot/creator-storage.js` (ES module, no external dependency). Database `topg-creator` with stores:
  `projects` (key: id → project JSON), `images` (key: hash → Blob + metadata), `settings` (key/value).
- C# `ICreatorStorage` over `IJSRuntime`: list/get/save/delete projects; put/get/has/delete image; get/set setting.
  Pass image bytes with `DotNetStreamReference` / `IJSStreamReference` or byte arrays.
- Autosave from the editor with debounce (~1 s) and a "Saved / Saving…" indicator.
- Deleting a project removes images that no other project references.
- Request persistent storage (`navigator.storage.persist()`), show the storage estimate on the settings page, and warn
  that clearing browser data deletes drafts and users should export project files.

**Acceptance:** projects and images survive a page reload and a browser restart. Deleting a project cleans up
images only that project used.
Depends on: WI-01, WI-04

#### WI-06 · Image intake and content-hash naming — **M**
- `InputFile` upload (a drag & drop area is optional). Allowed types: png, jpg/jpeg, webp, gif. Configurable size limit
  (e.g. 10 MB) with a clear error.
- Compute the SHA-256 of the bytes in C# (WASM). File name = first 16 hex characters + normalized lowercase extension
  from the content type (`jpeg` → `jpg`). The same image uploaded twice is stored once.
- Store in IndexedDB (WI-05) and attach an `ImageRef` to the question.
- Preview with an object URL made from the blob. Revoke the URL on dispose.
- Show the resulting final URL (`{BaseUrl}/{Folder}/{hash}.{ext}`) next to the preview so you can check it.
- UI: build this as the reusable **`C-3 ImageDropZone`**.

**Acceptance:** uploading the same file twice gives one stored image and the same URL. The URL follows the pattern.
Depends on: WI-05

#### WI-07 · Creator settings (browser-wide defaults) — **S**
- `/create/settings` as specified in **S-2**: default base URL, default points list, theme, storage info, the persist request
  from WI-05, and "Clean up unused images".
- New projects get their `BaseUrl` from this default. Changing the default doesn't change existing projects.

**Acceptance:** after setting a default, a new project gets that base URL. Existing projects keep theirs.
Depends on: WI-03, WI-05

### Epic C – Editor UI

#### WI-08 · Project list page (`/create`) — **M**
- Implement **S-1** (Projects) and the **S-9** New project dialog, including the empty, loading and storage-unavailable states.
- Actions: new, open, rename, duplicate, delete (`C-6`), **import project file** (S-8 / WI-16), **export project file** (WI-16).
- Build the shared components `C-6 ConfirmDialog` and `C-8 EmptyState` here.

**Acceptance:** you can do all CRUD actions on projects without any server call.
Depends on: WI-03, WI-05

#### WI-09 · Quiz settings screen — **S**
- Implement **S-4** at `/create/{id}/quiz`: Name, Base URL (+ "Use default"), Folder (slug validated), a live example URL,
  a warning about URL changes after the first export, the replace-mode switch, and the Backup section.

Depends on: WI-04, WI-08

#### WI-10 · Board & category editor — **L**
- Implement **S-3** (board editor) with its three panes: `ProjectOutline` (left), board tabs + **`C-9 BoardGrid`** (center),
  and the right drawer that hosts the inspectors. Also build **S-3c** (Category inspector).
- Boards: add, delete, duplicate, move left/right (sets `Order`). Categories: add, rename, delete, move to another board.
  **No manual category or question reordering** (principle P2: alphabetical categories, questions by points).
- Add question cells use the next value from the default points list.
- Selection stays in sync between the grid, outline, route and inspector.
- Complete `C-1` (project name, section tabs, validation summary, save status) and build `C-2 SaveStatusChip`,
  `C-4 ValidationBadge`, `C-5 ValidationPanel` and `C-7` icons.
- Deletes use the Undo snackbar from **UX-4** (keep a project snapshot before each destructive change).
- Keyboard shortcuts from **UX-6**.

**Acceptance:** a full 2-board, 5-category, 5-question quiz can be built only with this UI.
Depends on: WI-04, WI-09

#### WI-11 · Text question inspector — **S**
- Implement **S-3a** and the shared inspector rules (type switch with confirmation when data would be lost, debounced
  live binding, required-field error states, Duplicate, Move to…, Delete with Undo).

Depends on: WI-10

#### WI-12 · Image question inspector — **M**
- Implement **S-3b**: question text, question image (`C-3`, required), display size S/M/L with the mini size visual,
  answer text, answer image (`C-3`, optional).
- Grid cells show a thumbnail of the question image (C-9).

Depends on: WI-06, WI-10

#### WI-13 · Board preview — **M** — ❌ out of scope
Dropped: the editor grid already shows the game order, so a separate preview isn't needed (see "Out of scope for v1").
The original plan is kept for reference:
- Implement **S-5**: a full-screen dialog (`Ctrl+P`) that renders each board **the way the game will** (alphabetical categories,
  points order, 5 × 20% columns), plus a question/answer view per tile. This is the only place in the creator that uses game styling.
- Where possible, reuse or extract the markup from `Board.razor` / `SimpleImageQuestion.razor` into components the
  client project can render, or mirror their layout.

**Acceptance:** the preview matches what `/host` shows after the quiz is imported.
Depends on: WI-10

### Epic D – Export / import

#### WI-14 · SQL generator — **M**
Pure C# service (no DB access, fully unit-testable) that turns a validated `QuizProject` into PostgreSQL:
- A single `DO $$ DECLARE … BEGIN … END $$;` block, which runs atomically, and captures IDs with
  `INSERT … RETURNING "Id" INTO` like today.
- **Replace mode** (when enabled), before inserting:
  ```sql
  DELETE FROM "Questions" WHERE "BoardId" IN (
      SELECT b."Id" FROM "Boards" b JOIN "Templates" t ON t."Id" = b."TemplateId" WHERE t."Name" = '<name>');
  DELETE FROM "Templates" WHERE "Name" = '<name>';   -- Boards cascade
  ```
- Column mapping per type:
  - Text → `"TextQuestion_QuestionText"`, `"CorrectAnswer"`
  - Image → `"QuestionText"`, `"QuestionImageUri"`, `"AnswerText"`, `"AnswerImageUri"` (`''` if none), `"ImageSize"`
  - Common → `"QuestionType"`, `"AnswerType"`, `"Points"`, `"Category"` (the category name), `"BoardId"`
- Image URLs = `BaseUrl.TrimEnd('/') + "/" + Folder + "/" + hash + "." + ext`.
- Escape string literals safely (`'` → `''`; `standard_conforming_strings` is on by default in PG).
  Newlines and unicode must round-trip.
- Header comment with the project name, generation time, question count and replace mode.
- Boards are inserted in `Order` order, and `"Order"` is 0..n-1 without gaps.

**Acceptance:** unit tests with snapshot/golden files. An integration test (WI-18) runs the script against Postgres
and loads it with `QuizContext`/`TemplateService`. Running it twice in replace mode leaves exactly one template.
Depends on: WI-04

#### WI-15 · Export package (ZIP) — **M**
- Implement **S-7** (`MudStepper`: Check → Review → Download). Errors block the export, and warnings must be acknowledged
  with the checkbox. Review shows a summary, the SQL preview with Copy, and the image list. Download shows next steps
  generated from the project values.
- Build the ZIP in WASM with `System.IO.Compression`:
  ```
  {folder}-export.zip
  ├─ import.sql
  └─ {folder}/
     ├─ 3f9a1c0b2d4e5f60.png
     └─ …
  ```
  Only images referenced by the project are included.
- Trigger the download through a small JS helper (Blob + `<a download>`).

**Acceptance:** unzipping and uploading `{folder}/` to the CDN root under the base URL makes every URL in `import.sql`
resolve.
Depends on: WI-06, WI-14

#### WI-16 · Project file export / import (`*.topgquiz`) — **M**
- Export: a ZIP containing `project.json` (with `schemaVersion`) and `images/{hash}.{ext}` for every referenced image.
- Import: implement the **S-8** dialog. Validate the structure and version, recompute and check image hashes (showing the
  S-8 error texts), and store the project and images in IndexedDB. If the project ID already exists, offer
  "Import as copy" (default) or "Overwrite" (with a `C-6` confirmation).
- Set `LastProjectFileExportAt` on export (shown in S-4).
- Leave room to migrate `schemaVersion` so later additions (hints, sound) can still read old files.

**Acceptance:** export in browser A, then import in browser B gives an identical project, and the export package
is byte-identical except the timestamp.
Depends on: WI-04, WI-05, WI-08

### Epic E – Quality, cleanup & docs

#### WI-17 · Remove the MAUI QuizMaker — **S**
- Delete `src/Tools/QuizMaker` (including its `.slnx`, migrations and `db-schema.sql`) after WI-15 is merged.
- Check that CI/publish scripts and README don't refer to it.

#### WI-18 · Test project — **M**
- Add a `topg.Web.Client.Tests` (xUnit) project covering: validator rules, slug generation, hash naming, JSON polymorphism
  and project-file round-trip, and SQL golden files.
- Integration test (Testcontainers PostgreSQL): apply the EF migrations, run the generated SQL (insert and replace),
  load the template with `TemplateService`, and build a `QuizExecution` from it without exceptions (this catches
  relative URIs, wrong columns and enum mismatches).

Can be built alongside WI-04 and WI-14.

#### WI-19 · Fix: apply board order at runtime — **S**
- `QuizExecution` (and/or `TemplateService`) should order boards by `Board.Order`, and questions by `Order`/`Points`,
  instead of relying on DB return order.

Independent. Small bugfix found during this analysis.

#### WI-20 · Documentation — **S**
- README: how to use `/create` (create → export → upload `{folder}/` to CDN → run `import.sql`), what the project file is
  for, and that drafts live only in the browser.
- Reverse proxy section: example for protecting `/create` (e.g. Traefik basic-auth middleware on
  `PathPrefix(/create)`), with a note that the WASM bundle under `/_framework` contains no secrets.

---

## Suggested order / milestones

1. **M1 – Skeleton:** WI-01, WI-02, WI-03, WI-19 — ✅ done
   - `/create*` renders `topg.Web.Client.CreatorRoutes` (its own router, since a WASM root component must live in the
     client assembly) with `InteractiveWebAssemblyRenderMode(prerender: false)`. A "Loading creator…" hint shows until WASM has booted.
   - Follow-ups were completed in M2/M3 (theme persisted, project list, full app bar).
2. **M2 – Model & storage:** WI-04, WI-05, WI-06, WI-07, WI-18 (unit part) — ✅ done
   - Code: `topg.Web.Client/Creator/{Model,Validation,Storage,Images,Settings,Components}`, `wwwroot/creator-storage.js`,
     tests in `topg.Web.Client.Tests` (xUnit, in-memory `ICreatorStorage` fake).
   - Deviations from WI-04: board order is the position in `QuizProject.Boards` (no separate `Order` field, it is
     written as `Boards.Order` on export); `QuestionText` lives on `QuestionDraft` (all types have it); unused images are
     not a validator rule but shown/cleaned up in S-2 (images are shared between projects).
   - Client static assets are served from the site root (`/creator-storage.js`, `/creator.css`), not `_content/…`,
     because the client uses `StaticWebAssetProjectMode=Default`.
   - Ready for M3: `ProjectStore` (list/get/save/delete with image cleanup), `ProjectAutosave` (1 s debounce,
     `Status`/`LastSavedAt`/`Error` for C-2), `ImageIntake` + `C-3 ImageDropZone`, default points for
     new question cells (M3 replaced the helper with `CategoryDraft.NextPoints`), `CreatorJson.Clone` for undo snapshots.
   - Persistent storage is requested automatically when the first project is created (M3) and via the S-2 button.
3. **M3 – Editor:** WI-08, WI-09, WI-10, WI-11, WI-12 (UI per [ui-concept.md](ui-concept.md)) — ✅ done
   - Pages: S-1 `Projects`, S-9 `NewProjectDialog`, S-3 `BoardEditor` (+ `ProjectOutline`, `C-9 BoardGrid`,
     `QuestionInspector` S-3a/b, `CategoryInspector` S-3c), S-4 `QuizSettings`. Shared: C-2, C-4, C-5, C-6 (+ text prompt),
     C-7, C-8, `ProjectScope` (loading / not found for project routes), `UndoSnackbar`.
   - `ProjectSession` holds the open project for all project pages and the app bar: every edit goes through
     `Change` / `ChangeWithUndo` (revalidate → autosave → notify); the selection lives there so grid, outline,
     inspector and C-5 stay in sync. The board editor route follows the selected item to its board (after a move,
     undo or a click in C-5); navigating away yourself clears it.
   - Structural edits are plain, unit-tested model operations in `ProjectEditing` / `GameOrder`.
   - Deviations: the three panes are a CSS grid inside the page instead of `MudDrawer`s (see UX-5 note);
     field errors use MudBlazor `Validation` functions (`CreatorValidation`), because MudBlazor's own validation
     resets an `Error` parameter while typing.
4. **M4 – Export:** WI-14, WI-15, WI-16, WI-18 (integration part) — ✅ done
   - `Creator/Export`: `SqlExport` (WI-14), `ExportPackage` (WI-15), `ProjectFile` (WI-16), `FileDownloader` +
     `wwwroot/creator-files.js`. UI: S-7 `Export` page, S-8 `ImportProjectDialog`, "Export project file" in S-1 and S-4.
   - SQL details beyond the spec: the dollar-quote tag is chosen so no text of the project contains it (`$topg$`,
     `$topg1$`, …); line breaks in names are flattened in comments (a newline must not end a comment and turn text into
     SQL); NUL characters are removed (PostgreSQL text can't store them); the script always uses LF. Text rows only fill
     the text columns and image rows only the image columns – all type-specific columns are nullable (TPH).
   - Golden files: `topg.Web.Client.Tests/Golden/*.sql`. After an intended change run the tests with `UPDATE_GOLDEN=1`
     and review the diff.
   - Determinism: ZIP entries are sorted, images are stored uncompressed, entry times are the SQL generation time
     (export) or the project's `UpdatedAt` (project file). Importing with "overwrite" keeps `UpdatedAt`, so exports
     from two browsers are byte-identical (covered by a unit test).
   - `topg.Web.IntegrationTests` (xUnit + Testcontainers PostgreSQL): applies the EF migrations, runs the generated
     script with Npgsql (like psql, no EF placeholder handling), loads it with `TemplateService` and builds a
     `QuizExecution`. Covers replace twice → one template, other templates untouched, insert-only twice → two,
     unicode/newline/quote/backslash round trip and texts containing the dollar-quote tag. The tests are skipped
     (not failed) when Docker isn't available.
5. **M5 – Polish & cleanup:** WI-17, WI-20 (WI-13 was dropped, see below) — ✅ done
   - WI-17: `src/Tools/QuizMaker` is deleted. No solution, script or README referred to it; the old QuizMaker and its
     SQLite migrations remain in the git history.
   - WI-20: the README covers the projects, local run and tests, the `/create` workflow (create → export → upload
     `{folder}/` → run `import.sql`), browser storage, project files and protecting `/create` in the reverse proxy
     (Traefik example). The proxy rule is `Path(/create) || PathPrefix(/create/)`, because `PathPrefix(/create)` would
     also match `/creator.css` and `/creator-*.js`.

## Out of scope for v1 (keep in mind in the design)

- The board preview (WI-13 / S-5): the editor grid already shows the game's order.

- Sound questions (need an audio upload and a data model; `SoundQuestion` is empty today).
- Question hints (a planned feature; the polymorphic JSON and `schemaVersion` should make it additive).
- Loading existing templates from the live DB into the creator.
- Any server-side persistence or authentication.
