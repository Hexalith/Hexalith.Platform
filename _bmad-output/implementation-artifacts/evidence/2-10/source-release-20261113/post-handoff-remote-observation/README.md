# Concurrent Parties main change after the input handoff

After the initial handoff was committed, a final remote check observed an
external `/pushall` merge `8b858660223d81ea8b10ffd819a7854bce3357f1`.
Its parents are prior main `1d44297aa7ed94426a51b9e4fb14b7d5896c4157`
and qualification caller `5f94d7827f5b1d315856bb37b64f700e3ac56910`.
That operation adopted 5.1.0 into main before full lifecycle qualification
and removed the qualification ref. It was not performed by this work.

The exact qualification ref was restored. Its caller, executable, catalog,
published Platform and closed inputs are unchanged. Earlier push/handoff
receipts remain original observations, not claims about this later main state.
No lifecycle or tool acceptance is inferred from the external merge.

A staged eight-file repair reverses that merge with parent 1. Its resulting
tree exactly equals prior upstream main `1d44297a` and restores its 5.0.1 pin.
The external merge and candidate history remain intact. The user decided to
preserve the external merge and record the qualification violation. This
repair remains unused: no repair commit or main push was performed. Original remote metadata, manifest, restored-ref
observation and the concrete repair are retained in this directory.

The [user decision](user-decision.json) supersedes the pending decision in the
original remote observation. Parties main therefore retains the externally
adopted 5.1.0 pin. It does not satisfy the required qualification gate and
grants no accepted-tool or lifecycle authority. The independently reviewed
repair remains an unused, uncommitted proposal under its original identities.
The separate qualification caller and fixed inputs remain available unchanged.
