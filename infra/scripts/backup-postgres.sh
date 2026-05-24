#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${APP_DIR:-/opt/photobiz}"
RETENTION_DAYS="${RETENTION_DAYS:-7}"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
BACKUP_DIR="${APP_DIR}/backups/postgres"
ENV_FILE="${APP_DIR}/.env"

cd "${APP_DIR}"
mkdir -p "${BACKUP_DIR}"

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "Missing ${ENV_FILE}" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
. "${ENV_FILE}"
set +a

POSTGRES_DB="${POSTGRES_DB:-photobiz}"
POSTGRES_USER="${POSTGRES_USER:-photobiz}"
BACKUP_PATH="${BACKUP_DIR}/photobiz-${TIMESTAMP}.dump"

docker compose --env-file "${ENV_FILE}" -f docker-compose.prod.yml exec -T postgres \
  pg_dump -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -Fc >"${BACKUP_PATH}"

gzip "${BACKUP_PATH}"
find "${BACKUP_DIR}" -type f -name 'photobiz-*.dump.gz' -mtime +"${RETENTION_DAYS}" -delete

echo "Created ${BACKUP_PATH}.gz"
