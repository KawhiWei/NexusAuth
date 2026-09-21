#!/bin/sh
set -eu

cd "$(dirname "$0")/../.."
key_file=.env.apisix.local

if [ -z "${APISIX_ADMIN_KEY:-}" ]; then
    if [ ! -f "$key_file" ]; then
        umask 077
        printf 'APISIX_ADMIN_KEY=%s\n' "$(openssl rand -hex 32)" > "$key_file"
    fi
    APISIX_ADMIN_KEY=$(sed -n 's/^APISIX_ADMIN_KEY=//p' "$key_file")
fi

if [ "${#APISIX_ADMIN_KEY}" -lt 32 ]; then
    printf '%s\n' 'APISIX_ADMIN_KEY must contain at least 32 characters.' >&2
    exit 1
fi
export APISIX_ADMIN_KEY

docker compose -f docker-compose.apisix.yml up -d

# The public discovery endpoints use localhost:5100. Inside APISIX that address
# resolves to APISIX itself, so forward only its internal port 5100 to SSO.
attempt=0
until docker compose -f docker-compose.apisix.yml exec -T apisix \
    perl /usr/local/apisix/conf/put-backchannel-route.pl; do
    attempt=$((attempt + 1))
    if [ "$attempt" -ge 30 ]; then
        printf '%s\n' 'APISIX did not become ready for backchannel route setup.' >&2
        exit 1
    fi
    sleep 2
done

printf '%s\n' 'APISIX: http://localhost:9180' 'APISIX Dashboard: http://127.0.0.1:9000'
