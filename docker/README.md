# Docker reference environment

This `docker/` folder is a **reference environment**, not a runtime dependency. The
MyGraphRagV5 app is configured (see `src/MyGraphRagV5/appsettings.json`) to talk to an
operator-managed Postgres already listening on `localhost:6000`. Nothing in the app,
library, or test projects reads from or depends on this compose file - it exists so the
`localhost:6000` environment can be reproduced (or inspected) from scratch, e.g. on a new
machine.

## What's here

- `postgres-age-pgvector.Dockerfile` - `apache/age:latest` (Apache AGE graph extension,
  prebuilt) plus `pgvector` installed for that image's Postgres major version. Mirrors
  `tests/MyGraphRagV5.Tests/age-pgvector.Dockerfile`, which the Testcontainers integration
  tests use for the same purpose - keep the two in sync if you change one.
- `docker-compose.yml` - a `postgres` service built from that Dockerfile, plus optional
  `tei-embed` / `tei-rerank` services behind the `tei` profile.

## Running it

```
docker compose -f docker/docker-compose.yml up -d
```

This builds and starts Postgres on `localhost:6000` with database `graphdb`, user
`postgres`, password `M@y_Passw0rd` (matches `ConnectionStrings:GraphDb` in
`appsettings.json`), and Apache AGE preloaded via
`shared_preload_libraries=age` (set on the `postgres` command line, since that setting is
postmaster-context and cannot be applied by a reload - it has to be present at server
start).

The `vector` and `age` (`ag_catalog`) extensions are **not** created by this compose file
or the Dockerfile - they're created automatically by the app: `vector` by the EF Core
`InitialCreate` migration (`db.Database.MigrateAsync()` on startup), and `age` by
`AgeConnectionManager` in `ManagedCode.GraphRag.Postgres` on first graph use. The image
just needs to make both extensions available to `CREATE EXTENSION`.

Stop and remove with `docker compose -f docker/docker-compose.yml down` (add `-v` to also
drop the named data volume).

## Optional TEI services

`tei-embed` (port `14401`) and `tei-rerank` (port `18200`) match the app's
`Tei:EmbedUrl` / `Tei:RerankUrl` settings. They're gated behind the `tei` compose profile
so a plain `docker compose up` doesn't pull/start them:

```
docker compose -f docker/docker-compose.yml --profile tei up -d
```

A TEI embedding container may already be running elsewhere in your setup on a different
port (e.g. `42080`) - if so, don't start `tei-embed` here; just point `Tei:EmbedUrl` in
`appsettings.json` (or `appsettings.Development.json`) at that instance instead. Swap the
`--model-id` args in `docker-compose.yml` for whichever embedding/reranker models you
actually want to serve.

## Ollama API key

The Ollama Cloud API key is a secret and must never be committed to this repo or placed
in any file under `docker/`. Supply it one of two ways:

```
dotnet user-secrets set "Ollama:ApiKey" "<key>" --project src/MyGraphRagV5
```

or via the `OLLAMA_API_KEY` environment variable (the app bridges it into configuration
at startup). Neither `docker-compose.yml` nor the Dockerfile reference or require this
key - Ollama Cloud is called directly by the app over HTTPS, not through this compose
stack.
