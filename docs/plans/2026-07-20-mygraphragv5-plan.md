# План: MyGraphRagV5 — ASP.NET Core web-app поверх ManagedCode.GraphRag

## Контекст

Нужно создать веб-приложение **MyGraphRagV5** (ASP.NET Core, .NET 10) в репозитории `C:\Works_AI\graphrag-dotnet-v5` поверх библиотек `ManagedCode.GraphRag` + `ManagedCode.GraphRag.Postgres` (ProjectReference). Назначение: несколько **rag_projects** (каждый = папка с C#-кодом и markdown-документацией), индексация в knowledge graph (Apache AGE) + векторное хранилище (pgvector), RAG-поиск/чат через LLM. Дизайн — по `DESIGN.md`/`DESIGN.json`/`PRODUCT.md` в корне репо (система «Ember»), тёмная тема по умолчанию.

**Решения пользователя:** проект внутри репо через ProjectReference; TEI-формат API у embedding/rerank контейнеров; UI: Дашборд + Проекты/Индексация + Поиск/Chat + Просмотр графа; тесты unit + интеграционные (Testcontainers).

## Установленные факты о библиотеке (проверено)

- `services.AddGraphRag()` регистрирует пайплайн; `AddPostgresGraphStore(key, opts)` — keyed `IGraphStore` (AGE, raw Npgsql, GraphName на проект → мультитенантность). Регистрация только на старте DI ([ServiceCollectionExtensions.cs](src/ManagedCode.GraphRag.Postgres/ServiceCollectionExtensions.cs)).
- LLM/embeddings — Microsoft.Extensions.AI: keyed `IChatClient` / `IEmbeddingGenerator<string, Embedding<float>>` по model id, fallback на unkeyed. Хост регистрирует сам.
- Вход индексации: `IndexingPipelineRunner.RunAsync(GraphRagConfig, ct)`. Файлы ищутся **regex**-паттерном (`InputConfig.FilePattern`, default `.*\.txt$`) в `Input.Storage.BaseDir`; типы Text/Csv/Json (`.cs`/`.md` грузим как Text). Чанкеры: `TokenTextChunker`, `MarkdownTextChunker`.
- **КРИТИЧНЫЕ ПРОБЕЛЫ (реализует приложение):**
  1. Пайплайн пишет артефакты (`PipelineTableNames`: Documents, TextUnits, Entities, Relationships, Communities, CommunityReports) в `IPipelineStorage` (JSON-файлы в output dir) и **НЕ пишет в IGraphStore и не векторизует** — синхронизация в AGE и pgvector делается хостом после `RunAsync`.
  2. `IVectorStore` (Vectors/IVectorStore.cs) — интерфейс без реализаций → нужен свой **PgVectorStore** (pgvector).
  3. Query/RAG-движка нет (только `*SearchConfig`) → свой **RagQueryService**.
  4. [IndexingPipelineRunner.cs:27](src/ManagedCode.GraphRag/Indexing/IndexingPipelineRunner.cs:27) хардкодит `NoopWorkflowCallbacks.Instance` → мини-правка библиотеки для прогресса в UI.
  5. Reranker-абстракции нет → своя `IReranker`.

## Инфраструктура (дано)

- PostgreSQL (docker, уже существует): `Host=localhost;Port=6000;Database=graphdb;Username=postgres;Password=M@y_Passw0rd` — расширения `vector` И `age` уже установлены. Одна БД и для AGE-графов, и для pgvector, и для EF Core-таблиц приложения.
- LLM: Ollama Cloud, модель `nemotron-3-super:cloud`, endpoint `https://ollama.com/v1` (OpenAI-compatible). **API-ключ — только в user-secrets / env `OLLAMA_API_KEY`, в git не коммитить** (ключ у пользователя есть).
- Embeddings: TEI-контейнер `embedding-api` → `POST http://localhost:14401/embed`. Размерность неизвестна — probe при старте/первой индексации, хранить в проекте.
- Rerank: TEI-контейнер `reranker-api` → `POST http://localhost:18200/rerank`.

---

## Фазы реализации

### Фаза 1 — Каркас и интеграция в solution
- `src/MyGraphRagV5/MyGraphRagV5.csproj`: `Sdk="Microsoft.NET.Sdk.Web"`, net10.0, `<IsPackable>false</IsPackable>`, `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>`, `<UserSecretsId>`, ProjectReference на `ManagedCode.GraphRag` и `ManagedCode.GraphRag.Postgres`.
- `GraphRag.slnx`: добавить проект в папку `/src/` (и тестовый в `/tests/` в фазе 10).
- `Directory.Packages.props` (central package management!): добавить `Microsoft.EntityFrameworkCore` + `.Design` 10.0.x, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.x, `Pgvector` (+ `Pgvector.EntityFrameworkCore` при необходимости), `Microsoft.Extensions.AI.OpenAI` (+ pin `OpenAI` 2.x) — Ollama Cloud через OpenAI-compatible endpoint.
- Проверка: `dotnet build` зелёный.

### Фаза 2 — EF Core 10: модель и миграции
Файлы: `src/MyGraphRagV5/Data/AppDbContext.cs`, `Data/Entities.cs`, `Data/Migrations/` (schema `app`, чтобы не мешать `ag_catalog` и vec-таблицам):
- `rag_projects`: Id (Guid), Name (unique), SourceFolder, GraphName (unique, генерится из имени), VectorCollection, FilePattern (default `.*\.(cs|md)$`), EmbeddingDimension (int?), CreatedAt.
- `indexing_runs`: Id, ProjectId FK, Status (Pending/Running/Succeeded/Failed/Cancelled), StartedAt, CompletedAt?, CurrentWorkflow?, ProgressPercent?, Error?, DocumentCount?.
- `chat_sessions` (Id, ProjectId, Title, CreatedAt) + `chat_messages` (Id, SessionId, Role, Content, SourcesJson jsonb, CreatedAt).
- `Database.MigrateAsync()` при старте. Проверка: миграция применяется к graphdb:6000.

### Фаза 3 — PgVectorStore (реализация `IVectorStore`)
Файл: `src/MyGraphRagV5/Vectors/PgVectorStore.cs` — **raw Npgsql** (`NpgsqlDataSourceBuilder.UseVector()`), не EF (динамические таблицы по коллекциям):
- Таблица на коллекцию `vec_{collection}` (санитизация имени): `id text PK, embedding vector(N), text text, metadata jsonb, updated_at`. `CREATE TABLE IF NOT EXISTS` лениво, N — из первого эмбеддинга; кэш созданных коллекций.
- Индекс HNSW `vector_cosine_ops`.
- `UpsertAsync` → `INSERT … ON CONFLICT DO UPDATE`; `SearchAsync` → `ORDER BY embedding <=> $1 LIMIT k`, score = `1 - cosine_distance`.
- Регистрация singleton `IVectorStore`.

### Фаза 4 — AI-клиенты
Файлы: `src/MyGraphRagV5/Ai/TeiEmbeddingGenerator.cs`, `Ai/IReranker.cs`, `Ai/TeiReranker.cs`, `Ai/AiServiceCollectionExtensions.cs`:
- Chat: `OpenAIClient(apiKey, Endpoint=https://ollama.com/v1).GetChatClient("nemotron-3-super:cloud").AsIChatClient()` — keyed по model id + unkeyed fallback.
- `TeiEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>`: `POST /embed {"inputs":[…]}` → `float[][]`; named HttpClient, BaseAddress из конфига; батчинг по `TextEmbeddingConfig.BatchSize`; startup-probe размерности.
- `IReranker.RerankAsync(query, texts, topN, ct)` → `TeiReranker`: `POST /rerank {"query":…, "texts":[…]}` → `[{index, score}]`.

### Фаза 5 — Правка библиотеки: прогресс-callbacks (минимальный дифф)
- [IndexingPipelineRunner.cs](src/ManagedCode.GraphRag/Indexing/IndexingPipelineRunner.cs): добавить опциональный параметр `RunAsync(GraphRagConfig config, IWorkflowCallbacks? callbacks = null, CancellationToken ct = default)`; строка 27 → `callbacks ?? _services.GetService<IWorkflowCallbacks>() ?? NoopWorkflowCallbacks.Instance`. Не ломает существующие вызовы.
- В приложении: `RunProgressCallbacks : IWorkflowCallbacks` — пишет `ProgressSnapshot`/имя workflow в реестр запусков (фаза 6).

### Фаза 6 — Оркестрация индексации
Файлы: `src/MyGraphRagV5/Indexing/IndexingService.cs`, `Indexing/RunRegistry.cs`, `Indexing/ProjectGraphStoreProvider.cs`, `Indexing/GraphSyncService.cs`, `Indexing/EmbeddingSyncService.cs`:
- `RunRegistry`: `ConcurrentDictionary<Guid, RunHandle>` (CTS + live-прогресс). Один активный запуск на проект. Без hosted-service-очереди — `Task.Run` (ponytail: очередь добавим при необходимости).
- `ProjectGraphStoreProvider`: т.к. keyed-DI фиксируется на старте, а проекты создаются в рантайме — создаёт и кэширует `PostgresGraphStore` напрямую (`PostgresGraphStoreOptions {ConnectionString, GraphName=project.GraphName}` + `AgeConnectionManager`/`AgeClientFactory`), по образцу [ServiceCollectionExtensions.cs](src/ManagedCode.GraphRag.Postgres/ServiceCollectionExtensions.cs).
- `IndexingService.StartRunAsync(projectId)`:
  1. запись в `indexing_runs` → Running;
  2. `GraphRagConfig`: `Input.Storage.BaseDir = SourceFolder`, `FilePattern`, `FileType=Text`, output dir `data/output/{projectId}`, Models/EmbedText.ModelId;
  3. `IndexingPipelineRunner.RunAsync(config, runCallbacks, ct)`;
  4. **GraphSync**: читать артефакты Entities/Relationships из output storage (`ReadTableAsync` + `PipelineTableNames`) → `IGraphStore.UpsertNodesAsync/UpsertRelationshipsAsync` в AGE-граф проекта;
  5. **EmbeddingSync**: TextUnits + CommunityReports → `TeiEmbeddingGenerator` → `PgVectorStore.UpsertAsync(project.VectorCollection, …)` (metadata: id, text, doc title, type=text_unit|community);
  6. финальный статус/ошибка в БД.
- Отмена: `CancelRun(runId)` → CTS.

### Фаза 7 — RagQueryService
Файл: `src/MyGraphRagV5/Query/RagQueryService.cs`:
```csharp
Task<RagAnswer> AskAsync(Guid projectId, string question, CancellationToken ct);
IAsyncEnumerable<string> StreamAnswerAsync(...); // streaming IChatClient
record RagAnswer(string Answer, IReadOnlyList<SourceRef> Sources);
record SourceRef(string Id, string Title, string Snippet, double Score);
```
Поток: embed вопроса → `PgVectorStore.SearchAsync` top-20 → извлечь entity-имена из metadata → графовое расширение через `IGraphStore` проекта (ноды + 1 hop `GetOutgoingRelationshipsAsync`, cap ~30 фактов) → кандидаты (text units + описания связей + community summaries) → `IReranker` top-8 → промпт (system: «отвечай по контексту, цитируй [n]») → `IChatClient` (streaming) → сохранить в `chat_messages`.

### Фаза 8 — Web UI (Razor Pages + HTMX + Tailwind, тема Ember)
- Tailwind v4 **standalone CLI** (без Node) через `Exec`-target в csproj; токены Ember из DESIGN.json как CSS-переменные в `wwwroot/css/ember.css` (`--bg:#2D2D35`, `--panel:#363640`, `--inset:#1E1E26`, `--accent:#FF6B2C`, семантика: Running=info, Succeeded=success, Failed=danger, Cancelled=muted; Inter + JetBrains Mono; плоская elevation, hairline 1px; без neumorphism/glassmorphism).
- **Тёмная тема по умолчанию** (`data-theme="dark"` на `<html>`), переключатель light — в localStorage.
- Страницы: `Pages/Shared/_Layout.cshtml` (сайдбар 260px: Дашборд, Проекты, Поиск, Граф), `Pages/Index.cshtml` (дашборд: проекты, размеры графов, последние запуски, health-панель), `Pages/Projects/*` (CRUD + запуск индексации + история), `Pages/Chat/Index.cshtml`, `Pages/Graph/Index.cshtml` + `Node.cshtml` (таблицы сущностей/связей/сообществ, деталка узла; без визуализации).
- HTMX: формы `hx-post` → partials; прогресс запуска — polling `hx-trigger="every 2s"` на partial из `RunRegistry`; чат — streaming через minimal-API endpoint `text/event-stream` + htmx SSE-extension (fallback: нестриминговый ответ partial'ом).

### Фаза 9 — Конфигурация и health
- `appsettings.json`: `ConnectionStrings:GraphDb`, `Ollama:{Endpoint,Model}`, `Tei:{EmbedUrl,RerankUrl}`; `Ollama:ApiKey` — user-secrets/env.
- Health checks: Npgsql + 3 кастомных `IHealthCheck` (Ollama, TEI embed, TEI rerank); `/healthz` JSON → HTMX-панель на дашборде.

### Фаза 10 — Тесты (`tests/MyGraphRagV5.Tests`, xunit 2.9.3 как в репо)
- Unit: `TeiEmbeddingGenerator`/`TeiReranker` (стаб `HttpMessageHandler`), `RagQueryService` (fake `IVectorStore`/`IGraphStore`/`IChatClient`).
- Интеграционные (Testcontainers, по образцу [GraphRagApplicationFixture.cs](tests/ManagedCode.GraphRag.Tests/GraphRagApplicationFixture.cs)): `PgVectorStore` + smoke EF-миграций. Образ: Dockerfile `FROM apache/age:latest` + установка pgvector (`ImageFromDockerfileBuilder`), тот же трюк `ALTER SYSTEM SET shared_preload_libraries='age'`.

### Фаза 11 — docker-compose (референс)
`docker/docker-compose.yml` + `docker/postgres-age-pgvector.Dockerfile` (apache/age + pgvector, порт 6000:5432) — как справка для воспроизведения окружения; приложение по умолчанию работает с существующим Postgres:6000. Опционально сервисы TEI (embedding 14401, reranker 18200).

## Порядок и верификация

1→2: `dotnet build` + миграции на живой БД. 3→4: unit-тесты. 5: `dotnet test` существующего тестового проекта (регресс). 6: индексация маленькой тестовой папки → проверить AGE-граф (`ag_catalog`) и `vec_*`-таблицу. 7: временный minimal-API endpoint → RAG-ответ. 8–9: запуск приложения, прогон сценария Проект → Индексация → Чат в браузере. 10: `dotnet test`. Финал: полный сценарий на реальной папке с C#-кодом.

## Риски / заметки

- API-ключ Ollama, засвеченный в чате, не коммитить; рекомендовать пользователю перевыпустить.
- Точный формат ответов TEI-контейнеров проверить при первом запуске (контейнеры сейчас не подняты) — адаптеры изолированы в `Ai/`, правка локальна.
- `TreatWarningsAsErrors` наследуется из Directory.Build.props — для web-проекта отключаем.
- Правка библиотеки (фаза 5) — единственная и обратно совместимая.
