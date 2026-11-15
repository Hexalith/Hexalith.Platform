#!/usr/bin/env bash
# Mutation shim: redirect the launcher module directory (argument after the -c code) to MUTANT_DIR.
args=("$@"); for i in "${!args[@]}"; do if [[ "${args[$i]}" == */infrastructure/keycloak-workload-binding ]]; then args[$i]="$MUTANT_DIR"; fi; done
exec /usr/bin/python3 "${args[@]}"
