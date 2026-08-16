#!/usr/bin/env bash
set -euo pipefail

# Secure Box — local start
# Default: docker compose up -d (no rebuild)
# Optional: ./scripts/start.sh --build

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")"/.. && pwd)"
COMPOSE_FILE="${ROOT_DIR}/docker-compose.yml"

if [[ ! -f "${COMPOSE_FILE}" ]]; then
  echo "[ERROR] docker-compose.yml not found at ${COMPOSE_FILE}" >&2
  exit 1
fi

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

is_service_running() {
  local service_name=$1
  local container_name="securebox-${service_name}"
  docker ps --format '{{.Names}}' | grep -q "^${container_name}$"
}

INFRA_SERVICES=("postgres" "redis")
APP_SERVICES=("api-1" "api-2" "portal" "nginx")
DO_BUILD=0

if [[ ${1-} == "--build" || ${1-} == "-b" ]]; then
  DO_BUILD=1
fi

echo "[Info] Working directory: ${ROOT_DIR}"
cd "${ROOT_DIR}"

if [[ ! -e "infrastructure/postgres/init.sql" ]]; then
  echo "[Warn] Missing infrastructure/postgres/init.sql (Postgres init)." >&2
fi

SSL_DIR="${ROOT_DIR}/infrastructure/nginx/ssl"
mkdir -p "${SSL_DIR}"
if [[ ! -f "${SSL_DIR}/cert.pem" || ! -f "${SSL_DIR}/key.pem" ]]; then
  echo "[Step] Generating local TLS certificate for nginx..."
  openssl req -x509 -nodes -newkey rsa:2048 \
    -keyout "${SSL_DIR}/key.pem" \
    -out "${SSL_DIR}/cert.pem" \
    -days 825 \
    -subj "/CN=localhost"
fi

if [[ ! -f .env ]]; then
  echo "[ERROR] .env is missing. Run: cp .env.example .env  then fill every value." >&2
  exit 1
fi

set -a
# shellcheck disable=SC1091
source .env
set +a

for required in POSTGRES_PASSWORD REDIS_PASSWORD JWT_SECRET_KEY ENCRYPTION_KEK ADMIN_PASSWORD; do
  if [[ -z "${!required:-}" ]]; then
    echo "[ERROR] ${required} is empty in .env" >&2
    exit 1
  fi
done

echo ""
echo "=========================================="
echo "STEP 1: Infrastructure"
echo "=========================================="

INFRA_TO_START=()
for service in "${INFRA_SERVICES[@]}"; do
  if is_service_running "$service"; then
    echo "[OK] $service is already running"
  else
    echo "[Info] $service is not running, will start"
    INFRA_TO_START+=("$service")
  fi
done

if [[ ${#INFRA_TO_START[@]} -gt 0 ]]; then
  echo "[Step] Starting: ${INFRA_TO_START[*]}"
  compose up -d "${INFRA_TO_START[@]}"
  echo "[Wait] Waiting for infrastructure to be healthy..."
  for service in "${INFRA_TO_START[@]}"; do
    timeout=60
    counter=0
    until compose ps "$service" | grep -q "healthy" || [[ $counter -eq $timeout ]]; do
      sleep 1
      counter=$((counter + 1))
    done
    if [[ $counter -eq $timeout ]]; then
      echo "[Warn] $service did not become healthy in ${timeout}s"
    else
      echo "  - $service is healthy"
    fi
  done
else
  echo "[OK] All infrastructure services are already running"
fi

if [[ ${DO_BUILD} -eq 1 ]]; then
  echo ""
  echo "=========================================="
  echo "STEP 2: Building application images"
  echo "=========================================="
  compose build api-1 api-2 portal
else
  echo ""
  echo "=========================================="
  echo "STEP 2: Skipping build (use --build to rebuild)"
  echo "=========================================="
fi

echo ""
echo "=========================================="
echo "STEP 3: Starting application services"
echo "=========================================="

compose up -d "${APP_SERVICES[@]}"

echo ""
echo "✅ Stack is up"
echo ""
echo "  Portal:     http://localhost"
echo "  API:        http://localhost/api"
echo "  API (direct): http://127.0.0.1:5002"
echo "  Health:     http://127.0.0.1:5002/health/live"
echo ""
echo "  Admin: admin / value of ADMIN_PASSWORD (change password + enable MFA on first login)"
echo ""
echo "  Status:  docker compose ps"
echo "  Logs:    docker compose logs -f api-1 portal nginx"
echo "  Stop:    ./scripts/stop.sh"
echo "  Rebuild: ./scripts/start.sh --build"
echo ""
compose ps
