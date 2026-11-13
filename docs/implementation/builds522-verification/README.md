# Builds 5.2.2 published identity verification

The retained procedures authenticate the normal public release and original
publisher artifact, both official NuGet archives and catalog SHA-512 hashes,
repository signatures, nuspec source revisions, and every original payload
member. Each signed archive adds only `.signature.p7s`. The complete
AppHost/Tooling pair is measured from the signed Module archive.

Run the verification procedure with version `5.2.2`, source
`02ed624aa19781cac75d2c9112940e9bd830441a`, the selected Builds root and a new output directory.
Add each actual archive filename to the measured receipt, then run the
member measurement procedure against those same downloaded inputs.

These observations grant published identity and validation transport only.
Exact-source hosted tuple/catalog gates remain authoritative; no candidate,
runtime, lifecycle, image, native or Story acceptance is claimed here.
