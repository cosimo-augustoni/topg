# topg
Totally original party game

A Jeopardy-style game show: a host runs a quiz from `/host`, players buzz in on their phones and spectators follow on
a big screen. Quizzes are created in the browser at `/create` and imported into the database with a generated SQL
script.

## Projects

| Project | What it is |
|---|---|
| `src/topg.AppHost` | Aspire app host (local run and Docker Compose publishing) |
| `src/topg.Web` | The game (Blazor Server) and the host for the quiz creator |
| `src/topg.Web.Client` | The quiz creator at `/create` (Blazor WebAssembly, runs entirely in the browser) |
| `src/topg.MigrationService` | Applies the EF Core migrations on startup |
| `src/topg.Web.Client.Tests` | Unit tests of the creator (validation, storage, SQL golden files, project files) |
| `src/topg.Web.IntegrationTests` | Runs the generated SQL against a real PostgreSQL (Testcontainers) and loads it like the game |

## Running locally

```bash
dotnet run --project src/topg.AppHost
```

This starts PostgreSQL (plus pgAdmin), the migration service and the web app. Docker must be running.

Tests:

```bash
dotnet test src/topg.slnx
```

The integration tests need Docker; without it they are skipped, not failed. The SQL golden files live in
`src/topg.Web.Client.Tests/Golden`. After an intended change to the SQL output, run the tests with `UPDATE_GOLDEN=1`
and review the diff.

## Creating a quiz (`/create`)

The creator has no server-side storage and no login. Everything you create lives in your browser until you export it.

1. **Create** a project at `/create` and fill in the boards, categories and questions. Categories are always shown
   alphabetically and questions by points, the same way the game shows them. The ⚠/✖ counts in the app bar list
   everything that still needs fixing.
2. In **Quiz settings**, set the **base URL** of your CDN and the **folder** the images go into. An image URL is
   `{base URL}/{folder}/{hash}.{ext}`, where the file name is derived from the image content, so it is stable
   and unique. The default base URL for new projects can be set in the creator settings (⚙).
3. **Export** checks the quiz, shows the SQL and the image list, and downloads `{folder}-export.zip`:
   ```
   {folder}-export.zip
   ├─ import.sql
   └─ {folder}/
      ├─ 3f9a1c0b2d4e5f60.png
      └─ …
   ```
4. **Upload** the `{folder}/` directory to the root of your CDN (under the base URL), so the URLs in `import.sql`
   resolve.
5. **Run** `import.sql` against the topg database, e.g.
   ```bash
   docker exec -i <db-container> psql -U postgres -d topg < import.sql
   ```
   The script is a single transaction. With "Replace existing quizzes with the same name" enabled (Quiz settings),
   it first deletes every template with the same name, so you can re-import a corrected quiz.
6. Start a game at `/host` and choose the quiz.

### Where drafts are stored

Projects and images are stored in the browser's IndexedDB, only on this device and only in this browser profile.
They are gone if the site data is cleared, and private windows don't keep them. The creator asks the browser for
persistent storage so it doesn't evict the data on its own. You can also request it in the creator settings, where
you can also see how much space is used and remove unused images.

### Project files (`*.topgquiz`)

A project file contains the project and all its images. Use it to:

- **back up** a quiz (Quiz settings shows when you last exported one),
- **move** a quiz to another browser or computer, or give it to someone else to edit,
- **keep** a quiz you want to change and re-import later.

Export it from the project menu on the Projects page or from Quiz settings. Import it with **Import** on the Projects
page. If the project already exists in this browser, you can import it as a copy or overwrite the existing one.

The project file is not what the game reads. The game only uses the export ZIP (`import.sql` and the images).

## Protecting `/create`

The creator has no authentication of its own. Protect it in the reverse proxy.

- Protect the page routes: `/create` and everything below `/create/`.
- **Don't** protect `/_framework/*`, `/_content/*` or `/_blazor`. The game uses them too. The WebAssembly bundle under
  `/_framework` only contains the creator's code and no secrets or connection strings: the creator never talks to the
  database, it only generates a script that you run yourself.
- The creator's static files (`/creator.css`, `/creator-*.js`) contain no secrets either and may stay public.

Example with Traefik (Docker Swarm labels) and basic auth:

```yaml
services:
  web:
    deploy:
      labels:
        # Game (public)
        - traefik.http.routers.topg.rule=Host(`topg.example.com`)
        - traefik.http.routers.topg.entrypoints=websecure
        - traefik.http.services.topg.loadbalancer.server.port=${WEB_PORT}   # the port from HTTP_PORTS
        # Quiz creator (basic auth)
        - traefik.http.routers.topg-create.rule=Host(`topg.example.com`) && (Path(`/create`) || PathPrefix(`/create/`))
        - traefik.http.routers.topg-create.entrypoints=websecure
        - traefik.http.routers.topg-create.priority=100
        - traefik.http.routers.topg-create.service=topg
        - traefik.http.routers.topg-create.middlewares=topg-create-auth
        # htpasswd -nB user; in a compose file every $ of the hash must be written as $$
        - traefik.http.middlewares.topg-create-auth.basicauth.users=user:$$2y$$05$$...
```

Use `Path(`/create`) || PathPrefix(`/create/`)` rather than `PathPrefix(`/create`)`, because the prefix alone also
matches `/creator.css` and `/creator-storage.js`. The explicit `priority` makes sure the creator router wins over the
game's router. Other proxies work the same way: require authentication for `/create` and `/create/*`, and pass
everything else through.
