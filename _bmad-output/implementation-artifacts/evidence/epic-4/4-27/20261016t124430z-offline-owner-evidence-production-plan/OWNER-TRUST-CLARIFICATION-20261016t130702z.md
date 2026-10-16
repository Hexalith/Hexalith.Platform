---
title: 'Sole-owner clarification: public signing key versus certificate'
status: draft
scope: offline-planning-only
operational_gate: closed
release: FAIL
---

# Sole-owner trust clarification

This create-only addendum qualifies the ownership wording in [PLAN-v3.md](PLAN-v3.md).
It preserves that plan, its review, all drafts and all original evidence. It
records user-provided planning facts and proposed next steps, not an owner
attestation, authenticated trust root or accepted certificate.

## Newly supplied facts and remaining unknowns

The user states: "I am Jérôme Piquot the Owner of the project and only
contributor." The user then states: "I do not have a public certificate."

For planning, name Jérôme Piquot as the declared accountable project owner and
sole contributor. A separate corporate security organization is not a prerequisite
to assigning project accountability. Jérôme can be proposed for the project
trust-owner/producer role, subject to establishing the actual relevant signing
key, purpose and authority. Ownership of particular server PKI, credentials,
grants, revocation sources or offsite custody is not inferred from project
ownership. Independent reviewer assignment remains unidentified; a reviewer need
not contribute code. Agent review of this document supplies no owner authority.

The statement about a certificate establishes neither absence of a signing
public key nor absence of the infrastructure's server/client TLS certificates.
No existing public key, private key, certificate or credential was inspected in
this clarification. No filesystem/evidence search was repeated.

## Distinct public materials

1. **Owner evidence-signing verification material.** This can be a public signing
   key rather than an X.509 certificate, provided the independently reviewed
   format/algorithm/role contract supports it. An existing SSH signing public
   key is a possible candidate; no actual key or algorithm is selected here.
   The key's authorized purpose, fingerprint/provenance, validity/revocation and
   independent trust establishment still require evidence. Public-key possession,
   a self-signature or a caller assertion cannot establish those facts alone.
2. **API server TLS trust and serving identity.** The exact public CA/chain and
   current server leaf remain separate infrastructure evidence. The project
   owner's lack of a personal certificate does not establish those certificates
   are absent. They must come from authoritative PKI/current serving evidence
   under the plan's independently authorized qualification scope. Do not read
   kubeconfig or contact the API to obtain them in this scope.
3. **One public client TLS identity.** Its exact public certificate, issuing chain,
   authenticated mapping and protected single-pair custody remain required.
   A new owner signing key cannot substitute for this client or API TLS identity.
4. **Encryption recipients.** A public encryption-recipient file does not establish
   signing authority. The historical candidate's public trust material was
   explicitly recorded as unauthenticated for current purposes; it is not promoted
   to a current root by this clarification.

## Concrete next step, without assuming an existing certificate package

First determine from the owner's own known setup whether an existing evidence
signing key is already used. The previously read record describes eight historical
recovery signatures, so an existing signing setup is a relevant lead. Those
historical signatures do not prove current key authority or supply current TLS
identity/rights evidence. This addendum performs no new search or validation.

The smallest useful owner input is **the exact existing public verification file
path or the name of the signing setup used for those recovery records**, together
with any existing independently verifiable fingerprint/purpose/provenance record.
Supply public material only; no private-key export, kubeconfig or token is needed.
The signing setup's name is a lead for a separately scoped next task, not accepted
trust evidence. Do not manufacture trust-root-establishment.json or sign a new
assertion merely to fill the earlier prompt's placeholder.

If there is no suitable signing key, the next task is a concrete offline bootstrap
design for the sole-owner setup: supported public-key format/algorithm, exact
signing purpose, private custody/backup, independently authenticated fingerprint
establishment, independent reviewer, revocation/replacement handling and public
output contract. Keep unanswered authority/policy questions explicit. New key
generation, private access, signatures and enrollment are separately scoped owner
operations; none is authorized or performed here. A new key cannot authenticate
old unsigned evidence or recreate missing historical signatures/lineage.

Thus the earlier request for an already-existing certificate and signed trust
package was premature. The final acceptance requirements in the production plan
remain open; the immediate information request is narrowed to public verification
material or identification of the existing signing setup. If neither is known,
record that bootstrap gap rather than repeating completed evidence searches.

## Fixed stop and preservation boundary

Actual UTC checked with the clock tool: **2026-10-16T13:07:02Z**, before the
unchanged **2026-10-16T16:27:56Z** ceiling. This is an observation only. Preparation
remains **2026-10-15T16:27:56Z**. At or after expiry, stop candidate-dependent
production, qualification, enrollment and activation work; historical offline
planning may continue with the stop stated. No timestamp refresh or replacement
recovery is allocated.

Final sealed SHA-256 remains
`c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d`.
The exact verifier's recorded **exit 1** and sealed preservation exception remain
unchanged. This addendum supplies no disposition of prior Story 1.7 preservation
drift or additional Story 1.8 change. Both preparation directories, immutable
packets, signed originals, expired recovery, failures, concurrent changes,
indexes and gitlinks receive no writes.

No actual kubeconfig/private credential read, authentication, network/API/provider/
IAM request, preflight/verifier replay, signing, publication, infrastructure change,
capture, restore, retirement, upgrade, deployment, domain write, staging, commit,
push or dependency update occurs. No operational approval prompt is emitted.

Spec-23 remains **draft**; Story **3.6/Epic 3/4.27 in progress**;
**4.1 operational gate closed**; release **FAIL**.
