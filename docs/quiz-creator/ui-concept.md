# Quiz Creator – UI/UX Concept

UI specification for `/create`, which runs in `topg.Web.Client` (WASM) and uses MudBlazor.
Work items in [work-items.md](work-items.md) refer to the sections here by ID (`UX-*`, `S-*`, `C-*`).

**How to use this document (for implementers and agents)**
- Each screen (`S-*`) has a **route**, a **wireframe**, a **component tree** with the MudBlazor components to use,
  its **states**, an **interaction table** and **acceptance checks**.
- Reusable components (`C-*`) are specified once and referenced by ID. Build them, don't re-implement them inline.
- Wireframes show layout and hierarchy, not exact pixels. Spacing, colors and typography come from the theme (`UX-3`).
- Everything user-visible is in **English**, matching the rest of the app.

---

## UX-1 · Design principles

The gameshow is a **stage**: big type, full-screen, dark, built to be seen from across the room.
The creator is a **workbench**: one person at a desk, spending an hour or more building a quiz. Different goals lead to different choices:

| # | Principle | Consequence |
|---|---|---|
| P1 | **Desktop-first, dense** | Optimized for ≥ 1280 px wide. Dense MudBlazor variants (`Dense`, `Margin.Dense`, `Typo.body2`). Below 960 px it still works, with drawers turning into overlays, but it isn't optimized for phones. |
| P2 | **What you see is what gets played** | The board grid always shows categories **alphabetically** and questions **ordered by points**, exactly like `Board.razor`. So there's no manual category or question reordering, because it would be lost anyway. Only boards can be reordered. |
| P3 | **Structure left, content center, details right** | Three panes like an IDE: outline (left), board grid (center), inspector (right). The board stays visible while you edit a question. |
| P4 | **Never lose work** | Autosave to IndexedDB with a visible save status. Deletes can be undone from a snackbar. Nudge the user to export a project file. |
| P5 | **Show problems in place, don't block** | Validation runs live and shows as badges on cells, outline nodes and the app bar. It only blocks the **export**, never editing. |
| P6 | **Show the real output** | The final CDN URL is shown next to every image, and the SQL is viewable before download. The editor grid uses the game's category and question order. |
| P7 | **Keyboard friendly** | Tab order follows the reading order. Shortcuts are listed in `UX-6`. |

## UX-2 · Information architecture & navigation

```
/create                       S-1  Projects         (home of the creator)
/create/settings              S-2  Creator settings
/create/{projectId}           S-3  Editor, redirects to the first board
/create/{projectId}/board/{n} S-3  Editor with board n selected (n = 1-based order)
/create/{projectId}/quiz      S-4  Quiz settings (name, folder, base URL, replace mode)
/create/{projectId}/export    S-7  Export
(dialog)                      S-8  Import project file
(dialog)                      S-9  New project
(dialog)                      C-6  Confirm
```

The **app bar** is always visible (`C-1`). Inside a project there are three top-level **sections**: *Boards*, *Quiz settings*, *Export*.
They're shown as tabs in the app bar and each one maps to a route, so reload and browser back/forward work.

There is **no link** from the game UI (`/`, `/host`) to `/create`, because the creator is reached directly and protected by the reverse proxy.
Inside the creator, the "topg Creator" title links back to `/create`.

## UX-3 · Visual language (theme)

- Own `MudTheme` instance in `CreatorLayout`, **dark by default** (same `PaletteDark` base as `MainLayout` for brand consistency),
  with a light/dark toggle saved in the `settings` store.
- Palette usage:
  - `Primary` → main actions (New project, Export, Save in dialogs).
  - `Secondary` → type accents (see icons below).
  - `Error` / `Warning` / `Info` → validation severity only. Never used decoratively.
  - `Surface` panes on `Background`. Panes are separated by `MudDivider` or 1 px borders, not elevation.
    Elevation is only for dialogs and popovers.
- Typography: default Roboto. Page titles `Typo.h6`, pane titles `Typo.subtitle2` in uppercase with letter spacing, body `Typo.body2`.
  Nothing larger than `h5` in the creator.
