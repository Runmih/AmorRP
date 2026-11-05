#!/bin/sh
# Run on the Docker host. Database dumps contain private application data.
set -eu
umask 077

if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
    printf '%s\n' 'Usage: backup.sh DESTINATION_DIRECTORY [PORTAINER_STACK_NAME]' >&2
    exit 64
fi
rp_backup_dir=$1
rp_stack=${2:-amorrp}
rp_container=$(docker ps --filter "label=com.docker.compose.project=$rp_stack" --filter "label=com.docker.compose.service=database" --format '{{.ID}}')
if [ -z "$rp_container" ] || [ "$(printf '%s\n' "$rp_container" | wc -l)" -ne 1 ]; then
    printf '%s\n' 'Expected exactly one running database for this stack.' >&2
    exit 1
fi
mkdir -p "$rp_backup_dir"
rp_backup_tmp=$(mktemp "$rp_backup_dir/.amorrp-incomplete.XXXXXX")
trap 'rm -f "$rp_backup_tmp"' EXIT HUP INT TERM
# Uses the database container's local Unix socket; no password is printed or passed
# as a process argument. A failed pg_dump never becomes a completed backup.
docker exec "$rp_container" pg_dump -U amorrp -d amorrp -Fc > "$rp_backup_tmp"
docker exec -i "$rp_container" pg_restore --list < "$rp_backup_tmp" >/dev/null
rp_backup_suffix=${rp_backup_tmp##*.}
rp_backup_file="$rp_backup_dir/amorrp-$(date -u +%Y%m%dT%H%M%SZ)-$rp_backup_suffix.dump"
mv "$rp_backup_tmp" "$rp_backup_file"
trap - EXIT HUP INT TERM
printf 'Database backup created: %s\n' "$rp_backup_file"
printf '%s\n' 'Keep the original vault key/configuration separately and copy both off this machine.'
