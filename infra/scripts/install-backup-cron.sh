#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${APP_DIR:-/opt/photobiz}"
CRON_FILE="/etc/cron.d/photobiz-postgres-backup"

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this script as root on the Droplet." >&2
  exit 1
fi

cat >"${CRON_FILE}" <<EOF
SHELL=/bin/bash
APP_DIR=${APP_DIR}
RETENTION_DAYS=7
15 18 * * * root ${APP_DIR}/infra/scripts/backup-postgres.sh >> ${APP_DIR}/backups/postgres/backup.log 2>&1
EOF

chmod 0644 "${CRON_FILE}"
echo "Installed nightly PhotoBIZ PostgreSQL backup cron at ${CRON_FILE}."
