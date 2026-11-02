# Story 1.17 current-main reference mapping approval

On 2026-11-02 the Platform owner answered “Approve current main tuple (Recommended)” after reviewing the four exact direct gitlinks below. The same four gitlinks remain on Platform main revision `c635a2dc22f404d97c9fe7e27f7e7f4c9017943b`; this record binds local hosted graph qualification to that baseline and reviewed candidate `d891af161e9eb6eabc535e0ba50f910d0b323ca8`. It does not approve publication, deployment, activation, release acceptance, or operational acceptance.

The active [mapping](../../eng/story-1-17-main-reference-mapping.json) has SHA-256 `0246bc7b41b28bde97199133b790477b8c9cdb4018eafa6cde888b6f5c26feb4`.

| Owner | Current Platform direct revision | Declaration SHA-256 |
| --- | --- | --- |
| EventStore | `a3dbec2f8b6150a3ee33dcbfc2b6bf2fec274625` | `21eda6da824d5ae6135ff342dd8a5aae950b02420319b953a836dddeb00ce495` |
| Tenants | `b7824e4bba174b3a459364af24c99dedaf8f0738` | `1588f3428ec29296c452fdd35bf95146142315aad3ad8ceb934e7a791f1fb522` |
| Memories | `8aee7a1323160cb39d7b1415256dbfdcb032939a` | `56f6dc8134d3f7cea06910e923a4736a450e00241e8e4a565ac9226897881840` |
| Parties | `1d3576084ced39ec5104dac409becdbdd1c8635f` | `86002eeb916c77bd753a3c618ce394125794baf421315604c9f551b762624504` |

Each declaration digest equals the earlier approved declaration digest, but each executable Git revision differs. The earlier [baseline approval](story-1-17-reference-mapping-approval-2026-11-02.md) and its evidence remain historical records. Source roots for this qualification are explicit clean Git worktrees outside Platform's nested references. The verifier rejects a different revision even when its declaration bytes match.

The active mapping accepts only the current main baseline or its immediate merge commit whose second parent is the reviewed candidate, with unchanged current-main direct gitlinks. Any later Platform commit, rebase, or changed owner revision requires another explicit mapping decision and requalification. V1Only, unset T0, null activation authority, and the tracker backlog remain in force.
