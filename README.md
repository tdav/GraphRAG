# GraphRAG для .NET

[![NuGet](https://img.shields.io/nuget/v/ManagedCode.GraphRag.svg)](https://www.nuget.org/packages/ManagedCode.GraphRag/)
[![NuGet Neo4j](https://img.shields.io/nuget/v/ManagedCode.GraphRag.Neo4j.svg?label=Neo4j)](https://www.nuget.org/packages/ManagedCode.GraphRag.Neo4j/)
[![NuGet Postgres](https://img.shields.io/nuget/v/ManagedCode.GraphRag.Postgres.svg?label=Postgres)](https://www.nuget.org/packages/ManagedCode.GraphRag.Postgres/)
[![NuGet CosmosDb](https://img.shields.io/nuget/v/ManagedCode.GraphRag.CosmosDb.svg?label=CosmosDb)](https://www.nuget.org/packages/ManagedCode.GraphRag.CosmosDb/)
[![NuGet JanusGraph](https://img.shields.io/nuget/v/ManagedCode.GraphRag.JanusGraph.svg?label=JanusGraph)](https://www.nuget.org/packages/ManagedCode.GraphRag.JanusGraph/)
[![Build Status](https://github.com/managedcode/graphrag/actions/workflows/ci.yml/badge.svg)](https://github.com/managedcode/graphrag/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

GraphRAG для .NET — это порт эталонной реализации GraphRAG от Microsoft, созданный с нуля для современного стека .NET 10. Порт сохраняет паритет с исходными Python-пайплайнами и при этом использует нативные идиомы .NET: dependency injection, абстракции логирования, асинхронный I/O и строго типизированную конфигурацию.

> ℹ️ Исходный Python-код доступен в [`submodules/graphrag-python`](submodules/graphrag-python) для параллельного сравнения. Считайте его read-only, если задача явно не требует изменений в submodule.

---

## Основные возможности

- **Полный конвейер индексации.** Все стандартные этапы GraphRAG — загрузка документов, чанкинг, извлечение графа, построение сообществ и суммаризация — реализованы как отдельные workflow, которые регистрируются одним вызовом `AddGraphRag(...)`.
- **Эвристики ingestion и сопровождения.** Встроенные перекрывающиеся окна чанков, семантическая дедупликация, связывание orphan-узлов, улучшение/валидация связей и обрезка по токен-бюджету помогают поддерживать чистоту графа без дополнительных сервисов.
- **Быстрое выделение сообществ (label propagation).** Настраиваемый детектор fast label propagation (с fallback на connected components) повторяет поведение демо GraphRag.Net прямо внутри пайплайна.
- **Подключаемые graph store.** Готовые адаптеры для Azure Cosmos DB, Neo4j и Apache AGE/PostgreSQL соответствуют `IGraphStore`, поэтому бэкенд можно менять без правок workflow.
- **Оркестрация промптов.** Шаблоны промптов каскадируются по уровням manual, auto-tuned и default через keyed-клиенты [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/overview) для chat и embedding моделей.
- **Детерминированные интеграционные тесты.** Testcontainers поднимает реальные базы, а stub embeddings обеспечивают стабильное покрытие эвристик, чтобы CI валидировал полный pipeline.

---

## Структура репозитория

```
graphrag/
├── GraphRag.slnx                          # Solution: runtime + test projects
├── Directory.Build.props / Directory.Packages.props
├── src/
│   ├── ManagedCode.GraphRag               # Оркестрация core-пайплайна и абстракции
│   ├── ManagedCode.GraphRag.CosmosDb      # Адаптер графа для Azure Cosmos DB
│   ├── ManagedCode.GraphRag.Neo4j         # Адаптер Neo4j и интеграция Bolt
│   └── ManagedCode.GraphRag.Postgres      # Адаптер Apache AGE/PostgreSQL
├── tests/
│   └── ManagedCode.GraphRag.Tests
│       ├── Integration/                   # Сценарии с реальными контейнерами
│       └── … unit-level suites
└── submodules/
    └── graphrag-python                    # Исходная Python-реализация (read-only)
```

---

## Предварительные требования

| Требование | Примечание |
|-------------|-------|
| [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) | Решение таргетит `net10.0`. В CI используйте скрипт [`dotnet-install.sh`](dotnet-install.sh) из репозитория. |
| Docker Desktop / совместимый runtime | Нужен для интеграционных тестов на Testcontainers (Neo4j и Apache AGE/PostgreSQL). |
| (Опционально) Azure Cosmos DB Emulator | Установите `COSMOS_EMULATOR_CONNECTION_STRING`, чтобы включить тесты Cosmos. |

---

## Быстрый старт

1. **Клонируйте репозиторий и инициализируйте submodule**
   ```bash
   git clone https://github.com/<your-org>/graphrag.git
   cd graphrag
   git submodule update --init --recursive
   ```

2. **Установите .NET 10 при необходимости**
   ```bash
   ./dotnet-install.sh --version 10.0.100
   export PATH="$HOME/.dotnet:$PATH"
   ```

3. **Restore и build (всегда собирайте перед тестами)**
   ```bash
   dotnet build GraphRag.slnx
   ```

4. **Запустите полный набор тестов**
   ```bash
   dotnet test GraphRag.slnx --logger "console;verbosity=minimal"
   ```
   Команда восстанавливает пакеты, поднимает контейнеры Neo4j и Apache AGE/PostgreSQL через Testcontainers, выполняет unit + integration тесты и автоматически всё завершает.

5. **Запустите конкретный сценарий (опционально)**
   ```bash
   dotnet test tests/ManagedCode.GraphRag.Tests/ManagedCode.GraphRag.Tests.csproj \
       --filter "FullyQualifiedName~HeuristicMaintenanceIntegrationTests" \
       --logger "console;verbosity=normal"
   ```

6. **Форматируйте код перед коммитом**
   ```bash
   dotnet format GraphRag.slnx
   ```

---

## Использование GraphRAG в вашем приложении

Зарегистрируйте сервисы GraphRAG и предоставьте keyed-клиенты Microsoft.Extensions.AI для каждой ссылки на модель:

```csharp
using Azure;
using Azure.AI.OpenAI;
using GraphRag;
using GraphRag.Config;
using Microsoft.Extensions.AI;

var openAi = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));

builder.Services.AddKeyedSingleton<IChatClient>(
    "chat_model",
    _ => openAi.GetChatClient(chatDeployment));

builder.Services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding>>(
    "embedding_model",
    _ => openAi.GetEmbeddingClient(embeddingDeployment));

builder.Services.AddGraphRag();
```

---

## Кэш пайплайна и расширяемость

Каждый workflow в pipeline использует один и тот же `IPipelineCache` через `PipelineRunContext`. Регистрация DI по умолчанию подключает `MemoryPipelineCache`, что позволяет повторно использовать дорогие промежуточные артефакты (ответы LLM, расширения чанков, запросы к графу) без повторных вычислений. Вы можете заменить реализацию, зарегистрировав свой `IPipelineCache` до вызова `AddGraphRag()` — например, чтобы сохранять записи кэша или собирать диагностику.

- **Child scopes.** `MemoryPipelineCache.CreateChild("stage")` добавляет префикс stage к ключам, чтобы изолировать многошаговые workflow.
- **Debug payloads.** Записи могут содержать дополнительный debug payload; очистка кэша удаляет и значение, и связанные trace-метаданные.
- **Custom lifetimes.** Зарегистрируйте scoped cache, если хотите ограничить его временем жизни одного HTTP-запроса вместо singleton по умолчанию.

---

## Эвристики ingestion и сопровождения

Порт .NET включает поведения ingestion из GraphRag.Net прямо в indexing pipeline:

- **Перекрывающиеся окна чанков** формируют связные контекстные фрагменты, устойчивые к обрезке сообществ.
- **Семантическая дедупликация** отбрасывает дубликаты text unit, сравнивая косинусную близость эмбеддингов с настраиваемым порогом.
- **Ограничение token budget** автоматически соблюдает глобальные и per-community лимиты токенов при суммаризации.
- **Связывание orphan-узлов** повторно подключает изолированные сущности через высокодоверенные связи до финализации.
- **Улучшение и валидация связей** согласует вывод LLM с существующими ребрами, чтобы избегать дублей и повышать веса.

Настройка эвристик через `GraphRagConfig.Heuristics` (например, в `appsettings.json`):

```json
{
  "GraphRag": {
    "Models": [ "chat_model", "embedding_model" ],
    "EmbedText": {
      "ModelId": "embedding_model"
    },
    "Heuristics": {
      "MinimumChunkOverlap": 128,
      "EnableSemanticDeduplication": true,
      "SemanticDeduplicationThreshold": 0.92,
      "MaxTokensPerTextUnit": 1200,
      "MaxDocumentTokenBudget": 6000,
      "MaxTextUnitsPerRelationship": 6,
      "LinkOrphanEntities": true,
      "OrphanLinkMinimumOverlap": 0.25,
      "OrphanLinkWeight": 0.35,
      "EnhanceRelationships": true,
      "RelationshipConfidenceFloor": 0.35
    }
  }
}
```

См. [`docs/indexing-and-query.md`](docs/indexing-and-query.md) для полного списка параметров и соответствия оригинальному исследовательскому pipeline.

---

## Паритет конфигурации

Поверхность конфигурации .NET теперь соответствует исходному Python CLI. `GraphRagConfig` предоставляет те же секции, что и `graphrag.config`, включая cache providers, NLP-извлечение графа, извлечение claims, prune графа и все режимы поиска (local/global/DRIFT/basic). Это упрощает перенос существующих Python-конфигов без пересмотра всех параметров:

```json
{
  "GraphRag": {
    "Cache": { "Type": "File", "BaseDir": "cache" },
    "ExtractGraphNlp": {
      "ConcurrentRequests": 25,
      "TextAnalyzer": { "ModelName": "en_core_web_md", "IncludeNamedEntities": true }
    },
    "ExtractClaims": {
      "Enabled": true,
      "ModelId": "chat_model",
      "Prompt": "prompts/claims.txt",
      "MaxGleanings": 2
    },
    "PruneGraph": { "MinNodeFrequency": 2, "MinEdgeWeightPercentile": 40 },
    "EmbedGraph": { "Enabled": false, "Dimensions": 1536 },
    "LocalSearch": { "ChatModelId": "chat_model", "EmbeddingModelId": "embedding_model" },
    "GlobalSearch": { "MapPrompt": "prompts/global_map.txt", "ReducePrompt": "prompts/global_reduce.txt" },
    "DriftSearch": { "Prompt": "prompts/drift.txt", "Concurrency": 32 },
    "BasicSearch": { "K": 10 }
  }
}
```

`ClaimExtractionConfig.GetResolvedStrategy` повторяет поведение Python: загружает prompt-файлы из настроенного корневого каталога (и выбрасывает исключение, если файл отсутствует), при этом оставляя возможность полностью переопределить блок `Strategy`.

---

## Выделение сообществ и аналитика графа

Создание сообществ по умолчанию использует алгоритм fast label propagation. Настройте кластеризацию напрямую через конфигурацию:

```json
{
  "GraphRag": {
    "Models": [ "chat_model", "embedding_model" ],
    "ClusterGraph": {
      "Algorithm": "FastLabelPropagation",
      "MaxIterations": 40,
      "MaxClusterSize": 25,
      "UseLargestConnectedComponent": true,
      "Seed": 3735928559
    }
  }
}
```

Если граф разрежён, pipeline переключается на connected components, чтобы каждый узел попал в сообщество. Интеграционные тесты эвристик (`Integration/HeuristicMaintenanceIntegrationTests.cs`) покрывают как путь label propagation, так и fallback на connected components.

---

## Стратегия интеграционного тестирования

- **Только реальные сервисы.** Все graph-операции выполняются против контейнеризованных Neo4j и Apache AGE/PostgreSQL, поднятых через Testcontainers.
- **Увеличенное окно запуска.** Интеграционный fixture повышает `TestcontainersSettings.WaitStrategyTimeout` до 30 минут (переопределяется через `GRAPH_RAG_CONTAINER_TIMEOUT_MINUTES`), чтобы первые Docker pull для Cosmos, Janus или AGE успевали завершиться до срабатывания readiness checks.
- **Cosmos-тесты включаются по флагу.** Эмулятор Cosmos DB стартует только при `GRAPH_RAG_ENABLE_COSMOS=true`; иначе keyed-сервисы Cosmos пропускаются для ускорения CI.
- **Детерминированные эвристики.** `StubEmbeddingGenerator` обеспечивает стабильные эмбеддинги, чтобы проверки semantic dedup и token budget оставались надёжными.
- **Проверка между хранилищами.** Общие integration fixture подтверждают, что workflow успешно работает с каждым адаптером (Cosmos-сценарии активируются при наличии connection string эмулятора).
- **Приоритет промптов.** Тесты подтверждают, что ручные overrides имеют приоритет над auto-tuned вариантами и корректно каскадируют к default templates.
- **Покрытие телеметрии.** Runtime-тесты проверяют callbacks pipeline и статистику выполнения, чтобы пользовательская инструментализация не ломалась.

Чтобы запустить только container-backed suite:

```bash
dotnet test tests/ManagedCode.GraphRag.Tests/ManagedCode.GraphRag.Tests.csproj \
    --filter "Category=Integration" \
    --logger "console;verbosity=normal"
```

---

## Настройка graph store

GraphRAG поставляется с адаптерами для Apache AGE/PostgreSQL, Neo4j и Azure Cosmos DB. Каждый адаптер регистрирует keyed-сервисы, поэтому конкретное хранилище можно получить через `GetRequiredKeyedService<IGraphStore>("postgres")`, а первое зарегистрированное автоматически становится unkeyed default (`GetRequiredService<IGraphStore>()`). Это соответствует подходу EF Core «один default context» и устраняет необходимость в дополнительных флагах `MakeDefault`.

### Настройка Apache AGE / PostgreSQL

GraphRAG включает полноценный адаптер Apache AGE (`ManagedCode.GraphRag.Postgres`). AGE включается поверх PostgreSQL, поэтому достаточно стандартного экземпляра Postgres с установленным расширением AGE.

1. **Запустите экземпляр Postgres с AGE.** Интеграционные тесты используют официальный контейнер, вы можете сделать то же локально:
   ```bash
   docker run --rm \
     -e POSTGRES_USER=postgres \
     -e POSTGRES_PASSWORD=postgres \
     -e POSTGRES_DB=graphrag \
     -p 5432:5432 \
     apache/age:latest
   ```
2. **Задайте connection string.** `AgeConnectionManager` принимает стандартную строку в стиле Npgsql (например, `Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=graphrag`). Менеджер автоматически выполняет `CREATE EXTENSION IF NOT EXISTS age;`, `LOAD 'age';` и `SET search_path = ag_catalog, "$user", public;` перед любым запросом.
3. **Настройте store.** Можно bind'ить `PostgresGraphStoreOptions` в коде или использовать конфигурацию. Ниже показан JSON-формат (переменные окружения следуют той же иерархии, например `GraphRag__GraphStores__postgres__ConnectionString`):
   ```json
   {
     "GraphRag": {
       "GraphStores": {
         "postgres": {
           "ConnectionString": "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=graphrag",
          "GraphName": "graphrag"
         }
       }
     }
   }
   ```
4. **Зарегистрируйте через DI.** `services.AddPostgresGraphStore("postgres", configure: ...)` подключает `IAgeConnectionManager`, `IAgeClientFactory`, `PostgresGraphStore`, `IGraphStore` и `PostgresExplainService`. Настройки пула берутся из обычных параметров Npgsql (задавайте `Max Pool Size`, `Timeout` и т.д. в connection string). Первая регистрация становится default unkeyed `IGraphStore`; дополнительные stores остаются только keyed.

   ```csharp
   var services = new ServiceCollection()
       .AddLogging()
       .AddPostgresGraphStore("postgres", options =>
       {
           options.ConnectionString = postgresConnectionString;
           options.GraphName = "graphrag";
       });

   await using var provider = services.BuildServiceProvider();

   // Regular graph operations
   var graphStore = provider.GetRequiredService<IGraphStore>();

   // Scoped operations reuse a single AGE/Postgres connection for the lifetime of the scope
   var ageClientFactory = provider.GetRequiredKeyedService<IAgeClientFactory>("postgres");
   await using (await ageClientFactory.CreateScopeAsync())
   {
       await graphStore.UpsertNodeAsync("node-1", "Example", new Dictionary<string, object?> { ["name"] = "Scoped" });
       await graphStore.UpsertNodeAsync("node-2", "Example", new Dictionary<string, object?> { ["name"] = "Connection" });
   }

   // Bulk helpers batch large workloads while keeping the scoped connection alive
   await graphStore.UpsertNodesAsync(new[]
   {
       new GraphNodeUpsert("bulk-1", "Example", new Dictionary<string, object?> { ["name"] = "Bulk" }),
       new GraphNodeUpsert("bulk-2", "Example", new Dictionary<string, object?> { ["name"] = "Write" })
   });

   await graphStore.UpsertRelationshipsAsync(new[]
   {
       new GraphRelationshipUpsert("bulk-1", "bulk-2", "RELATES_TO", new Dictionary<string, object?>())
   });
   ```

   `AgeConnectionManager` автоматически повторяет попытки при временных ошибках `53300: too many clients` (до трёх попыток с экспоненциальной задержкой), поэтому scope может дождаться свободного слота вместо немедленного сбоя. При Dispose scope базовый `IAgeClientScope`, созданный `IAgeClientFactory`, возвращает соединение в пул, сохраняя предсказуемую конкуррентность даже при высоком fan-out.

   Нужно настроить пул соединений или другие параметры Npgsql? Используйте `options.ConfigureConnectionStringBuilder` / `ConfigureDataSourceBuilder` при регистрации store:

   ```csharp
   builder.Services.AddPostgresGraphStore("postgres", options =>
   {
       options.ConnectionString = postgresConnectionString;
       options.ConfigureConnectionStringBuilder = builder =>
       {
           builder.MaxPoolSize = 80;
           builder.MinPoolSize = 40;
       };
       options.ConfigureDataSourceBuilder = ds =>
       {
           ds.EnableArrayNullabilityMode();
       };
   });
   ```

### Настройка Neo4j

Поддержка Neo4j находится в `ManagedCode.GraphRag.Neo4j` и использует официальный Bolt-драйвер:

1. **Запустите Neo4j локально (опционально).**
   ```bash
   docker run --rm \
     -e NEO4J_AUTH=neo4j/test1234 \
     -e NEO4J_ACCEPT_LICENSE_AGREEMENT=yes \
     -p 7687:7687 -p 7474:7474 \
     neo4j:5.23.0-community
   ```
2. **Зарегистрируйте store.**
   ```csharp
   builder.Services.AddNeo4jGraphStore("neo4j", options =>
   {
       options.Uri = "bolt://localhost:7687";
       options.Username = "neo4j";
       options.Password = "test1234";
   });
   ```
   Первая регистрация Neo4j автоматически удовлетворяет `IGraphStore`; для явного доступа используйте `GetRequiredKeyedService<IGraphStore>("neo4j")`.

   Также можно переопределить auth token и конфигурацию драйвера:

   ```csharp
builder.Services.AddNeo4jGraphStore("neo4j", options =>
{
    options.Uri = "neo4j+s://example.databases.neo4j.io";
    options.AuthTokenFactory = _ => AuthTokens.Basic("user", "pass");
    options.ConfigureDriver = config => config.WithMaxConnectionPoolSize(50);
    // Или полностью подменить и предоставить свой драйвер:
    options.DriverFactory = opts => GraphDatabase.Driver(opts.Uri, AuthTokens.None);
});
```

### Настройка JanusGraph

Поддержка JanusGraph (`ManagedCode.GraphRag.JanusGraph`) использует Gremlin.Net и теперь запускается автоматически в интеграционном fixture. Регистрация выполняется так же, как и для остальных store:

```csharp
builder.Services.AddJanusGraphStore("janus", options =>
{
    options.Host = "localhost";
    options.Port = 8182;
    options.ConnectionPoolSize = 16; // optional
    options.MaxInProcessPerConnection = 32; // optional
    options.ConfigureConnectionPool = pool =>
    {
        pool.MaxInProcessPerConnection = Math.Max(pool.MaxInProcessPerConnection, 8);
    };
});
```

По умолчанию адаптер использует пул из 32 соединений и 64 in-flight запросов на соединение, но эти значения можно переопределить (или изменить `ConnectionPoolSettings` напрямую) через новые properties, показанные выше.

### Настройка Azure Cosmos DB

Адаптер Cosmos (`ManagedCode.GraphRag.CosmosDb`) ориентирован на SQL API и работает как с эмулятором, так и с боевыми аккаунтами:

1. **Укажите connection string.** Задайте `COSMOS_EMULATOR_CONNECTION_STRING` или настройте options вручную.
2. **Зарегистрируйте store.**
   ```csharp
   builder.Services.AddCosmosGraphStore("cosmos", options =>
   {
       options.ConnectionString = cosmosConnectionString;
       options.DatabaseId = "GraphRagIntegration";
       options.NodesContainerId = "nodes";
       options.EdgesContainerId = "edges";
       options.ConfigureClientOptions = clientOptions =>
       {
           clientOptions.GatewayModeMaxConnectionLimit = 100;
       };
       options.ConfigureSerializer = serializer => serializer.PropertyNamingPolicy = null;
   });
   ```
   Как и в других адаптерах, первый Cosmos store становится default unkeyed. Если у вас уже есть `CosmosClient`, задайте `options.ClientFactory`, чтобы он его возвращал, и GraphRAG переиспользует этот экземпляр.

> **Подсказка:** `IGraphStore` теперь предоставляет полный набор методов инспекции и изменения графа (`GetNodesAsync`, `GetRelationshipsAsync`, `DeleteNodesAsync`, `DeleteRelationshipsAsync`) в дополнение к целевым API (`InitializeAsync`, `Upsert*`, `GetOutgoingRelationshipsAsync`). Эти методы используют те же AGE-базовые примитивы, поэтому граф можно инспектировать, очищать или экспортировать без перехода на конкретные реализации.

> **Пагинация:** `GetNodesAsync` и `GetRelationshipsAsync` принимают необязательный объект `GraphTraversalOptions` (`new GraphTraversalOptions { Skip = 100, Take = 50 }`), если нужно постранично обходить очень большие графы. По умолчанию методы стримят всё по одной записи, без материализации всего графа в памяти.

---

## Благодарности

- **pg-age** ([Allison-E/pg-age](https://github.com/Allison-E/pg-age)) — мы вендорим эту библиотеку-клиент Apache AGE (см. `src/ManagedCode.GraphRag.Postgres/ApacheAge`), чтобы GraphRAG для .NET мог опираться на проверенный коннектор. Большое спасибо Allison и контрибьюторам за доступность AGE поверх PostgreSQL.

---

## Дополнительная документация и диаграммы

- [`docs/indexing-and-query.md`](docs/indexing-and-query.md) объясняет, как каждый workflow соотносится с исследовательскими диаграммами GraphRAG (основной поток данных, оркестрации query, стратегии prompt tuning), опубликованными на [microsoft.github.io/graphrag](https://microsoft.github.io/graphrag/).
- [`docs/dotnet-port-plan.md`](docs/dotnet-port-plan.md) описывает стратегию миграции с Python на .NET и ссылается на канонические архитектурные диаграммы, использованные при портировании.
- Upstream-документация содержит самые свежие диаграммы для indexing, query и data schema. При представлении системы используйте именно их — они соответствуют реализованному здесь pipeline.

---

## Локальное тестирование Cosmos

1. Установите и запустите [Azure Cosmos DB Emulator](https://learn.microsoft.com/azure/cosmos-db/local-emulator).
2. Экспортируйте connection string:
   ```bash
   export COSMOS_EMULATOR_CONNECTION_STRING="AccountEndpoint=https://localhost:8081/;AccountKey=..."
   ```
3. Запустите `dotnet test`; сценарии Cosmos инициализируют эмулятор и проверят поведение хранилища.

---

## Советы по разработке

- **Структура solution.** Откройте `GraphRag.slnx` в вашей IDE, чтобы увидеть весь workspace.
- **Форматирование и анализаторы.** Перед коммитом запускайте `dotnet format GraphRag.slnx`.
- **Соглашения по коду.** Включены nullable reference types и implicit usings; следите за корректными аннотациями и добавляйте суффикс `Async` к асинхронным методам.
- **Расширение graph-адаптеров.** Реализуйте `IGraphStore` и регистрируйте свой сервис через DI при добавлении новых storage back-end.

---

## Вклад в проект

1. Сделайте fork репозитория и создайте feature-ветку от `main`.
2. Внесите изменения, убедившись, что `dotnet build GraphRag.slnx` проходит до запуска тестов.
3. Выполните `dotnet test GraphRag.slnx` (при запущенном Docker) и `dotnet format GraphRag.slnx` перед созданием pull request.
4. Включите вывод тестов в описание PR и укажите ссылки на связанные issue.

Подробное руководство — в [`CONTRIBUTING.md`](CONTRIBUTING.md).

---

## Лицензия и авторские права

- Распространяется по лицензии [MIT License](LICENSE).
- GraphRAG © Microsoft. Этот репозиторий переосмысливает пайплайны для экосистемы .NET, сохраняя соответствие официальной документации и диаграммам.

Есть вопросы или обратная связь? Откройте issue или начните discussion — мы активно развиваем .NET-порт и приветствуем вклад! 🚀