- Radius: `LayoutProperties.DefaultBorderRadius = "6px"`.
- Inputs: `Variant.Outlined`, `Margin.Dense` everywhere.
- **Question type iconography** (always the same icon and label):

  | Concept | Icon | Label |
  |---|---|---|
  | Text question | `Icons.Material.Outlined.Notes` | Text |
  | Image question | `Icons.Material.Outlined.Image` | Image |
  | Sound question (future) | `Icons.Material.Outlined.MusicNote` | Sound |
  | Answer: Buzzer | `Icons.Material.Outlined.NotificationsActive` | Buzzer |
  | Answer: Text input | `Icons.Material.Outlined.Keyboard` | Text input |
  | Error | `Icons.Material.Filled.Error` (`Color.Error`) | |
  | Warning | `Icons.Material.Filled.Warning` (`Color.Warning`) | |
  | Board | `Icons.Material.Outlined.GridView` | |
  | Category | `Icons.Material.Outlined.ViewColumn` | |

## UX-4 · Global feedback patterns

| Situation | Pattern |
|---|---|
| Saved / saving / failed | `C-2` SaveStatus chip in the app bar |
| Destructive action (delete question/category/board) | Happens right away, then a `MudSnackbar` "Question deleted" with an **Undo** action (8 s). Undo restores the previous project snapshot. |
| Destructive action (delete project, overwrite on import) | `C-6` ConfirmDialog, because these can't be undone |
| Validation | `C-4` ValidationBadge on cells/nodes, `C-5` ValidationPanel |
| Long operation (hashing large image, building ZIP) | `MudProgressLinear Indeterminate` under the app bar, and the triggering button disabled with a spinner |
| Unexpected error (storage quota, corrupt import) | `MudAlert Severity.Error` inline in the relevant screen and an error snackbar. Never a silent failure. |
| Empty collections | An empty state with an icon, one sentence and one primary action (see each screen) |

## UX-5 · Layout shell

```
┌───────────────────────────────────────────────────────────────────────────────────────┐
│ C-1 AppBar                                                                              │
├───────────────────────────────────────────────────────────────────────────────────────┤
│ (MudProgressLinear, only while busy)                                                    │
├───────────────┬───────────────────────────────────────────────┬───────────────────────┤
│ Left drawer   │ Main content                                   │ Right drawer          │
│ (editor only) │                                                │ (editor only, when a  │
│ 260 px        │                                                │  question is selected)│
│               │                                                │ 400 px                │
└───────────────┴───────────────────────────────────────────────┴───────────────────────┘
```

- `CreatorLayout.razor`: `MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`
  (`Anchor = BottomLeft`), `MudLayout`, `C-1`, `MudMainContent`.
- Drawers belong to `S-3`, not the layout, so they only exist in the editor.
  - Left: `MudDrawer Variant="DrawerVariant.Responsive" Anchor="Anchor.Start" Width="260px" ClipMode="DrawerClipMode.Always"`.
  - Right: `MudDrawer Variant="DrawerVariant.Persistent" Anchor="Anchor.End" Width="400px" ClipMode="DrawerClipMode.Always"`,
    open when a question is selected.
- Below 960 px both drawers turn into `DrawerVariant.Temporary`.
- **As implemented (M3):** the panes are a CSS grid in `BoardEditor.razor(.css)` instead of `MudDrawer`s, so they exist
  only in the editor and each pane scrolls on its own. ☰ in the app bar toggles the outline. Below 960 px the panes
  overlay the board, and only one at a time (the inspector wins over the outline).

## UX-6 · Keyboard shortcuts (editor)

| Keys | Action |
|---|---|
| `Ctrl+S` | Force save (it autosaves anyway) and show "Saved" |
| `Ctrl+Z` | Undo the last delete (same as the snackbar action) |
| `Ctrl+E` | Go to `S-7` Export |
| `Esc` | Close the inspector or dialog |
| `Alt+↑ / Alt+↓` | Previous/next question in the current category while the inspector is open |
| `Alt+← / Alt+→` | Same row in the previous/next category |

Implement these with one small JS keydown listener registered by `S-3` that calls a `[JSInvokable]` method, and dispose it with the page.
Shortcuts must not fire while an IME composition is active.

---

## Reusable components

