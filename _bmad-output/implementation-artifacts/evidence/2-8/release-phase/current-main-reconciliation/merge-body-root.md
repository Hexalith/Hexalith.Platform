Add domain-ci-v2.yml for required declared package integration after isolated tests.
Keep the legacy workflow contract intact. Bind v2 workflow identity and its direct
executor to the exact authorized source; require pull-only registry credentials,
HTTPS downloads, bounded owner finalization and classified diagnostics. Run both
workflow validator modes in producer CI and release.

Preserve current main's documented immediate metadata refusal and explicit same-owner
cleanup retry, with no production source or stopping-policy change. Add one active-
attachment control covering retained ownership and holds on refusal, then idempotent
cleanup with first outcome, timestamps and absolute expiry unchanged.

A fresh Debug graph and all 78 local/CI/legacy controls pass. Complete exact-head
hosted checks remain required before merge. Reference adoption, qualified releases
and real hosted lifecycle observations remain separate requirements; source controls
supply no candidate acceptance.
