#!/bin/sh
set -eu
# The startup verifier belongs to the real runner and inherits only its own process tag.
[ "${#HEXALITH_G4_RUN_ID}" -eq 32 ]
case "$HEXALITH_G4_RUN_ID" in *[!0-9a-f]*) exit 1 ;; esac
# The lifecycle qualification deliberately fails a required task after resources become healthy.
[ "${1-}" != "--fail-after-readiness" ] || exit 42
