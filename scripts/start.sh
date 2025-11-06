#!/usr/bin/env bash
set -euo pipefail

# Secure Box — start script (build + up)
# - Builds images and starts the full docker-compose stack in detached mode.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")"/.. && pwd)"
COMPOSE_FILE="${ROOT_DIR}/docker-compose.yml"

if [[ ! -f "${COMPOSE_FILE}" ]]; then
  echo "[ERROR] docker-compose.yml not found at ${COMPOSE_FILE}" >&2
  exit 1
fi

# Helper: choose docker compose v2 or v1
compose() {
  if docker compose version >/dev/null 2>&1; then
    docker compose -f "${COMPOSE_FILE}" "$@"
  elif command -v docker-compose >/dev/null 2>&1; then
    docker-compose -f "${COMPOSE_FILE}" "$@"
  else
    echo "[ERROR] Docker Compose is not installed (docker compose / docker-compose)." >&2
    exit 1
  fi
}

echo "[Info] Working directory: ${ROOT_DIR}"
cd "${ROOT_DIR}"

# Optional sanity warnings for referenced bind mounts
WARN=0
if [[ ! -e "infrastructure/postgres/init.sql" ]]; then
  echo "[Warn] Missing infrastructure/postgres/init.sql (Postgres init). Container will start without bootstrap SQL." >&2
  WARN=1
fi
if [[ ! -d "infrastructure/nginx/ssl" ]]; then
  echo "[Warn] Missing infrastructure/nginx/ssl (TLS certs for LB). Nginx may fail if config requires certs." >&2
  WARN=1
fi
if [[ ! -d "infrastructure/logstash/pipeline" ]]; then
  echo "[Warn] Missing infrastructure/logstash/pipeline (Logstash pipeline). Logstash may not ingest logs." >&2
  WARN=1
fi

if [[ ! -f .env ]]; then
  echo "[Info] .env not found. Using defaults and inline fallbacks from docker-compose.yml."
  if [[ -f .env.example ]]; then
    echo "[Hint] You can create one via: cp .env.example .env"
  fi
fi

echo "[Step] Pulling base images (third-party)…"
compose pull --ignore-pull-failures || true

echo "[Step] Building service images (api-1, api-2, portal)…"
compose build --pull

echo "[Step] Starting stack in detached mode…"
compose up -d

echo "[OK] Stack is starting. Useful URLs (once healthy):"
echo "  - API via LB:       http://localhost (use /api/* routes; TLS off by default)"
echo "  - Swagger (via LB): http://localhost/api/swagger"
echo "  - Swagger (direct): http://localhost:5002/swagger (api-1), http://localhost:5001/swagger (api-2)"
echo "  - Portal:           http://localhost"
echo "  - Kibana:           http://localhost:5601"
echo "  - RabbitMQ:         http://localhost:15672"
echo "  - Postgres:         localhost:5432"

if [[ "${WARN}" -eq 1 ]]; then
  echo "[Note] One or more optional infra files/dirs are missing; see warnings above."
fi

echo "[Tip] To view logs: docker compose logs -f nginx api-1 api-2 portal"
