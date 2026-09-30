#!/usr/bin/env bash
set -euo pipefail

DOMAIN="${1:-}"
SOURCE="${2:-$(cd "$(dirname "$0")" && pwd)/linux-x64}"
APP_USER=odyssey
APP_DIR=/opt/odyssey
DATA_DIR=/var/lib/odyssey
KEY_FILE=/etc/odyssey/plugin.key

configure_nginx() {
  apt-get update
  apt-get install -y certbot python3-certbot-nginx
  install -d /var/www/html
  cat > /etc/nginx/sites-available/odyssey <<EOF
server {
    listen 80;
    server_name ${DOMAIN};
    location /.well-known/acme-challenge/ { root /var/www/html; }
    location / { return 301 https://\$host\$request_uri; }
}
EOF
  ln -sfn /etc/nginx/sites-available/odyssey /etc/nginx/sites-enabled/odyssey
  nginx -t
  systemctl reload nginx
  certbot certonly --webroot -w /var/www/html -d "${DOMAIN}" --non-interactive --agree-tos --register-unsafely-without-email
  cat > /etc/nginx/sites-available/odyssey <<EOF
server {
    listen 80;
    server_name ${DOMAIN};
    location /.well-known/acme-challenge/ { root /var/www/html; }
    location / { return 301 https://\$host\$request_uri; }
}
server {
    listen 443 ssl;
    server_name ${DOMAIN};
    ssl_certificate /etc/letsencrypt/live/${DOMAIN}/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/${DOMAIN}/privkey.pem;
    location /odyssey {
        proxy_pass http://127.0.0.1:5088;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_read_timeout 3600s;
    }
    location /health {
        proxy_pass http://127.0.0.1:5088;
    }
}
EOF
  nginx -t
  systemctl reload nginx
}

install_caddy() {
  apt-get update
  apt-get install -y debian-keyring debian-archive-keyring apt-transport-https curl
  curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | tee /etc/apt/sources.list.d/caddy-stable.list
  apt-get update
  apt-get install -y caddy
  printf '%s {\n    reverse_proxy 127.0.0.1:5088\n}\n' "${DOMAIN}" > /etc/caddy/Caddyfile
  systemctl enable --now caddy
  systemctl reload caddy
}

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this on the server as root: sudo bash install.sh <domain>"
  exit 1
fi
if [[ -z "${DOMAIN}" ]]; then
  echo "Usage: sudo bash install.sh odyssey.example.com [/path/to/linux-x64]"
  exit 1
fi
if [[ ! -x "${SOURCE}/PNX.Odyssey.Server" ]]; then
  echo "Published server was not found at ${SOURCE}/PNX.Odyssey.Server"
  exit 1
fi

id -u "${APP_USER}" >/dev/null 2>&1 || useradd --system --home "${DATA_DIR}" --shell /usr/sbin/nologin "${APP_USER}"
install -d -o "${APP_USER}" -g "${APP_USER}" -m 0750 "${APP_DIR}" "${DATA_DIR}" /etc/odyssey
find "${APP_DIR}" -mindepth 1 -delete
cp -a "${SOURCE}/." "${APP_DIR}/"
chown -R "${APP_USER}:${APP_USER}" "${APP_DIR}" "${DATA_DIR}"
chmod 0755 "${APP_DIR}/PNX.Odyssey.Server"

if [[ ! -s "${KEY_FILE}" ]]; then
  openssl rand -hex 24 > "${KEY_FILE}"
fi
chown "${APP_USER}:${APP_USER}" "${KEY_FILE}"
chmod 0600 "${KEY_FILE}"

cat > /etc/systemd/system/odyssey.service <<EOF
[Unit]
Description=PNX Odyssey session service
After=network-online.target
Wants=network-online.target

[Service]
User=${APP_USER}
Group=${APP_USER}
WorkingDirectory=${APP_DIR}
ExecStart=${APP_DIR}/PNX.Odyssey.Server
Restart=always
RestartSec=2
Environment=ASPNETCORE_URLS=http://127.0.0.1:5088
Environment=ODYSSEY_DB=${DATA_DIR}/odyssey.db
Environment=ODYSSEY_PLUGIN_KEY_FILE=${KEY_FILE}
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable --now odyssey.service
sleep 1
curl -fsS http://127.0.0.1:5088/health >/dev/null

if command -v nginx >/dev/null 2>&1 && ss -ltn | awk '{print $4}' | grep -Eq '(:|\.)443$'; then
  configure_nginx
elif ! ss -ltn | awk '{print $4}' | grep -Eq '(:|\.)443$'; then
  install_caddy
else
  echo "Port 443 is already in use. Proxy wss://${DOMAIN}/odyssey to http://127.0.0.1:5088."
fi

echo
echo "Plugin server URL: wss://${DOMAIN}/odyssey"
echo "Plugin key: $(cat "${KEY_FILE}")"
