The strict integration interface requires declared suites and pull-only registry
secrets. Introduce domain-ci-v2.yml so existing domain-ci.yml callers retain their
current contract; the legacy workflow stays byte-identical to merged foundation
57255b502fdd546bb09851d04fc1830175d897f3.

V2 requires isolated success before runner-owned package integration and exact
receipt cleanup, including cancellation and partial startup. Bind its actual
workflow identity and direct integration executor into the authorized closure;
reject legacy identities/digests and missing or changed executor source. Remove
unused legacy hook/filter/timeout/browser/Dapr inputs, preserve explicit direct
integration/AppHost refusals, and keep permissions and registry access read-only.

Retain all legacy validator coverage and run explicit legacy/v2 modes in CI and
release. Document migration without activating callers or adopting speculative
pins. The unenrolled C# candidate consumer, runtime and package layout are unchanged.
Both initial tool downloads and redirects require HTTPS. The scoped attributes
entry preserves authored CRLF in the touched shell tests.

Validation: legacy 411 and v2 254 executable assertions; 95 provenance controls;
56 affected governed-workflow controls on final source; 11 preparation controls;
24 unchanged helper controls; actionlint without new exclusions and clean diff
checks. The broader publisher suite previously passed 159 cases. Changed Python
lines pass the existing style checks; seven unrelated baseline findings remain.
These are local source controls, not hosted reference or candidate acceptance.

E/P adoption, registry provisioning, compatible releases and hosted lifecycle
qualification remain pending. No merge or publication is part of this PR stage.
