#!/usr/bin/env bash
set -euo pipefail

# Secure Box — start script (down + build + up)
# - Stops application services (api, portal, nginx)
# - Builds images and starts the full docker-compose stack in detached mode
# - Infrastructure services (postgres, redis, mongodb, rabbitmq, elk) are only started if not running

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

# Helper: check if a service is running
is_service_running() {
  local service_name=$1
  local container_name="securebox-${service_name}"
  
  if docker ps --format '{{.Names}}' | grep -q "^${container_name}$"; then
    return 0  # Running
  else
    return 1  # Not running
  fi
}

# Infrastructure services (don't restart if already running)
INFRA_SERVICES=("postgres" "redis" "mongodb" "rabbitmq" "elasticsearch" "logstash" "kibana")

# Application services (always restart)
APP_SERVICES=("api-1" "api-2" "portal" "nginx")

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

# ==================================================
# STEP 1: DOWN - Stop application services
# ==================================================
echo ""
echo "=========================================="
echo "STEP 1: Stopping application services"
echo "=========================================="

APP_RUNNING=()
for service in "${APP_SERVICES[@]}"; do
  if is_service_running "$service"; then
    APP_RUNNING+=("$service")
  fi
done

if [[ ${#APP_RUNNING[@]} -gt 0 ]]; then
  echo "[Info] Stopping: ${APP_RUNNING[*]}"
  compose stop "${APP_RUNNING[@]}"
  compose rm -f "${APP_RUNNING[@]}"
  echo "[OK] Application services stopped"
else
  echo "[Info] No application services running, skipping stop"
fi

# ==================================================
# STEP 2: Check infrastructure services
# ==================================================
echo ""
echo "=========================================="
echo "STEP 2: Checking infrastructure services"
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

# Start infrastructure services if needed
if [[ ${#INFRA_TO_START[@]} -gt 0 ]]; then
  echo "[Step] Starting infrastructure services: ${INFRA_TO_START[*]}"
  compose up -d "${INFRA_TO_START[@]}"
  
  # Wait for critical services to be healthy
  echo "[Wait] Waiting for infrastructure services to be healthy..."
  sleep 5
  
  for service in "${INFRA_TO_START[@]}"; do
    if [[ "$service" == "postgres" ]] || [[ "$service" == "redis" ]] || [[ "$service" == "mongodb" ]] || [[ "$service" == "rabbitmq" ]]; then
      echo "  - Waiting for $service..."
      timeout=60
      counter=0
      until compose ps "$service" | grep -q "healthy" || [[ $counter -eq $timeout ]]; do
        sleep 1
        counter=$((counter + 1))
      done
      if [[ $counter -eq $timeout ]]; then
        echo "[Warn] $service did not become healthy in ${timeout}s, continuing anyway..."
      else
        echo "  - $service is healthy"
      fi
    fi
  done
else
  echo "[OK] All infrastructure services are already running"
fi

# ==================================================
# STEP 3: BUILD - Build application images
# ==================================================
echo ""
echo "=========================================="
echo "STEP 3: Building application images"
echo "=========================================="

echo "[Step] Pulling base images for app services..."
compose pull --ignore-pull-failures api-1 api-2 portal nginx || true

echo "[Step] Building api-1, api-2, portal images..."
compose build --pull api-1 api-2 portal

echo "[OK] Build completed"

# ==================================================
# STEP 4: UP - Start application services
# ==================================================
echo ""
echo "=========================================="
echo "STEP 4: Starting application services"
echo "=========================================="

echo "[Step] Starting application services..."
compose up -d "${APP_SERVICES[@]}"

echo "[Wait] Waiting for application services to be ready..."
sleep 3

echo ""
echo "=========================================="
echo "✅ DEPLOYMENT COMPLETE"
echo "=========================================="
echo ""
echo "📊 Service Status:"
echo "  Infrastructure:"
for service in "${INFRA_SERVICES[@]}"; do
  if is_service_running "$service"; then
    echo "    ✅ $service - RUNNING"
  else
    echo "    ❌ $service - NOT RUNNING"
  fi
done
echo ""
echo "  Application:"
for service in "${APP_SERVICES[@]}"; do
  if is_service_running "$service"; then
    echo "    ✅ $service - RUNNING"
  else
    echo "    ❌ $service - NOT RUNNING"
  fi
done

echo ""
echo "🌐 Useful URLs (once healthy):"
echo "  - API via LB:       http://localhost (use /api/* routes)"
echo "  - Swagger (via LB): http://localhost/swagger"
echo "  - Swagger (api-1):  http://localhost:5002/swagger"
echo "  - Swagger (api-2):  http://localhost:5001/swagger"
echo "  - Portal:           http://localhost"
echo "  - Kibana:           http://localhost:5601"
echo "  - RabbitMQ Mgmt:    http://localhost:15672 (admin/RabbitPass123!)"
echo "  - PostgreSQL:       localhost:5432 (securebox_user/ChangeMe123!)"
echo ""
echo "🔑 Default Admin User:"
echo "  - Username: admin"
echo "  - Password: Admin@123"
echo ""

if [[ "${WARN}" -eq 1 ]]; then
  echo "⚠️  [Note] One or more optional infra files/dirs are missing; see warnings above."
  echo ""
fi

echo "📝 Useful commands:"
echo "  - View logs:        docker compose logs -f api-1 api-2 portal nginx"
echo "  - Stop app:         docker compose stop api-1 api-2 portal nginx"
echo "  - Stop all:         docker compose down"
echo "  - Restart:          ./scripts/start.sh"
echo ""
