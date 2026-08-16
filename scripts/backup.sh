#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")"/.. && pwd)"
OUT_DIR="${ROOT_DIR}/backups"
mkdir -p "${OUT_DIR}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
FILE="${OUT_DIR}/securebox-${STAMP}.sql.gz"
docker exec securebox-postgres pg_dump -U securebox_user secureboxdb | gzip > "${FILE}"
echo "[OK] Backup written to ${FILE}"
