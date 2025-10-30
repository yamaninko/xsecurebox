#!/usr/bin/env bash
set -euo pipefail

# Secure Box — stop script
# - Stops and removes containers and networks for the stack.
# - Use --volumes to also remove named volumes (data loss!).

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

cd "${ROOT_DIR}"

REMOVE_VOLUMES=0
if [[ ${1-} == "--volumes" || ${1-} == "-v" ]]; then
  REMOVE_VOLUMES=1
fi

echo "[Step] Stopping and removing stack…"
if [[ ${REMOVE_VOLUMES} -eq 1 ]]; then
  echo "[Warn] Removing named volumes (data will be lost)."
  compose down -v
else
  compose down
fi

echo "[OK] Stack stopped."

