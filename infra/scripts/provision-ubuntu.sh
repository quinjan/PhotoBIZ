#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this script as root on the fresh Ubuntu Droplet." >&2
  exit 1
fi

DEPLOY_USER="${DEPLOY_USER:-photobiz}"
SSH_PUBLIC_KEY="${SSH_PUBLIC_KEY:-}"

apt-get update
apt-get install -y ca-certificates curl fail2ban ufw rsync git cron

install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

. /etc/os-release
cat >/etc/apt/sources.list.d/docker.list <<EOF
deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable
EOF

apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

if ! id "${DEPLOY_USER}" >/dev/null 2>&1; then
  adduser --disabled-password --gecos "" "${DEPLOY_USER}"
fi
usermod -aG docker "${DEPLOY_USER}"

install -d -m 0755 "/home/${DEPLOY_USER}/.ssh"
if [[ -n "${SSH_PUBLIC_KEY}" ]]; then
  echo "${SSH_PUBLIC_KEY}" >"/home/${DEPLOY_USER}/.ssh/authorized_keys"
  chmod 0600 "/home/${DEPLOY_USER}/.ssh/authorized_keys"
fi
chown -R "${DEPLOY_USER}:${DEPLOY_USER}" "/home/${DEPLOY_USER}/.ssh"

install -d -m 0755 -o "${DEPLOY_USER}" -g "${DEPLOY_USER}" /opt/photobiz
install -d -m 0755 -o "${DEPLOY_USER}" -g "${DEPLOY_USER}" /opt/photobiz/deploy/admin-web
install -d -m 0755 -o "${DEPLOY_USER}" -g "${DEPLOY_USER}" /opt/photobiz/deploy/booth-ui
install -d -m 0750 -o "${DEPLOY_USER}" -g "${DEPLOY_USER}" /opt/photobiz/backups/postgres

ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

systemctl enable --now docker
systemctl enable --now fail2ban

echo "Provisioning complete. Copy infra/production.env.example to /opt/photobiz/.env and fill real values."
