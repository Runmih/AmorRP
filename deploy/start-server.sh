#!/bin/sh
set -eu

# Free Render services cannot run a separate pre-deploy migration command.
# Keep this opt-in; paid deployments can retain their separate migration step.
case "${AMORRP_MIGRATE_ON_START:-false}" in
    true)
        rp_migration_only=false
        for rp_arg do
            if [ "$rp_arg" = "--migrate" ]; then
                rp_migration_only=true
            fi
        done
        if [ "$rp_migration_only" = false ]; then
            dotnet AmorRP.Server.dll --migrate
        fi
        ;;
    false) ;;
    *)
        printf '%s\n' 'AMORRP_MIGRATE_ON_START must be true or false.' >&2
        exit 64
        ;;
esac

# The server receives container termination signals directly.
exec dotnet AmorRP.Server.dll "$@"