### C-1 · `CreatorAppBar`
```
┌───────────────────────────────────────────────────────────────────────────────────────────┐
│ ☰  topg Creator  /  Pub Quiz Sept   [ Boards | Quiz settings | Export ]   ⓘ2 ⚠3  ✓ Saved  ◐ ⚙   │
└───────────────────────────────────────────────────────────────────────────────────────────┘
```
- `MudAppBar Dense Elevation="0"` with a bottom border.
- Left: `MudIconButton` menu (only in the editor, toggles the left drawer). Title `MudLink Href="/create"` "topg Creator".
  When inside a project: " / " + project name (`MudText Typo.subtitle1`, ellipsis at 240 px).
- Center (project only): `MudToggleGroup` or `MudTabs`-like links for *Boards / Quiz settings / Export* (links, not state).
- Right: `C-4` summary (error/warning counts, click opens `C-5`), `C-2`,
  theme toggle (`DarkMode`/`LightMode`), settings link (`Settings` → `/create/settings`).

### C-2 · `SaveStatusChip`
`MudChip Size.Small Variant.Text` with these states:
| State | Icon | Text | Color |
|---|---|---|---|
| Saved | `CloudDone` | "Saved" (tooltip: time of the last save) | Default |
| Pending/Saving | `MudProgressCircular Size.Small` | "Saving…" | Default |
| Failed | `CloudOff` | "Not saved" (tooltip: error, click retries) | Error |

### C-3 · `ImageDropZone`
Used for the question image and answer image.
```
Empty                                       Filled
┌─────────────────────────────────┐        ┌─────────────────────────────────┐
│            ⬆                    │        │ ┌─────────────────────────────┐ │
│  Drop image here or  [Browse]   │        │ │        (thumbnail,          │ │
│  PNG, JPG, WEBP, GIF · max 10MB │        │ │     object-fit: contain)    │ │
└─────────────────────────────────┘        │ └─────────────────────────────┘ │
                                           │ cat.png · 412 KB · 1200×800     │
                                           │ …/pub-quiz-sept/3f9a1c0b2d4e5f60.png  ⧉ │
                                           │ [Replace]  [Remove]             │
                                           └─────────────────────────────────┘
```
- `MudFileUpload T="IBrowserFile" Accept=".png,.jpg,.jpeg,.webp,.gif" DragAndDrop` with a custom activator `MudPaper Outlined`,
  plus a dashed border while dragging (`Class="drag-over"`).
- Parameters: `ImageRef? Value`, `EventCallback<ImageRef?> ValueChanged`, `bool Required`, `string Label`, `string UrlPrefix`.
- Shows the **final CDN URL** (`UrlPrefix + hash.ext`) with a copy `MudIconButton` (`ContentCopy`).
- While hashing: overlay `MudProgressCircular`. Rejected file: inline `MudText Color.Error` with the reason (type or size).
- `Remove` is hidden when `Required` and a value is present (only `Replace` is possible).

### C-4 · `ValidationBadge`
- `MudBadge` (dot, or count when > 1) in `Color.Error` if there are any errors, otherwise `Color.Warning`.
  A tooltip lists the messages (max 3 + "…").
- Used on board tabs, outline nodes, grid cells, category headers, and the app bar summary.
- Input: `IReadOnlyList<ValidationIssue>` filtered for the target (`issue.Target` = project/board/category/question id).

### C-5 · `ValidationPanel`
- `MudPopover` (from the app bar) **and** embedded in `S-7`.
- A `MudList` grouped by *Errors*, then *Warnings*. Each item: severity icon, message, and location ("Board 2 › History › 300").
- Clicking an item goes to the target (board route + select question/category + focus the field).

### C-6 · `ConfirmDialog`
- `MudDialog` with a title, message, a `Cancel` (text) button and a confirm button (`Color.Error` for destructive).
  Optionally a "type the project name to confirm" field for project deletion.
- Opened through a small `IDialogService` extension: `ShowConfirmAsync(title, message, confirmText, destructive)`.

### C-7 · `QuestionTypeIcon` / `AnswerTypeIcon`
- `MudIcon` + optional label using the table in `UX-3`. Used in grid cells, the outline and the inspector.

### C-8 · `EmptyState`
- Centered `MudStack`: large outlined icon (`Size.Large`, `Color.Default`, 40% opacity), `Typo.subtitle1` title,
  `Typo.body2` description, and one optional action button.

---

## Screens

### S-1 · Projects — `/create`

**Purpose:** list, create, open, import, export and delete local projects.

