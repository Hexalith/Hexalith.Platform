# Enrollment preflight gaps observed during live verification

The live runtime proofs are separate from complete kit adoption.

- Parties and Tenants: `validate --adoption` cannot enumerate the tracked file list. Git itself succeeds; Parties produces 472,879 bytes. The validator uses `GitWorkspaceProcess`, whose shared process capture is bounded to 65,536 characters and treats truncation as failure. The bounded capture is the inferred cause; the public report currently gives only HXA001 / "Cannot enumerate tracked module-owned files." This needs a Builds follow-up, a regression on a real-sized index, and a reviewed release. The released validator was not patched or bypassed for these checks.
- Parties and Tenants: no `module/hosting-inventory.json` exists at the tested owner revisions. Existing ownership/freeze documentation remains in force; this run grants no hosting retirement authority.
- Tenants: its existing v2 declaration lists only `eventstore` and omits the distinct minimum `memories` dependency required by the kit, excluding self. The runner still starts its technical minimum; this does not repair the declaration. The runtime proof tests the existing declaration plus the recorded current-tool/Platform pin overlay. It does not claim that this declaration passes kit adoption.
- Folders and Projects: the Platform-recorded revisions have no v2 module declaration. Their live SM-2 status stays open; no replacement manifest or successful run was fabricated.

Exact reports are in `parties/adoption.txt`, `tenants/adoption.txt` and `kit-preflight-gaps.json`; recorded module identities are in `remaining-module-prerequisites.json`. No other repository's owning branch, tool pin, declaration, inventory or hosting projects were changed by this verification.
