# Postgres image that has BOTH Apache AGE (graph, already built into the base image) and
# pgvector (vector search) available via CREATE EXTENSION. apache/age:latest ships its own
# pgdg apt repo already configured for its exact Postgres major version (PG_MAJOR env var,
# e.g. 18 as of writing) - reuse that repo instead of hardcoding a version, so this keeps
# working if the base image bumps its Postgres major.
FROM apache/age:latest

# Prefer the prebuilt pgdg package (fast, no compiler needed). Fall back to building
# pgvector from source (pinned tag) if apt doesn't have a package for this PG major yet.
RUN set -eux; \
    apt-get update; \
    if apt-cache show "postgresql-${PG_MAJOR}-pgvector" >/dev/null 2>&1; then \
        apt-get install -y --no-install-recommends "postgresql-${PG_MAJOR}-pgvector"; \
    else \
        apt-get install -y --no-install-recommends git build-essential "postgresql-server-dev-${PG_MAJOR}"; \
        git clone --branch v0.8.0 --depth 1 https://github.com/pgvector/pgvector.git /tmp/pgvector; \
        make -C /tmp/pgvector; \
        make -C /tmp/pgvector install; \
        rm -rf /tmp/pgvector; \
        apt-get purge -y --auto-remove git build-essential "postgresql-server-dev-${PG_MAJOR}"; \
    fi; \
    rm -rf /var/lib/apt/lists/*