```
┌ C-1 AppBar: topg Creator                                          ✓   ◐  ⚙ ┐
├────────────────────────────────────────────────────────────────────────────┤
│  Projects                                    [⬆ Import]  [＋ New project]   │
│  ⓘ Projects are only stored in this browser. Export a project file to back │
│    up or move a quiz.                                               [×]     │
│ ┌─────────────────────────────────────────────────────────────────────────┐│
│ │ 🔍 Search…                                                              ││
│ ├──────────────────────────┬────────┬───────────┬──────────────┬────────┤│
│ │ Name                    ▲│ Boards │ Questions │ Last edited  │        ││
│ ├──────────────────────────┼────────┼───────────┼──────────────┼────────┤│
│ │ Pub Quiz Sept     ⚠2     │   2    │    50     │ 2 min ago    │  ⋮     ││
│ │ Birthday Anna            │   1    │    25     │ Sep 12       │  ⋮     ││
│ └──────────────────────────┴────────┴───────────┴──────────────┴────────┘│
└────────────────────────────────────────────────────────────────────────────┘
 ⋮ menu: Open · Rename · Duplicate · Export project file · Delete
```

**Component tree**
```
MudContainer MaxWidth=Large Class="py-6"
├─ MudStack Row Justify=SpaceBetween AlignItems=Center
│  ├─ MudText Typo=h6 "Projects"
│  └─ MudStack Row
│     ├─ MudButton Variant=Outlined StartIcon=Upload "Import"          → S-8
│     └─ MudButton Variant=Filled Color=Primary StartIcon=Add "New project" → S-9
├─ MudAlert Severity=Info Dense ShowCloseIcon (dismissal saved in settings store)
└─ MudDataGrid T=ProjectSummary Dense Hover RowClick=Open QuickFilter=search SortMode=Single
   ├─ PropertyColumn Name (+ C-4 badge when the project has errors/warnings)
   ├─ PropertyColumn BoardCount, QuestionCount (Align Right)
   ├─ PropertyColumn UpdatedAt (relative time, tooltip absolute), default sort desc
   └─ TemplateColumn → MudMenu Icon=MoreVert (Open, Rename, Duplicate, Export project file, Delete)
```

**States**
| State | Display |
|---|---|
| Loading | `MudDataGrid Loading` + `MudProgressLinear` |
| Empty | `C-8`: icon `Quiz`, "No quizzes yet", "Create your first quiz or import a project file.", [New project] + text button [Import] |
| Storage unavailable (private mode / blocked) | `MudAlert Error`: "Browser storage is not available. Projects can't be saved." |

**Interactions**
| Trigger | Result |
|---|---|
| Row click / Open | Navigate to `/create/{id}` |
| Rename | Inline `MudDialog` with one text field, prefilled, Enter to save |
| Duplicate | Copy with a new id and name "… (copy)", then show a snackbar |
| Export project file | Download `{folder}.topgquiz` (WI-16) |
| Delete | `C-6` destructive, typing the name is required if the project has ≥ 1 question |

**Acceptance:** list sorted by last edited (desc). The search filters by name. All menu actions work without a reload.

---

### S-9 · New project (dialog)

```
┌ New quiz ─────────────────────────────────────┐
│ Name *          [ Pub Quiz Sept             ] │
│ CDN folder *    [ pub-quiz-sept             ] │ ← auto-slug until edited by hand
│ Base URL *      [ https://cdn.example.com/q ] │ ← prefilled from settings
│ Start with      (•) 1 board, 5 categories × 5 │
│                 ( ) Empty                     │
│                         [Cancel] [Create]     │
└───────────────────────────────────────────────┘
```
- `MudForm` with `MudTextField`s. Folder validated with `^[a-z0-9]+(-[a-z0-9]+)*$`, base URL as an absolute http(s) URL.
- "1 board, 5 × 5" creates categories "Category 1…5", each with text questions worth 100–500 and empty texts.
  These get validation errors on purpose, so they show what still needs to be filled in.
- Create → saves and navigates to `/create/{id}/board/1`.

---

### S-2 · Creator settings — `/create/settings`

