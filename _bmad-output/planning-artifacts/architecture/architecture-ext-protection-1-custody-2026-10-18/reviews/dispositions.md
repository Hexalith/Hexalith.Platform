# Finalization dispositions — 2026-10-18

Gate verdict: final decision-ready feasibility proposal. No provider selection, production qualification or dependency availability follows.

| Review | Disposition |
| --- | --- |
| Rubric / both load-bearing inputs | Preserved queued EventStore originals; limit one physical epoch cycle rather than pretending the owner has not reserved. Added explicit versioned object-wrap versus actual tenant-epoch schema gate; no reinterpretation of old KekVersion. |
| Adversary — stranded NotPerformed | Removed terminal admitted manifest aborts. Safe stops stay resumable Unknown under the exact original; NotStarted never means previously admitted and stopped. |
| Adversary — abandoned epochs | Added permanent generation registry and actual retirement of all abandoned/unselected wrapper-bearing generations. Explicitly exempt only the current prepared successor bound to the same original/attempt and verified survivor checkpoint. |
| Adversary — remote buffer lifetime | Named missing key-use token/disposal acknowledgement and host-buffer proof as required contract extensions. Existing OwnedKey.Dispose is insufficient; F1 gate remains open. |
| Vendor verification | Added native log/digest bytes, signature metadata, original locations and regional public keys to retained evidence. CloudTrail order cannot substitute for custody-head order. Claims remain limited to documented primitives. |
| Editorial structure/prose | Moved the acceptance-gate table after scope; clarified CAS, copy inventory, accountable holders and batch sizes; added infrastructure, timing and cost subheadings. Preserved technical tables and diagrams. |
| Parent final consistency | Receipt publication merges into current state, preserving survivor mutations during the wait. Audit/receipt encryption and public verification keys outlive erasure epochs. Existing actor ledger bounds are explicit integration work. |

Blocking requirements remain visible near the beginning of the design: native atomicity, any immediate-erasure requirement, G2 hold/dispatch ordering, key-version schema, remote holder proof, infrastructure/AD-9 acceptance, current-authority recovery, vendor-internal copy assurance and lifetime retention. These are intentionally unresolved; none was converted to an assertion.

Verification is documentation-only: spine lint has zero findings; local links, whitespace, code-fence balance and template removal are checked in `document-checks.json`. Baseline hashes and final artifact hashes are retained separately. A repository-wide `git diff --check` encountered an unrelated concurrently modified `evidence/1-10/unit-tests.txt` blank line at EOF; that file was not edited by this task. No executable tests or infrastructure commands were required for these new documents.
