# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Jeopardy-style party game (.NET 10, Blazor, MudBlazor, PostgreSQL via EF Core, orchestrated with Aspire). A host
runs a quiz from `/host`, players buzz in on their phones, spectators watch a big screen. Quizzes are authored in the
browser at `/create` and imported into the database with a generated SQL script. See `README.md` for the end-user
creator/export/import workflow and the reverse-proxy setup for protecting `/create`.

## Commands

The solution file is `src/topg.slnx`.

```bash
dotnet run --project src/topg.AppHost          # PostgreSQL (+ pgAdmin), migration service, web app; Docker required
dotnet build src/topg.slnx
dotnet test src/topg.slnx                      # all tests
dotnet test src/topg.Web.Client.Tests --filter "FullyQualifiedName~ExportTests"   # single class / test
UPDATE_GOLDEN=1 dotnet test src/topg.Web.Client.Tests                            # rewrite SQL golden files
```

- Integration tests (`topg.Web.IntegrationTests`) use Testcontainers PostgreSQL and `[SkippableFact]`; without Docker
  they are skipped, not failed.
- `topg.Web.Tests` holds bUnit tests for the game's Razor components. MudBlazor services only support async
  disposal, so test classes own a `BunitContext` and dispose it in `IAsyncLifetime.DisposeAsync`.
- Golden files live in `src/topg.Web.Client.Tests/Golden`. A missing golden file is written and the test fails once.
  After an intended change to SQL output, regenerate with `UPDATE_GOLDEN=1` and review the git diff.
- EF migrations live in `src/topg.Web/Migrations`; `QuizContextFactory` provides the design-time context, e.g.
  `dotnet ef migrations add <Name> --project src/topg.Web`. They are applied at runtime by `topg.MigrationService`
  (in non-Development, `topg.Web` waits for migrations itself since Swarm has no `depends_on`).

## Architecture

### Two render modes in one host
`topg.Web/Components/App.razor` switches on the request path: anything under `/create` renders
`topg.Web.Client.CreatorRoutes` as **InteractiveWebAssembly with prerendering disabled** (it needs IndexedDB); every
other route renders the game's `Routes` as **InteractiveServer**. `topg.Web.Client` has its own router, layout and
DI setup (`topg.Web.Client/Program.cs`), and its CSS/JS (`creator.css`, `creator-*.js`) is only loaded for `/create`.

### Game (`topg.Web`, Blazor Server)
- `Templating/` — persisted quiz definitions: `QuizTemplate` → `Board` → `Question` (TPH on `QuestionType`:
  `TextQuestion`, `ImageQuestion`, `SoundQuestion`), loaded through `ITemplateService` / `QuizContext`.
- `Quiz/` — in-memory live games. `SessionHandler` (singleton) holds all `QuizSession`s keyed by `SessionId`;
  `SessionCleanupService` evicts unused ones. A `QuizSession` wraps a `QuizExecution` (runtime copy of a template)
  plus buzzer/text-input/timer/sound state and the player list. Every mutation calls `SessionStateHasChanged()`,
  which raises `SessionStateChanged`.
- Host, player and spectator pages derive from `Components/Pages/QuizSessionBase`, which subscribes to the session
  events and re-renders via `InvokeAsync(StateHasChanged)`. That event fan-out across circuits is how all screens stay
  in sync, so state changes must go through `QuizSession` methods. `IsInUse` is derived from event subscribers, so
  pages must unsubscribe on dispose.
- Player identity is `name.HMAC(sessionSecret, name)`, kept in protected browser storage.

### Quiz creator (`topg.Web.Client/Creator`, Blazor WebAssembly)
Runs entirely in the browser and never talks to the database or server.
- `Model/` — `QuizProject` and drafts; `ProjectEditing` holds edit operations. `GameOrder` reproduces the game's
  ordering (categories alphabetical, questions by points) and must stay consistent with the game.
- `Storage/` — projects and images in IndexedDB (`IndexedDbCreatorStorage` + `wwwroot/creator-storage.js`), with
  `ProjectSession` / `ProjectAutosave`. Tests use fakes behind `ICreatorStorage` (`Fakes.cs`).
- `Images/` — images are content-addressed: file name is a hash of the content, URL is `{baseUrl}/{folder}/{hash}.{ext}`.
- `Validation/` — `ProjectValidator` produces the issues shown in the app bar and gating export.
- `Export/` — `SqlExport` produces one atomic `DO` block using `RETURNING … INTO`; `ExportPackage` builds the ZIP
  (`import.sql` + images); `ProjectFile` is the `*.topgquiz` backup format (not read by the game).

### Shared contract between creator and game
`topg.Web` references `topg.Web.Client`, not the reverse. Enums shared by both (`QuestionType`, `AnswerType`,
`ImageSize`) live in `topg.Web.Client/Shared`. `SqlExport` hard-codes table/column names from
`topg.Web/Migrations/QuizContextModelSnapshot.cs`, so **any schema change to the templating entities must be
mirrored in `SqlExport`**, its golden files, and `topg.Web.IntegrationTests/SqlImportTests` (which runs the generated
SQL against real migrations and loads it like the game does).

### Deployment
`topg.AppHost` also publishes Docker Compose for Docker Swarm (overlay networks `topg_internal` and external `topg`,
container registry from `REGISTRY_ENDPOINT`/`REGISTRY_REPOSITORY`, Postgres data bind mount via `PostgresDataPath`).

## Backlog

Bugs and features are tracked as GitHub issues. Before writing or refining an issue, read `docs/issues.md`, which
defines the required structure (user story, Given/When/Then acceptance criteria, labels). Before implementing an
issue, treat its acceptance criteria as the definition of done.

## Comments

Only write a comment when it explains **why** the code behaves the way it does: a constraint, a workaround, a
non-obvious decision, or a coupling to another part of the system (e.g. "the game groups by the exact category
string, so duplicates would merge"). Don't write comments that restate **what** the code does. That includes XML doc
summaries that repeat the member name, section markers like `// Boards`, layout labels like `@* Top row *@`, and
references to spec IDs (`S-3`, `C-5`, `WI-14`) that carry no reason. Don't leave commented-out code in place either.
If a comment mixes the two, keep only the reason.
