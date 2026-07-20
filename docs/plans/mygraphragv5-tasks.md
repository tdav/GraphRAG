# MyGraphRagV5 — Execution Tasks

Source design: [2026-07-20-mygraphragv5-plan.md](2026-07-20-mygraphragv5-plan.md). This file breaks it into dispatchable tasks with verbatim binding values.

## Global Constraints (bind every task)

- **Runtime:** .NET 10 (`net10.0`), C# LangVersion 14. Central package management — all NuGet versions go in `Directory.Packages.props` (`<PackageVersion .../>`), project files use `<PackageReference Include="..." />` WITHOUT Version.
- **New web project** `src/MyGraphRagV5`: `Sdk="Microsoft.NET.Sdk.Web"`, `<RootNamespace>MyGraphRagV5</RootNamespace>`, `<IsPackable>false</IsPackable>`, `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` (Razor generators trip TWAE inherited from Directory.Build.props). ProjectReference to `..\ManagedCode.GraphRag\ManagedCode.GraphRag.csproj` and `..\ManagedCode.GraphRag.Postgres\ManagedCode.GraphRag.Postgres.csproj`.
- **Code style (user rule):** private fields WITHOUT `_` prefix — `private readonly IFooService fooService;` and reference via `this.fooService`. Identifiers/comments/commit messages in English.
- **Database (single existing Postgres, docker):** `Host=localhost;Port=6000;Database=graphdb;Username=postgres;Password=M@y_Passw0rd`. Extensions `vector` and `age` already installed. EF Core app tables live in schema `app`; AGE uses `ag_catalog`; pgvector tables `vec_*` in `public`.
- **LLM:** Ollama Cloud, model id `nemotron-3-super:cloud`, OpenAI-compatible endpoint `https://ollama.com/v1`. API key ONLY from user-secrets key `Ollama:ApiKey` or env `OLLAMA_API_KEY` — NEVER hardcode or commit it.
- **Embeddings (TEI):** `POST http://localhost:14401/embed`, request `{"inputs": ["text", ...]}`, response `float[][]`. Dimension unknown → configurable + probe at startup.
- **Reranker (TEI):** `POST http://localhost:18200/rerank`, request `{"query": "...", "texts": ["...", ...]}`, response `[{"index": int, "score": float}, ...]`.
- **Design system "Ember"** (DESIGN.json/DESIGN.md, repo root): background `#2D2D35`, panel `#363640`, inset/well `#1E1E26`, surface `#3A3A45`; accent Ember Orange `#FF6B2C` (+ light `#FF8C42`) ONLY for interactive elements. Fonts: Inter (UI) + JetBrains Mono (data: paths, sizes, GUIDs, timestamps). Flat elevation, hairline 1px borders (`rgba(255,255,255,0.08)`), box-shadow only for overlays. NO neumorphism, NO glassmorphism, NO gradient text. **Dark theme is default** (`data-theme="dark"` on `<html>`), light toggle in localStorage. Status semantic colors (status also always shown as text, never color alone): success `#4ADE80`, warning `#FFB02E`, danger `#FF4D4D`, info `#4DA6FF`, muted `#6B7280`. Map run status → Running=info, Succeeded=success, Failed=danger, Cancelled/Pending=muted, Degraded=warning.
- **UI convention (user rule):** modal action buttons right-aligned; primary (Save/OK) rightmost, Cancel to its left.
- **FilePattern** default per project: `.*\.(cs|md)$`.
- **Library changes:** the ONLY permitted modification to `ManagedCode.GraphRag` is making `IndexingPipelineRunner.RunAsync` accept optional `IWorkflowCallbacks` — must stay backward compatible (existing call sites and tests unchanged).
- **Tests approved** (user confirmed unit + integration). Each component task writes focused unit tests; Testcontainers integration tests consolidated in Task 10. xunit version matches repo (`2.9.3`).
- Language of any user-facing UI text: Russian is fine (matches design docs), but keep code identifiers English.

---

## Task 1 — Project scaffolding and solution integration

Goal: web + test projects exist, wired into the solution, and everything builds green.

Create:
- `src/MyGraphRagV5/MyGraphRagV5.csproj` per Global Constraints (Web SDK, ProjectReferences, IsPackable=false, TreatWarningsAsErrors=false, `<UserSecretsId>mygraphragv5</UserSecretsId>`).
- Minimal `src/MyGraphRagV5/Program.cs` (builder + `MapGet("/")` health stub + Razor Pages services registered but no pages yet is fine) so the project builds and runs.
- `tests/MyGraphRagV5.Tests/MyGraphRagV5.Tests.csproj` (xunit 2.9.3, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, ProjectReference to the web project and to `ManagedCode.GraphRag`), with one trivial passing smoke test.