```
┌ C-1 ──────────────────────────────────────────────────────────────┐
│  Settings                                                          │
│  ┌ Defaults ─────────────────────────────────────────────────────┐ │
│  │ Default base URL   [ https://cdn.example.com/quiz          ]  │ │
│  │ Used for new projects. Existing projects keep their own URL.  │ │
│  │ Default points     [ 100, 200, 300, 400, 500               ]  │ │
│  └───────────────────────────────────────────────────────────────┘ │
│  ┌ Appearance ───────────────────────────────────────────────────┐ │
│  │ Theme  [ Dark | Light | System ]                              │ │
│  └───────────────────────────────────────────────────────────────┘ │
│  ┌ Storage ──────────────────────────────────────────────────────┐ │
│  │ ███████░░░░░░░░░░  48 MB of ~2 GB used                        │ │
│  │ Persistent storage: ✓ granted   (or [Request])                │ │
│  │ ⚠ Clearing browser data deletes all projects. Export project  │ │
│  │   files regularly.                                            │ │
│  │ [Clean up unused images]  (shows how many images/MB it frees)  │ │
│  └───────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────┘
```
- `MudContainer MaxWidth=Medium`, three `MudPaper Outlined Class="pa-4"` sections, `MudProgressLinear Value` for storage.
- Saves on blur and confirms with a small snackbar.

---

### S-3 · Board editor — `/create/{id}/board/{n}`  *(main screen)*

```
┌ C-1: ☰ topg Creator / Pub Quiz Sept  [Boards|Quiz settings|Export]  ⓘ ⚠3 ✓Saved ◐ ⚙   ┐
├──────────────────┬──────────────────────────────────────────────────┬────────────────────┤
│ OUTLINE          │ [Board 1 ⚠] [Board 2] [＋]              ⋮ board │ QUESTION       [×] │
│ ▾ ▦ Board 1  ⚠   │ ─────────────────────────────────────────────────│ Board 1 › History  │
│   ▾ ▥ Geography  │ Categories are shown alphabetically, like in    │                    │
│      ≡ 100       │ the game.                        [＋ Category]  │ Type               │
│      🖼 200  ⚠   │ ┌─────────┬─────────┬─────────┬─────────┬──────┐ │ [≡ Text|🖼 Image]   │
│      ≡ 300       │ │Geography│History ✎│ Movies  │ Music   │Sport │ │ Answer via         │
│   ▸ ▥ History    │ ├─────────┼─────────┼─────────┼─────────┼──────┤ │ [🔔 Buzzer|⌨ Text] │
│   ▸ ▥ Movies     │ │ 100 ≡   │ 100 ≡   │ 100 🖼  │ 100 ≡   │100 ≡ │ │ Points  [ 300   ]  │
│   ▸ ▥ Music      │ │ Capital…│ Who was…│ (thumb) │ Which…  │ …    │ │ ────────────────── │
│   ▸ ▥ Sport      │ ├─────────┼─────────┼─────────┼─────────┼──────┤ │ Question *         │
│ ▸ ▦ Board 2      │ │ 200 🖼⚠ │ 200 ≡   │ 200 ≡   │ 200 ≡   │200 ≡ │ │ [ In which year…  ]│
│                  │ │ (empty) │ …       │ …       │ …       │ …    │ │                    │
│                  │ ├─────────┼─────────┼─────────┼─────────┼──────┤ │ Correct answer *   │
│                  │ │ 300 ≡ ◉ │ 300 ≡ ● │ …       │ …       │ …    │ │ [ 1291           ] │
│                  │ ├─────────┼─────────┼─────────┼─────────┼──────┤ │ ────────────────── │
│                  │ │ ＋      │ ＋      │ ＋      │ ＋      │ ＋   │ │ [Duplicate] [Move▾]│
│                  │ └─────────┴─────────┴─────────┴─────────┴──────┘ │ [🗑 Delete]         │
│  [＋ Board]      │                                                  │                    │
└──────────────────┴──────────────────────────────────────────────────┴────────────────────┘
 ● = selected cell (primary outline)   ⚠ = C-4 badge   ≡/🖼 = C-7 icons
```

