# Story 1.17 Platform direct reference mapping approval

The Platform owner approved local qualification on 2026-11-02 after the explicit checkpoint "approve and continue". The approval binds Platform revision `3d344e850471b6a21fc4649df7ede9a0390689b1` and only the four direct owner revisions and declaration SHA-256 values in [`eng/story-1-17-reference-mapping.json`](../../eng/story-1-17-reference-mapping.json). The mapping file SHA-256 is `46a4bef075af48791ec3f0bc4e4336de8597c36406ccad2f3ab5183e438b41cd`.

| Owner | Direct revision | Declaration SHA-256 |
| --- | --- | --- |
| EventStore | `7c93a1234d14c66f5ee85ed48caaee1d30a671f3` | `21eda6da824d5ae6135ff342dd8a5aae950b02420319b953a836dddeb00ce495` |
| Tenants | `a01e89854f711d1a93ae15695b438424c1ed4f0f` | `1588f3428ec29296c452fdd35bf95146142315aad3ad8ceb934e7a791f1fb522` |
| Memories | `e8956eb5c1bd49297f86702f9606eb9b013bc2f1` | `56f6dc8134d3f7cea06910e923a4736a450e00241e8e4a565ac9226897881840` |
| Parties | `f4285584dce0dfa08e3336fca3c203a1b5e748c6` | `86002eeb916c77bd753a3c618ce394125794baf421315604c9f551b762624504` |

Source roots are session inputs, supplied through `Platform:Hosted:SourceRoots:{EventStore|Tenants|Memories|Parties}` for a hosted publish probe. Each root must be an absolute Git checkout outside Platform's nested `references` directory, at the approved revision, with no tracked source diff, nonignored untracked files, or ignored executable source outside generated build output. The Platform HEAD and index gitlinks and on-disk declaration bytes are checked before graph resolution, graph file creation, or resource addition. The composition test runner and existing `ReferenceOptions(root)` export caller use `HEXALITH_PLATFORM_HOSTED_{OWNER}_ROOT`; Debug project-reference builds use `Hexalith{Owner}Root` MSBuild properties. Equal declaration bytes at another revision do not qualify executable source.

This approval grants local source and hosted graph qualification only. Route mode remains `V1Only`; T0 and activation authority remain unset; the Story 1.17 tracker remains backlog. Historical approval and evidence remain unchanged. Publication, deployment, activation, release acceptance, and operational acceptance require their own authority and evidence.

The reviewed source may be captured as one immediate candidate commit on top of the approved Platform baseline. The verifier accepts that commit only while its parent is the baseline and all four direct gitlinks still match. The committed candidate must pass the hosted tests again; any further Platform commit or integration into a newer base requires a new mapping decision and qualification.