Modify:
- `GraphRag.slnx` — add `src/MyGraphRagV5/MyGraphRagV5.csproj` under the `/src/` folder and `tests/MyGraphRagV5.Tests/MyGraphRagV5.Tests.csproj` under `/tests/`. Match existing element format in the file.
- `Directory.Packages.props` — add `<PackageVersion>` entries (resolve exact latest-compatible 10.x / current versions at implementation time via `dotnet add`): `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Pgvector`, `Microsoft.Extensions.AI.OpenAI` (preview) + pin transitive `OpenAI`. (Only add versions actually referenced now; later tasks add more.)

Verify: `dotnet build GraphRag.slnx` green; `dotnet test tests/MyGraphRagV5.Tests` passes the smoke test. Do NOT break existing library/test builds.

## Task 2 — EF Core model, DbContext, migrations

Goal: relational schema for the app's own data, migratable against graphdb:6000.

Create under `src/MyGraphRagV5/Data/`:
- `Entities.cs`: `RagProject` (Id Guid PK, Name string unique, SourceFolder string, GraphName string unique, VectorCollection string, FilePattern string default `.*\.(cs|md)$`, EmbeddingDimension int?, CreatedAt DateTimeOffset), `IndexingRun` (Id, ProjectId FK, Status enum-as-string Pending/Running/Succeeded/Failed/Cancelled, StartedAt, CompletedAt?, CurrentWorkflow?, ProgressPercent double?, Error?, DocumentCount int?), `ChatSession` (Id, ProjectId FK, Title, CreatedAt), `ChatMessage` (Id, SessionId FK, Role, Content, SourcesJson jsonb string?, CreatedAt).
- `AppDbContext.cs`: DbSets; `OnModelCreating` sets `HasDefaultSchema("app")`, `HasPostgresExtension("vector")`, unique indexes on `RagProject.Name`/`GraphName`, jsonb column type for `SourcesJson`. Use `this.`-style, no `_` fields.
- DI registration extension `AddAppDatabase(this IServiceCollection, string connectionString)` using `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Initial migration in `Data/Migrations/` (`dotnet ef migrations add InitialCreate`). App calls `db.Database.MigrateAsync()` at startup.

Verify: `dotnet ef migrations add` succeeds; build green. Migration apply against live DB is validated in Task 10 / manual run (note if DB unreachable).

## Task 3 — PgVectorStore (implements IVectorStore)

Goal: pgvector-backed implementation of `GraphRag.Vectors.IVectorStore` (read its exact signature in `src/ManagedCode.GraphRag/Vectors/IVectorStore.cs` + `VectorSearchResult`).

Create `src/MyGraphRagV5/Vectors/PgVectorStore.cs`:
- Raw Npgsql via a shared `NpgsqlDataSource` built with `NpgsqlDataSourceBuilder(conn).UseVector()` (Pgvector package). Register as singleton `IVectorStore`.
- One table per collection `vec_{sanitized}` (sanitize to `[a-z0-9_]`): `id text primary key, embedding vector(N), text text, metadata jsonb, updated_at timestamptz default now()`. Create lazily (`CREATE TABLE IF NOT EXISTS` + HNSW index `USING hnsw (embedding vector_cosine_ops)`), N from first embedding length; cache created-collection names in a `ConcurrentDictionary`.
- `UpsertAsync`: `INSERT ... ON CONFLICT (id) DO UPDATE`; id from metadata `id` else new Guid.
- `SearchAsync`: `SELECT id, text, metadata, 1-(embedding <=> $1) AS score ... ORDER BY embedding <=> $1 LIMIT k`, yield `VectorSearchResult`.

Tests (`tests/MyGraphRagV5.Tests/Vectors/`): focused unit tests for name sanitization and SQL shaping that don't need a DB; the live pgvector round-trip is a Testcontainers test deferred to Task 10 (leave a clearly-named skipped/pending marker or put it in Task 10).

Verify: build green; unit tests pass.

## Task 4 — AI clients: Ollama chat, TEI embedding, TEI reranker

Goal: register `IChatClient`, `IEmbeddingGenerator<string,Embedding<float>>`, and an app-owned `IReranker`.

Create under `src/MyGraphRagV5/Ai/`:
- `TeiEmbeddingGenerator.cs : IEmbeddingGenerator<string, Embedding<float>>` — HttpClient POST `/embed` `{"inputs":[...]}` → `float[][]`; batch by configured size; expose probed dimension.
- `IReranker.cs` (`Task<IReadOnlyList<RerankResult>> RerankAsync(string query, IReadOnlyList<string> texts, int topN, CancellationToken)`, `record RerankResult(int Index, double Score)`) + `TeiReranker.cs` — POST `/rerank`.
- `AiServiceCollectionExtensions.cs` — `AddAiClients(config)`: keyed chat client `nemotron-3-super:cloud` via `new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions{Endpoint=new Uri("https://ollama.com/v1")}).GetChatClient(model).AsIChatClient()` registered keyed + unkeyed; named HttpClients "tei-embed"/"tei-rerank" with BaseAddress from config; embedding generator keyed by model id + unkeyed.
- Options records bound from config: `OllamaOptions{Endpoint,Model}`, `TeiOptions{EmbedUrl,RerankUrl,EmbedBatchSize}`.

Tests: `TeiEmbeddingGenerator`/`TeiReranker` with a stubbed `HttpMessageHandler` asserting request shape and response parsing.

Verify: build green; unit tests pass.

## Task 5 — Library callback injection + RunProgressCallbacks

Goal: web UI can observe indexing progress.

Modify `src/ManagedCode.GraphRag/Indexing/IndexingPipelineRunner.cs`: change `RunAsync(GraphRagConfig config, CancellationToken ct = default)` → `RunAsync(GraphRagConfig config, IWorkflowCallbacks? callbacks = null, CancellationToken ct = default)`; line 27 becomes `var callbacks = callbacks ?? _services.GetService<IWorkflowCallbacks>() ?? NoopWorkflowCallbacks.Instance;` (rename local to avoid clash). Confirm existing callers/tests still compile (backward compatible via defaults).

Create `src/MyGraphRagV5/Indexing/RunProgressCallbacks.cs : IWorkflowCallbacks` (read `Callbacks/IWorkflowCallbacks.cs` + `Logging/ProgressSnapshot.cs`) that writes current workflow name + ProgressSnapshot into a run-scoped sink (interface `IRunProgressSink` with `Update(Guid runId, string? workflow, ProgressSnapshot?)`), implemented against the registry from Task 6 (define the interface here, wire in Task 6).

Verify: `dotnet build GraphRag.slnx` green; `dotnet test tests/ManagedCode.GraphRag.Tests` still green (regression — the only library change).

## Task 6 — Indexing orchestration

Goal: start/track/cancel per-project indexing, then sync artifacts into AGE + pgvector.

Create under `src/MyGraphRagV5/Indexing/`:
- `RunRegistry.cs`: `ConcurrentDictionary<Guid, RunHandle>` (RunHandle: runId, projectId, CTS, live workflow+progress); implements `IRunProgressSink` from Task 5. One active run per project (reject second).
- `ProjectGraphStoreProvider.cs`: builds/caches a `PostgresGraphStore` per project GraphName at runtime (keyed DI is start-only), mirroring `AddPostgresGraphStore` wiring (`PostgresGraphStoreOptions{ConnectionString,GraphName}`, `AgeConnectionManager`, `AgeClientFactory`). Read `src/ManagedCode.GraphRag.Postgres/ServiceCollectionExtensions.cs`.
- `IndexingService.cs`: `StartRunAsync(projectId)` → insert IndexingRun(Running); build `GraphRagConfig` (Input.Storage.BaseDir=SourceFolder, FilePattern, FileType=Text, output dir `data/output/{projectId}`, Models set to the model id, EmbedText.ModelId); run `IndexingPipelineRunner.RunAsync(config, runCallbacks, ct)`; then read output artifacts (`PipelineTableNames.Entities/Relationships` via `IPipelineStorage.ReadTableAsync`) → `IGraphStore.UpsertNodesAsync/UpsertRelationshipsAsync`; read `TextUnits`/`CommunityReports` → embed via TeiEmbeddingGenerator → `PgVectorStore.UpsertAsync(VectorCollection, ...)`; persist final status/error + DocumentCount. Probe & persist EmbeddingDimension on first run.
- `CancelRun(runId)`.
- Background execution via `Task.Run` tracked in the registry (no hosted-service queue — ponytail; note upgrade path).

Read the workflow record shapes in `src/ManagedCode.GraphRag/Indexing/**` (Entities/Relationships/TextUnits record types, `PipelineTableNames`) before mapping to graph/vector upserts.

Verify: build green. End-to-end run against a small sample folder validated manually / Task 10 (note if DB/containers down).

## Task 7 — RagQueryService

Goal: RAG question-answering per project.

Create `src/MyGraphRagV5/Query/RagQueryService.cs`:
```
Task<RagAnswer> AskAsync(Guid projectId, string question, CancellationToken ct);
IAsyncEnumerable<string> StreamAnswerAsync(Guid projectId, string question, CancellationToken ct);
record RagAnswer(string Answer, IReadOnlyList<SourceRef> Sources);
record SourceRef(string Id, string Title, string Snippet, double Score);
```
Flow: embed question → `IVectorStore.SearchAsync(project.VectorCollection, top 20)` → collect entity names/text-unit ids from metadata → graph expand via `ProjectGraphStoreProvider` store (`GetNodesAsync` + 1-hop `GetOutgoingRelationshipsAsync`, cap ~30 facts) → assemble candidate passages → `IReranker.RerankAsync(question, passages, topN 8)` → prompt (system: answer only from context, cite `[n]`) → `IChatClient` streaming → persist Q/A + sources to `chat_messages`.

Tests: `RagQueryServiceTests` with fake `IVectorStore`/`IGraphStore`/`IChatClient`/`IReranker` verifying the retrieval→rerank→prompt orchestration and source assembly.

Verify: build green; unit tests pass.

## Task 8 — Web UI (Razor Pages + HTMX + Tailwind, Ember theme)

Goal: server-rendered UI, dark default, four sections. (If this grows too large, report DONE_WITH_CONCERNS and it will be split.)

- Tailwind v4 standalone CLI invoked from csproj `Exec` target (no Node); Ember tokens as CSS variables in `wwwroot/css/ember.css` + Tailwind `@theme`. Include Inter + JetBrains Mono. HTMX via local `wwwroot/lib/htmx.min.js`.
- `Pages/Shared/_Layout.cshtml`: 260px sidebar (Дашборд, Проекты, Поиск, Граф), dark-default theme toggle (localStorage), flat elevation + hairline.
- `Pages/Index.cshtml` — dashboard: project count, per-project graph sizes (nodes/edges/communities via IGraphStore), last runs, service-health panel (HTMX poll `/healthz`).
- `Pages/Projects/Index.cshtml` + `Create/Edit` + `Details` — CRUD RagProject; "Start indexing" button; run history table with status badge; live progress partial polled `hx-trigger="every 2s"` from RunRegistry.
- `Pages/Chat/Index.cshtml` — pick project, ask question, render answer + sources; streaming via minimal-API `text/event-stream` endpoint + htmx SSE (fallback: non-streamed partial).
- `Pages/Graph/Index.cshtml` + `Node.cshtml` — paged entities/relationships/communities tables + node detail (outgoing relationships). No graph visualization lib.

Verify: build green; app starts (`dotnet run`) and pages render (manual/Playwright smoke). Status colors and dark theme match Ember tokens.

## Task 9 — Health checks and configuration

Goal: config layout + health endpoint feeding the dashboard.

- `appsettings.json` / `appsettings.Development.json`: `ConnectionStrings:GraphDb`, `Ollama:{Endpoint,Model}`, `Tei:{EmbedUrl,RerankUrl,EmbedBatchSize}`. `Ollama:ApiKey` NOT in file — user-secrets/env. Wire `AddAppDatabase`, `AddAiClients`, `AddGraphRag`, PgVectorStore, orchestration services in `Program.cs`.
- Health checks: `AddHealthChecks().AddNpgSql(...)` + custom `IHealthCheck` for Ollama (`/v1/models`), TEI embed, TEI rerank; `/healthz` JSON writer consumed by dashboard HTMX panel.

Verify: build green; `/healthz` returns JSON (services may report Unhealthy if containers down — that's expected and correct).

## Task 10 — Integration tests (Testcontainers) + consolidation

Goal: DB-backed tests for the pieces that need a real Postgres.

- Add `age-pgvector.Dockerfile` (`FROM apache/age:latest` + install pgvector matching its PG major) used via Testcontainers `ImageFromDockerfileBuilder`; reuse the `ALTER SYSTEM SET shared_preload_libraries='age'` + `pg_reload_conf()` trick from `tests/ManagedCode.GraphRag.Tests/GraphRagApplicationFixture.cs`.
- `PgVectorStoreIntegrationTests`: real upsert + cosine search round-trip.
- `AppDbContext` migration smoke test (apply `InitialCreate` to the container).
- (If feasible) a thin end-to-end indexing test over a tiny fixture folder with fake embedding/chat clients.

Confirm CLAUDE.md test rule already satisfied (user approved). Verify: `dotnet test` green (integration tests may be skipped when Docker unavailable — gate with a fact/skip).

## Task 11 — docker-compose reference

Goal: reproducible environment reference (app targets existing :6000 instance).

Create `docker/docker-compose.yml` + `docker/postgres-age-pgvector.Dockerfile` (apache/age + pgvector, `shared_preload_libraries=age`, port `6000:5432`, db graphdb, user/pw as Global Constraints) and optional TEI embedding (14401) / reranker (18200) services. Add a short `docker/README.md`. Documentation-only; nothing in the app depends on compose.

Verify: `docker compose config` parses (if docker present) or YAML lints; no app code change.