**Component tree**
```
BoardEditorPage
├─ MudDrawer (left, UX-5) → ProjectOutline
│  ├─ MudText Typo=subtitle2 "OUTLINE"
│  ├─ MudTreeView T=OutlineNode Dense SelectionMode=Single @bind-SelectedValue
│  │  └─ MudTreeViewItem per Board (GridView icon, "Board n", C-4)
│  │     └─ per Category (ViewColumn icon, name, C-4)      — alphabetical
│  │        └─ per Question (C-7 icon, points, C-4)        — ordered by points
│  └─ MudButton Variant=Text StartIcon=Add "Board"
├─ MudMainContent area
│  ├─ BoardTabs: MudTabs Rounded Elevation=0 ActivePanelIndex ↔ route n
│  │  ├─ MudTabPanel per board: Text "Board n", BadgeData from validation
│  │  ├─ tab header suffix: MudIconButton Add (new board)
│  │  └─ MudMenu (board ⋮): Move left, Move right, Duplicate board, Delete board
│  ├─ MudStack Row: MudText Typo=caption hint (alphabetical) · MudButton "Category"
│  └─ BoardGrid (C-9, see below)
└─ MudDrawer (right, UX-5) → QuestionInspector (S-3a / S-3b); CategoryInspector (S-3c)
```

#### C-9 · `BoardGrid`
- A CSS grid (`display:grid; grid-template-columns: repeat(max(5,count), minmax(160px,1fr))`) in a `MudPaper Outlined`,
  with horizontal scroll if there are more than 5 categories. From the 6th column on, headers get a warning badge ("Only 5 fit on the game board").
- **Header cell** = category name + ✎ on hover. Click opens the `S-3c` Category inspector. Double-click edits inline.
- **Question cell** (`MudPaper Outlined Class="question-cell"`, height 88 px):
  - Top row: points (`Typo.subtitle1`, bold), `C-7` type icon, `C-4` badge right-aligned.
  - Body: the first two lines of the question text (`Typo.caption`, ellipsis), or a thumbnail for image questions
    (48 px tall, `object-fit: contain`), or "(empty)" in italics at 50% opacity.
  - Selected: 2 px `Primary` border. Hover: `action-hover` background.
- **Add cell** at the bottom of each column: a dashed outline "＋". It adds a text question with the next points value from
  the default points list (the largest existing + step), selects it and focuses the question field.
- Rows are **per column index** like the game (columns with fewer questions leave empty space at the bottom), not aligned by points.
- **Empty board** → `C-8` inside the grid area: "This board has no categories", [＋ Category].

#### S-3a · Question inspector – Text
```
QUESTION                              [×]
Board 1 › History › 300          ⚠ 1 issue
Type        [ ≡ Text | 🖼 Image ]
Answer via  [ 🔔 Buzzer | ⌨ Text input ]
Points      [ 300 ]  (MudNumericField, step 100, min 1)
──────────────────────────────────────
Question *
┌──────────────────────────────────────┐
│ In which year was the Swiss          │  MudTextField Lines=4 AutoGrow
│ Confederation founded?               │
└──────────────────────────────────────┘
Correct answer *
[ 1291                               ]
──────────────────────────────────────
[⧉ Duplicate]  [Move to… ▾]    [🗑 Delete]
```

#### S-3b · Question inspector – Image
```
QUESTION                              [×]
Board 1 › Geography › 200
Type        [ ≡ Text | 🖼 Image ]
Answer via  [ 🔔 Buzzer | ⌨ Text input ]
Points      [ 200 ]
──────────────────────────────────────
Question text *
[ Which country is this?             ]
Question image *            C-3 (Required)
Display size [ S | M | L ]  ▭ ▭▭ ▭▭▭  ← mini visual of relative size
──────────────────────────────────────
Answer text *
[ Portugal                           ]
Answer image (optional)     C-3
──────────────────────────────────────
[⧉ Duplicate]  [Move to… ▾]    [🗑 Delete]
```

**Inspector rules (S-3a/S-3b)**
- Type switch: `MudToggleGroup T=QuestionType`. Switching keeps points, answer type and question text. If the switch would
  drop filled fields (correct answer ↔ answer text/images), a `C-6` asks first (not destructive color: "Switch type").
  The correct answer and answer text are the same idea, so move the value across.
- Every field writes to the model on input (debounced 300 ms) and triggers autosave. There is no Save button.
- Required fields show the MudBlazor error state once the field is touched, or right away when the question was opened from `C-5`.
- **Move to…**: `MudMenu` listing "Board n › Category" targets. The question keeps its points.
- **Delete**: removes the question, closes the inspector and shows an Undo snackbar (UX-4).
- `Alt+↑/↓/←/→` moves the selection (UX-6). The inspector stays open.

