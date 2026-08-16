#!/usr/bin/env bash
set -euo pipefail
if [[ $# -lt 1 ]]; then
  echo "Usage: $0 backups/securebox-YYYYMMDDTHHMMSSZ.sql.gz" >&2
  exit 1
fi
FILE="$1"
if [[ ! -f "${FILE}" ]]; then
  echo "[ERROR] file not found: ${FILE}" >&2
  exit 1
fi
echo "[Warn] This replaces the current database."
gunzip -c "${FILE}" | docker exec -i securebox-postgres psql -U securebox_user -d secureboxdb
echo "[OK] Restore complete."
