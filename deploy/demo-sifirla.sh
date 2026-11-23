#!/usr/bin/env bash
# Canlı demoyu her gece ilk hâline döndürür: veritabanı silinir, ekler ve demo posta kutusu boşaltılır,
# uygulama açılırken migration ve demo verisini yeniden kurar. Demo talep tarihleri de o güne göre yeniden hesaplanır.
# Cron örneği (Türkiye saatiyle 04.00):  0 1 * * * /opt/csm/deploy/demo-sifirla.sh >> /var/log/csm-sifirla.log 2>&1
set -euo pipefail
cd "$(dirname "$0")"
set -a; . ./.env; set +a

echo "[$(date -Is)] sıfırlama başladı"
docker compose stop app
docker compose exec -T sql /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q \
  "IF DB_ID('CihazServis') IS NOT NULL BEGIN ALTER DATABASE CihazServis SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE CihazServis; END"
docker compose run --rm --no-deps --entrypoint sh app -c 'rm -rf /veri/ekler/*'
docker compose restart smtp
docker compose start app

for _ in $(seq 1 60); do
  if docker compose exec -T caddy wget -qO /dev/null http://app:8080/saglik 2>/dev/null; then
    echo "[$(date -Is)] sıfırlama bitti, uygulama yanıt veriyor"
    exit 0
  fi
  sleep 5
done
echo "[$(date -Is)] UYARI uygulama 5 dakikada yanıt vermedi" >&2
exit 1