#### S-3c · Category inspector
```
CATEGORY                              [×]
Board 1
Name *   [ History                    ]   (unique on this board)
5 questions · 100 – 500
──────────────────────────────────────
Add questions   [ Fill default points ]   ← adds missing values of the default points list
[Move to board… ▾]           [🗑 Delete category]
```
- Deleting a category with questions shows an Undo snackbar ("Category and 5 questions deleted").
- Renaming changes the category name on every contained question (the model keeps questions under the category, so it's one field).

**States (S-3)**
| State | Display |
|---|---|
| Project not found | `C-8` "Project not found" + [Back to projects] |
| Board index out of range | Redirect to `/board/1` |
| No boards | `C-8` in the main area: "No boards yet" + [＋ Board] |
| Nothing selected | Right drawer closed. The grid takes the full width. |

**Interactions (S-3)**
| Trigger | Result |
|---|---|
| Click a grid cell or outline question | Select the question, open S-3a/b, sync the selection in the grid and outline (scroll into view) |
| Click a category header or outline category | Open S-3c |
| Click a board tab or outline board | Navigate to `/board/{n}` (keeps the inspector closed) |
| Board ⋮ → Move left/right | Swap `Order`, update the route to the new index |
| ＋ Board | Append an empty board, navigate to it |
| Delete board | Undo snackbar. If it was the last board → "No boards" state. |

**Acceptance:** you can fully author a quiz without leaving this screen (except quiz settings). The grid, outline and inspector
selection are always in sync. The grid ordering matches `Board.razor` for the same data.

---

### S-4 · Quiz settings — `/create/{id}/quiz`

```
┌ C-1 … [Boards | Quiz settings | Export] ───────────────────────────────┐
│  Quiz settings                                                          │
│  ┌ General ───────────────────────────────────────────────────────────┐ │
│  │ Name *        [ Pub Quiz Sept                     ]                │ │
│  │ Shown to the host when choosing a template.                        │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│  ┌ Images / CDN ──────────────────────────────────────────────────────┐ │
│  │ Base URL *    [ https://cdn.example.com/quiz      ]  [Use default] │ │
│  │ Folder *      [ pub-quiz-sept                     ]                │ │
│  │ Example: https://cdn.example.com/quiz/pub-quiz-sept/3f9a…c0.png    │ │
│  │ ⚠ Changing base URL or folder changes all image URLs. Re-upload   │ │
│  │   the images if you already exported. (only if exported before)   │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│  ┌ Database import ───────────────────────────────────────────────────┐ │
│  │ [✓] Replace existing quizzes with the same name                    │ │
│  │     The SQL first deletes every template named "Pub Quiz Sept"     │ │
│  │     including its boards and questions.                            │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│  ┌ Backup ────────────────────────────────────────────────────────────┐ │
│  │ Last project file export: never   [⬇ Export project file]          │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```
- `MudContainer MaxWidth=Medium`, one `MudPaper Outlined` per section, `MudSwitch Color=Primary` for replace mode.
- The example URL updates live and uses `Typo.body2` monospace (`font-family: monospace`).
- Track `LastExportedAt` / `LastProjectFileExportAt` on the project (WI-04) so the warnings and "last export" can show.

---

### S-5 · Preview — out of scope

A full-screen game-style preview was planned here (WI-13) and dropped: the editor grid already shows the game's
category and question order, so a separate preview adds little. The number S-5 is kept free so the other screen
numbers stay stable.

---

### S-7 · Export — `/create/{id}/export`

```
┌ C-1 … [Boards | Quiz settings | Export] ───────────────────────────────┐
│  Export                                                                 │
│  ① Check ──────── ② Review ──────── ③ Download                          │
│                                                                         │
│  ① Check                                                                │
│  ✖ 1 error must be fixed before exporting                               │
│  ⚠ 3 warnings                                                           │
│   C-5 ValidationPanel (embedded, click jumps to editor)                 │
│   [ ] I have reviewed the warnings            [Continue →] (disabled)   │
│                                                                         │
│  ② Review                                                               │
│  ┌ Summary ───────────────┐ ┌ SQL preview ───────────────────────────┐  │
│  │ Name: Pub Quiz Sept    │ │ -- topg quiz import                    │  │
│  │ Boards: 2              │ │ DO $$                                  │  │
│  │ Questions: 50          │ │ DECLARE template_id bigint; …          │  │
│  │ Images: 14 (8.2 MB)    │ │ …                              [⧉ Copy]│  │
│  │ Replace existing: yes  │ └────────────────────────────────────────┘  │
│  │ Folder: pub-quiz-sept  │ ┌ Images ────────────────────────────────┐  │
│  └────────────────────────┘ │ ▢ 3f9a1c0b2d4e5f60.png  412 KB  B1›Geo… │  │
│                             │ ▢ …                                     │  │
│                             └────────────────────────────────────────┘  │
│                                                  [← Back] [Continue →]  │
│  ③ Download                                                             │
│  [⬇ Download pub-quiz-sept-export.zip]                                  │
│  Next steps:                                                            │
│   1. Upload the folder "pub-quiz-sept/" to https://cdn.example.com/quiz │
│   2. Run import.sql against the topg database                           │
│   3. Start a game at /host                                              │
└─────────────────────────────────────────────────────────────────────────┘
```
- `MudStepper` (linear) with three `MudStep`s. Step ① can be completed when there are **0 errors** and (warnings = 0 or the checkbox is ticked).
- SQL preview: `MudPaper Outlined` with `<pre>` monospace, max height 360 px with scroll, and a copy button (`C-3`-style ContentCopy).
- Images: `MudSimpleTable Dense` with a thumbnail, file name, size and where it's used (click jumps to the question).
- Download: `MudButton Filled Primary Size.Large`, busy state while zipping. On success, set `LastExportedAt` and show a snackbar.
- The "Next steps" are generated from the project values (base URL, folder).

---

### S-8 · Import project file (dialog)

```
┌ Import project file ─────────────────────────────┐
│  ┌─────────────────────────────────────────────┐ │
│  │   ⬆  Drop a .topgquiz file or [Browse]      │ │
│  └─────────────────────────────────────────────┘ │
│  ✓ Pub Quiz Sept · 2 boards · 50 questions ·     │
│    14 images · saved Sep 20                      │
│  ⚠ A project with this id already exists         │
│    (•) Import as copy   ( ) Overwrite existing   │
│                              [Cancel] [Import]   │
└──────────────────────────────────────────────────┘
```
- The file is read and validated **before** Import is enabled. Errors are shown inline (`MudAlert Error`):
  "Not a topg project file", "Created by a newer version (schema 3) – please update", "Image … is corrupt (hash mismatch)".
- Overwrite → a second `C-6` destructive confirmation.
- On success → close, snackbar "Imported" with an [Open] action.

---

## UX-7 · Validation message catalog

Use these texts so the UI stays consistent (the WI-04 validator produces them; `{…}` are placeholders).

| Code | Severity | Target | Message |
|---|---|---|---|
| `project.name.empty` | Error | Project | Quiz name is required. |
| `project.folder.invalid` | Error | Project | Folder may only contain lowercase letters, digits and dashes. |
| `project.baseUrl.invalid` | Error | Project | Base URL must be an absolute http(s) URL. |
| `project.noBoards` | Error | Project | The quiz needs at least one board. |
| `board.noCategories` | Error | Board | Board {n} has no categories. |
| `board.tooManyCategories` | Warning | Board | Board {n} has {count} categories – only 5 fit on the game board. |
| `category.name.empty` | Error | Category | Category name is required. |
| `category.name.duplicate` | Error | Category | Category "{name}" exists twice on board {n}. |
| `category.noQuestions` | Warning | Category | Category "{name}" has no questions. |
| `question.text.empty` | Error | Question | Question text is required. |
| `question.answer.empty` | Error | Question | Correct answer is required. |
| `question.image.missing` | Error | Question | Question image is required. |
| `question.points.invalid` | Error | Question | Points must be greater than 0. |
| `question.points.duplicate` | Warning | Question | Another question in "{category}" also has {points} points – order is undefined. |

Unused images are not a validation issue: images are shared between projects (stored once per hash), so "unused"
is a browser-wide state. It is shown and cleaned up in S-2 ("Clean up unused images").

There is intentionally no "category order" warning: by principle P2 the editor always shows the alphabetical order the game uses.
