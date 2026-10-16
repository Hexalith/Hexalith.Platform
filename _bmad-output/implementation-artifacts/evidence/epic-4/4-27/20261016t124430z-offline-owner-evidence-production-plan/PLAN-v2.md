---
title: 'Platform 4.27 offline owner-evidence production plan for Timesheets 3.6'
type: documentation
created: '2026-10-16'
status: draft
story_status: in-progress
scope: offline-planning-only
baseline_commit: b5e1caea7ea95026e3e2138de050c6712f201502
operational_gate: closed
release: FAIL
---

# Offline owner-evidence production handoff

This is a proposed production plan, not produced owner evidence or an activation
revision. Platform owns adapters, evidence-production tooling and infrastructure
qualification. Timesheets owns intake, acceptance evidence and the Story 3.6
acceptance decision. No substantive evidence gap is closed by this document.

Actual UTC was **2026-10-16T12:43:23Z**, checked again at **12:44:30Z**, before
the unchanged **2026-10-16T16:27:56Z** ceiling. These are planning observations.
Preparation remains **2026-10-15T16:27:56Z**, RID `20261015t162756z`.
At or after the ceiling, stop every candidate-dependent production,
qualification, enrollment, review-for-activation and execution step. Offline
historical documentation may continue with that stop recorded. No new signature,
readback, acceptance date or maintenance window extends the ceiling. A successor
recovery requires separate authorization; this plan allocates none.

## Inputs, available facts and limits

The exact Timesheets inputs read for this continuation are:

- `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/3-6-resume-checkpoint.md`.
- `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/evidence/3-6/20261016t113557z-offline-owner-evidence-delta/README.md`.
- `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/evidence/3-6/20261016t112503z-offline-owner-evidence-intake/README.md`.

They record twelve unresolved gaps and no newly supplied owner artifact within
their documented search scope. That search conclusion is historical and scoped;
it is not a claim about undisclosed external evidence. No evidence search,
verifier, preflight, test, build, runtime sweep or old preservation validation was
repeated here. The 135 synthetic tests and 13 LaunchReadiness passes remain dated
results, not current infrastructure qualification.

The exact sealed manifest remains
`/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision-sealed/packet-v1.json`,
SHA-256 **`c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d`**.
It selects `enrollment-execution-binding-v4.json`, recorded SHA-256
`c42bfe8cd0534cafd652872a2abbc7f2171e9bb5ac6083ad7ac4e7755d453622`.
The original supplement remains
`a7b9bf06139cec3022fbced5fe182b42c773b9a7099c40c84162893a766e2631`.
These identify inputs; they establish no signer authority.

The recorded exact verifier result is preserved **exit 1**, at
**2026-10-16T11:25:36.164697Z**, reason
`offline-revision-integrity-or-custody-refusal`, gate closed. Its stdout, stderr
and command remain in the Timesheets intake `exact-verifier-result.json`.
The older whole verifier also remains refused. Separate historical matching hash
counts do not override either refusal. This plan does not rerun or repair them.

The known fixed binding is context `jpiquot@local`, endpoint
`https://192.168.1.30:6443`, config SHA-256
`f00b691a5886ef76e6be0d6e3efe4030251147b7bba74bb0c58fa1944e0fe86c`.
The configuration and private credential were not opened. A context name does
not identify an authenticated owner or prove a current client identity.
Current CA, peer, client, grant, revocation head, rights and provider behavior
remain unknown/unaccepted. Ten sealed enrollment fields remain recorded null;
public live flags remain false and the real output slot remains closed/absent
according to the intake. This is not a new filesystem absence assertion.

Read-only static inspection in this continuation confirms the current source
still refuses public live entry points and accepts only synthetic witnesses in
its qualification lane. Platform HEAD was obtained directly from Git as
`b5e1caea7ea95026e3e2138de050c6712f201502`. Existing unrelated modifications to
`deferred-work.md` and `spec-1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagnostics.md`
were present and are preserved. A newer repository revision is not a new sealed
baseline, owner disposition or passing integrity result.

## Ownership and common artifact contract

**Every named person's identity and authority below is UNIDENTIFIED unless an
owner supplies independently authenticated evidence.** Administrator is an
existing accountable role, not a newly verified person. Platform Maintainer is
the documented repository role. No agent is an owner, authorized signer or
independent human reviewer. Proposed assignments become effective only when the
organization authenticates the named people, their mandates and independence.

| Role code | Accountable producer | Required independent reviewer |
| --- | --- | --- |
| T | Organization security/trust authority — person UNIDENTIFIED | Organization trust reviewer with separately established authority — UNIDENTIFIED |
| S | Administrator and API server PKI owner — people UNIDENTIFIED | Security/PKI reviewer independent of issuer/operator — UNIDENTIFIED |
| C | Administrator and credential custodian — people UNIDENTIFIED | Enrollment/custody reviewer independent of delivery operator — UNIDENTIFIED |
| G | Native authorization/RBAC authority and grant signer — people UNIDENTIFIED | Native authorization reviewer independent of grant issuer and KubeSphere — UNIDENTIFIED |
| L | Grant/revocation records authority — person UNIDENTIFIED | Independent offsite custodian and lineage reviewer — people UNIDENTIFIED |
| Q | Platform runtime/security maintainer — person UNIDENTIFIED | Runtime/transport qualification reviewer independent of implementation/operator — UNIDENTIFIED |
| P | Platform preservation owner — person UNIDENTIFIED | Preservation reviewer independent of changed-file producer — UNIDENTIFIED |
| A | Administrator, with Platform supplying exact revision bytes — people UNIDENTIFIED | Activation reviewer independent of author/operator — UNIDENTIFIED |
| X | Timesheets acceptance owner — person UNIDENTIFIED | Timesheets acceptance reviewer independent of evidence producers — UNIDENTIFIED |

For **every** artifact row below, its role code supplies both producer and
reviewer; the source, output, signature and acceptance requirements are
cumulative with this common contract:

1. Supply public/nonsecret original payload bytes, bounded schema/version,
   byte count and SHA-256, original detached signature(s), signature algorithm,
   signer public key/certificate reference, signer identity/role/purpose and
   delegation limits. Sign the original byte serialization; never verify a
   reserialized approximation. JSONL signatures must identify the exact original
   record framing/byte span. Platform must implement and review that framing
   contract before new production. No fabricated historical signatures.
2. Bind RID, sealed manifest digest, target/cluster/resource identity, exact
   endpoint/context/config digest, applicable client DER/SPKI identity, input
   payload digests and relevant predecessor/head. Unknown identity values remain
   explicit blockers; no historical UID is silently promoted to current truth.
3. State observation time, `validFrom`, `validUntil`, revocation cut, source of
   freshness and applicable CRL/OCSP or authoritative revocation records. Require
   an overlapping valid interval strictly before the ceiling. Signature creation
   alone proves neither observation time nor current nonrevocation. Evidence
   that lacks a usable current revocation/completeness mechanism fails; an
   owner may not substitute an unsupported assertion of nonrevocation.
4. Independently establish the applicable signing key, authorized role/purpose,
   scope, validity and revocation status through T's verified authority register.
   Verify the entire relevant delegation path and signer transitions. Server CA
   trust is separate from authority to sign grants, custody, runtime results,
   preservation dispositions, reviews or approvals. A file beside a signature,
   `allowed_signers.txt`, `recipient.pub` or a self-signed key is not bootstrap
   authority. Reviewers sign their own verification/disposition records using
   independently established reviewer keys; their records must bind exact
   reviewed bytes and independence. An agent's review never substitutes.
5. Preserve original signatures and restricted supporting evidence in owner
   custody. Return only public certs, sanitized native records and typed results;
   no kubeconfig, private key, bearer token, raw process environment, secret
   location capability or raw HTTP body enters this handoff or Timesheets intake.
   Nonsecret custody references must not grant access. New evidence uses a
   separate create-only owner delivery location, selected by the owner after
   authorization, outside all sealed/preparation directories. No such directory
   or operational slot is allocated by this plan.

Tool status vocabulary: **available** means an existing offline primitive can
support a narrow part; **implementation needed** means no accepted production
path exists; **owner operation** means already-produced authoritative originals
or separately authorized acquisition/signing/delivery is required. A row can
require all three. None means permission to execute here.

## Artifact production contracts

### 1. Public authority roots, server trust and peer

| Artifact and role | Authoritative source and exact nonsecret output | Signature, trust, validity and objective acceptance | Tool status |
| --- | --- | --- | --- |
| `public-trust-roots/` — T | Organization-controlled public key/PKI and delegation records: exact public cert/key bytes for trust, server attestations, client attestations, native grants/revocations, offsite custody, runtime qualification, preservation, reviewer and eventual approval roles. Include client issuing public chain if different from server chain. Map every file to DER/SPKI/key fingerprint, authorized person/role/purpose/scope and validity/revocation source. | Authenticated organization provenance must authorize each role. Check every key and delegation against the independent register, not a root presented only by its own claimant. Public certificates retain original CA signatures; bare keys require independent authorization. Missing applicable root blocks its dependent rows. | Available: byte/custody inventory only. Implementation needed: public key format/signature/delegation validation. Owner operation: authoritative public export and authentication. |
| `trust-root-establishment.json` plus producer and reviewer detached signatures — T | Existing independently authenticated organization security authority and a second approved offline verification channel. Output named producer/reviewer and owner-role assignments, exact root fingerprints, provenance-channel/reference, actual verification method/result, purpose/delegation constraints, validity, current revocation evidence references and authenticated organization authority basis. | The reviewer establishes the security authority and fingerprints outside the submitted assertion's own chain, through a pre-existing trusted organization record/contact channel. Both channel and authority basis must be concrete and reviewer-verifiable. If none exists, bootstrap stays blocked; signing this JSON with a newly supplied key cannot solve it. Bind independent reviewer verification and exact public-root bytes. | No existing bootstrap/authentication producer or verifier. Implementation needed for the nonsecret validation/report contract. Owner operation for the actual identity/mandate verification and signatures. **First concrete owner deliverable**, together with the root files. |
| `server-ca.pem`, `server-intermediates.pem` — S | API server PKI authority's public CA issuance/configuration record; exact PEM cert bytes with per-certificate DER/SPKI SHA-256, issuer, serial, subject, CA constraints/path limits and permitted chain/order. The intermediate file is required; an empty file is permitted only with signed explicit direct-root-chain disposition. | Preserve CA signatures and verify chain against the independently authenticated server trust source, valid CA usage and current revocation evidence. A client-auth CA must not be assumed identical. Bind these exact bytes in the server trust attestation. | Available: file binding only. Implementation needed: certificate/path/constraint parser and verifier. Owner operation: public export if not already available. |
| `server-trust-attestation.json` and producer/reviewer signatures — S | Independently authenticated PKI/Administrator server-trust configuration authority. Output authenticated trust-source reference, exact endpoint/IP/cluster identity, permitted CA chain/fingerprints, trust purpose, source authority and current validation/revocation evidence. | Establish the trust source independently of kubeconfig/KubeSphere endpoint metadata and the peer being validated. T must authorize S's signing purpose; reject trust-on-first-use or default OS trust. Given T and exact CA bytes, acceptance requires authenticated explicit CA-only trust for this endpoint throughout the proposed interval. | No production attestation/authentication path. Owner operation plus implementation of offline verification; never read actual kubeconfig here. |
| `server-peer.pem` — S | Previously independently authorized current endpoint qualification or server PKI's authoritative issued/current-serving record. Output exact public served leaf, DER/SPKI pins, issuer/serial/validity, IP SAN `192.168.1.30`, server usage and chain reference. | Leaf CA signature, valid chain, strict IP identity, current revocation evidence and binding to actual serving peer are required. Issued-but-not-served certificate metadata alone does not prove current peer. | Public leaf export/current observation is an owner operation. Existing dormant `_connect` does not provide an authenticated peer evidence producer. |
| `server-peer-attestation.json` and producer/reviewer signatures — S | Authorized observation bound to the exact serving endpoint and authoritative PKI record. Output leaf/chain digests, current serving observation, IP-SAN/server usage validation result, revocation evidence and observation/validity interval. | Cross-bind server-trust attestation and later actual TLS qualification. Reject a historical leaf, a pin alone, an unsigned capture or unverified hostname/chain. Any inability to observe serving identity within authorized scope is a stop. | Implementation needed for sanitized observation/verification. Owner operation for actual peer observation/signing; no handshake here. |

### 2. One public client and protected delivery

| Artifact and role | Authoritative source and exact nonsecret output | Signature, trust, validity and objective acceptance | Tool status |
| --- | --- | --- | --- |
| `client-public.pem` — C | Chosen one current client certificate from its issuing PKI/custodian; public leaf and referenced public issuing chain only. Output DER/SPKI fingerprints, issuer/serial/subject, client usage and validity. | Preserve issuer signature, verify issuing chain with independently authenticated client-auth authority. One certificate/key-pair identity only; no candidate list, token alternate, executable auth or fallback. Groups require authenticated server identity-mapping evidence, not an untrusted subject string. | No actual credential read/export in this task. Public export is owner operation; certificate verification implementation needed. |
| `client-identity-attestation.json` and custodian/reviewer signatures — C | Client issuing authority and native authentication configuration. Output exact one certificate identity, authenticated principal/groups and mapping source, public issuing chain, endpoint/context/config binding, observation/validity and revocation evidence. | Bind `client-public.pem` and later grants/rights/delivery. Verify signing authority for client identity claims separately from certificate issuance. Confirm client usage/chain and server authentication mapping. A remembered `jpiquot` subject or successful old GET cannot pass. | Synthetic `clientIdentity` metadata exists; production parser, authentication mapping verification and signed evidence validation are absent. Owner operation required. |
| `protected-enrollment-delivery-attestation.json` and separate custodian and enrollment-reviewer signatures — C | Protected credential custodian's actual authorized delivery and independent review, or previously produced originals. Output named custodian/recipient/channel identity, exact public client binding, delivery authorization/reference, exclusive custody/recipient, prior key/certificate-pair verification result/method, single-consumption state and no-fallback result. | Do not output private bytes or a private-key digest. Bind a nonsecret delivery event identifier to the chosen public certificate and exact recipient, consumer/runtime and attempt. Attest actual delivery/verification rather than a planned channel. One-use evidence must prevent replay and bind disposal/failure retention without claiming impossible memory erasure. If a live channel cannot meet these properties, stop. | Available: `SyntheticCredentialDelivery` models one use with inert bytes only. Implementation needed: protected real delivery, pair validation and replay/custody enforcement. Owner operation: private access, pair verification and delivery under separate scope; prohibited here. |

The sealed fixture's inert `credentialSha256`, prefix and 4096-byte bound are
not a production certificate/key-pair schema. Do not place real credentials in
the fixture or populate sealed null fields. The production evidence/consumer
contract must be independently designed and reviewed without changing originals.

### 3. Native grants, complete lineage, current head and all current rights

| Artifact and role | Authoritative source and exact nonsecret output | Signature, trust, validity and objective acceptance | Tool status |
| --- | --- | --- | --- |
| `native-grant-current.json` and grant-owner/reviewer signatures — G | Independently authorized authoritative native authentication/RBAC export and grant authority records. Output exact principal/groups/client certificate, cluster identity, binding name/UID/resourceVersion, roleRef, all applicable public subject/binding/role records, complete resolved rules including aggregation and relevant resource/nonresource permissions, provenance and observation/validity. | Native ownership and signer authority must be independent of KubeSphere. Resolve applicable grants completely; a role name or the one measured binding cannot prove the whole policy. Include exact sanitized source records as `native-records/` files referenced by digest and signed by the export owner. Verify identity, record completeness and grant lineage/head. | Available: selector 19 checks historical binding shape and deliberately returns `currentAuthorityAccepted=false`. Implementation needed: complete native export verification/rule resolution. Owner operation: authoritative native export/signing; no grant or IAM changes here. |
| `native-records/` exact referenced role/binding/public subject records — G | Same native export, with exact nonsecret bytes, record kind/name/namespace/UID/resourceVersion, all rule and aggregation dependencies and identity-mapping provenance. | Owner signatures must bind each member via an exact signed inventory in `native-grant-current.json`; a digest without that authenticated inventory is insufficient. Reviewer independently verifies source and completeness. Never expand the 23-selector lane to collect these. | Owner operation for acquisition; implementation needed for format/schema/completeness validation. |
| `native-grants-and-revocations.jsonl` and original per-record signatures — L | Authoritative grant/revocation journal from independently authenticated trusted origin. Output every grant, revoke, predecessor, sequence, target/principal/scope, effective time and authorized signer transition, through the declared cut/head; include original framing and signatures. | Authenticate origin, every link, signer validity at issuance and current signer status, effective revocations and complete sequence. Missing/unsigned history remains missing; no retroactive invented attestations or fabricated journal may replace it. If no such authoritative journal exists, owner must disclose that impossibility; acceptance stays blocked pending separately agreed authoritative evidence contract. | No production lineage verifier/producer. Implementation needed; authoritative historical originals and authenticated export are owner operations. |
| `native-current-head-attestation.json` and authority/reviewer signatures — L | Independent authoritative latest-head source and independently retained offsite checkpoint, outside the presented chain. Output origin, current sequence/head digest, cut/as-of time, retrieval/observation evidence reference, freshness deadline and completeness scope. | Authenticate that this is the current head, not merely the final included hash. Require independent latest-head witness, exact journal digest and prevention of older-head replay. A hash chain or its own head signature does not prove latestness. No current authenticated head means no rights acceptance. | Implementation needed: anti-rollback/current-head verification and refresh mechanism. Owner operation: authenticated latest-head evidence; none obtained here. |
| `offsite-custody-attestation.json` and independent custodian signature plus lineage-reviewer signature — L | Independent offsite custodian's actual immutable retention/readback records. Output custodian identity, exact public journal/head and signature object/version/digest bindings, retention bounds, readback time/result and independent validator identity. | Establish custodian signing purpose via T, distinguish writer from reader, bind independent readback to exact objects. Old backup publication receipts cannot prove current grant-journal custody. | Historical recovery tools concern other records. Native-journal custody producer/integration is missing; offsite access/signing/readback is a separately authorized owner operation. |
| `post-cut-revocations.jsonl` and original signatures — L | Same authoritative revocation source, complete interval after initial cut through an authenticated as-of/latest head. Output every intervening revocation/signer transition and predecessor links. | An empty file requires a signed completeness/empty-interval statement in `native-current-head-attestation.json`, bound to empty-file digest, interval and independently authenticated latest head. No absence-by-search inference. Reject gaps, stale head, relevant effective revocation or unknown state. | Implementation needed for completeness/merge/freshness. Owner operation for authoritative export and signatures. |
| `selector-rights-current.json` and native authorization-owner/reviewer signatures — G | Complete resolved authenticated native grants plus revocation lineage/head and authentication mapping. Output exactly 23 ordered `(ordinal,id,method,path)` entries below, each allow decision and rule/source binding, chosen principal/certificate, target, observed/valid interval, current head/cut and per-step refresh requirement. | Require every decision to cover the exact wire selector, including nonresource `/version`, `/readyz?verbose`, native binding and four named absence GETs. For `/readyz?verbose`, document native nonresource authorization matching of `/readyz` while preserving the exact query on the wire. No list permission, historical success, caller-supplied allow-list or arbitrary self-authorization assertion substitutes. All 23 must independently pass, with current revocations/rights revalidated at every future boundary. | Available: exact fixture equality only. Implementation needed: native rule evaluation and authenticated per-step current-rights/head validation. Owner operation: authoritative policy/rights witness. Do not add SubjectAccessReview requests or discovery to this lane. |

All methods are exactly **GET**. This literal table is transcribed from the
Timesheets intake `ordered-get-selectors.json`, not a new allowlist. Any mismatch
against the sealed order is a stop; sign the complete ordered tuple set.

| Ordinal | Exact id | Exact path |
| --- | --- | --- |
| 1 | namespace-kube-system | `/api/v1/namespaces/kube-system` |
| 2 | namespace-keycloak | `/api/v1/namespaces/keycloak` |
| 3 | namespace-openbao | `/api/v1/namespaces/openbao` |
| 4 | namespace-hexalith-memories | `/api/v1/namespaces/hexalith-memories` |
| 5 | version | `/version` |
| 6 | readyz | `/readyz?verbose` |
| 7 | keycloak-postgres | `/apis/postgresql.cnpg.io/v1/namespaces/keycloak/clusters/keycloak-postgres` |
| 8 | keycloak-keycloak | `/apis/apps/v1/namespaces/keycloak/deployments/keycloak` |
| 9 | openbao-hexalith-keys | `/apis/apps/v1/namespaces/openbao/statefulsets/hexalith-keys` |
| 10 | hexalith-memories-redis-stack | `/apis/apps/v1/namespaces/hexalith-memories/statefulsets/redis-stack` |
| 11 | hexalith-memories-falkordb | `/apis/apps/v1/namespaces/hexalith-memories/statefulsets/falkordb` |
| 12 | hexalith-memories-memories | `/apis/apps/v1/namespaces/hexalith-memories/deployments/memories` |
| 13 | hexalith-memories-memories-mcp | `/apis/apps/v1/namespaces/hexalith-memories/deployments/memories-mcp` |
| 14 | hexalith-memories-memories-access-telemetry | `/apis/apps/v1/namespaces/hexalith-memories/deployments/memories-access-telemetry` |
| 15 | hexalith-memories-memories-access-telemetry-clock | `/apis/apps/v1/namespaces/hexalith-memories/deployments/memories-access-telemetry-clock` |
| 16 | keycloak-service | `/api/v1/namespaces/keycloak/services/keycloak` |
| 17 | openebs-hostpath | `/apis/storage.k8s.io/v1/storageclasses/openebs-hostpath` |
| 18 | openebs-hostpath-retain | `/apis/storage.k8s.io/v1/storageclasses/openebs-hostpath-retain` |
| 19 | native-binding | `/apis/rbac.authorization.k8s.io/v1/clusterrolebindings/kubeadm:cluster-admins` |
| 20 | absent-s40-kcpre-20261015t162756z | `/api/v1/namespaces/s40-kcpre-20261015t162756z` |
| 21 | absent-s40-kc-20261015t162756z | `/api/v1/namespaces/s40-kc-20261015t162756z` |
| 22 | absent-s40-bao-20261015t162756z | `/api/v1/namespaces/s40-bao-20261015t162756z` |
| 23 | absent-s40-mem-20261015t162756z | `/api/v1/namespaces/s40-mem-20261015t162756z` |

No credential-bearing request is needed or permitted to bootstrap owner authority
under this plan. G/L/S/C supply independently authenticated originals or obtain
separate authorization for their evidence-production operations. A future
boundary-current head/rights channel must itself be authenticated, bounded,
reviewed and covered separately; it cannot silently add network selectors.
An unsupported refresh mechanism leaves the lane blocked even if a snapshot passes.

### 4. Runtime, OpenSSL and actual compatibility

| Artifact and role | Authoritative source and exact nonsecret output | Signature, trust, validity and objective acceptance | Tool status |
| --- | --- | --- | --- |
| `runtime-execution-inventory.json` and Q producer/reviewer signatures | Exact approved executable/source/stdlib/native-library and loader provenance from the actual qualification process. Output canonical file identity/custody/size/hash inventories, execution flags, runtime build, nine-library inventory mapping, actual loaded module/library paths/digests, exact provider/config files and sanitized allowlisted environment. Reference sealed inventories without changing them. | Bind actual executed bytes and post-run observations, not merely installed files/version strings. Include every interpreter/helper/loader used; reject unbound provider/library/config/environment drift. Historical 1,337-file/nine-library matching is integrity evidence only. Q's signing role comes from T. | Available: offline inventories/source hash readers. Implementation needed: actual process-loaded dependency and provider instrumentation, sanitized output and signed execution provenance. Owner operation for actual qualification. |
| `openssl-provider-configuration-attestation.json` and Q producer/reviewer signatures | Actual process startup/provider/configuration instrumentation under the exact approved runtime. Output explicit provider names and exact implementation/module bytes (or built-in library binding), configuration contents/digests and loading policy, property selection, disabled ambient/default loading and observed enforcement results. | Bind exact startup ordering before TLS/library initialization. Show ambient OpenSSL config/default trust/automatic provider behavior cannot supersede the explicit profile. Python `ssl` version metadata and later environment clearing cannot prove startup behavior. Require the exact bound library/runtime profile below, validity and revocation/invalidation basis for the signed qualification. If runtime cannot enforce it, stop for implementation/design review, never relax the profile. | Metadata requirements exist; no enforcement/observation producer exists. Implementation needed, potentially an explicitly controlled native bootstrap/runtime mechanism; choice requires Q review. No dependency upgrade or new provider installed here. |
| `tls-peer-client-qualification.json` and Q producer/reviewer signatures | Actual separately authorized controlled qualification under exact runtime/provider, explicit CA/peer and one protected client. Output authorization reference, executed-source/runtime digests, endpoint, CA/leaf/client DER/SPKI, negotiated profile and strict validation results, revocation/head bindings, observation and validity; no private bytes. | Demonstrate actual strict chain, pinned peer SPKI, IP SAN, client pair/identity and profile enforcement. Separate controlled negative fixtures establish rejection of wrong CA/pin/SAN/client, ambient trust/config and changed provider without unapproved production attempts. Actual target observation still required. Bind S/C/G/L evidence; reject mocks as target qualification. Authorization must precede any private access/handshake. | `_connect` is dormant mechanics checking only generic SSL context properties. Full profile enforcement, credential bridge, peer pin verification, revocation binding and qualification producer are missing. Owner operation for every actual handshake. |
| `http-response-compatibility.json` and Q producer/reviewer signatures | Actual separately authorized bounded validation on all exact 23 selectors with the qualified runtime/TLS/client. Output one ordered sanitized row per selector: identity tuple, status, exact Content-Length, observed header/body byte counts, media type, absence of prohibited encodings/redirect/pagination, typed sanitizer result/schema version, actual timing, authorization and prerequisite digests. | Require 200 for selectors 1–19 and exact typed Kubernetes NotFound/404 for 20–23. A generic 404, 403 or missing response fails. Require readyz `text/plain` healthy schema; others `application/json` and the exact current sanitizer shapes, UIDs/generation/readiness/image digest/storage rules where applicable. Pass only if actual behavior fits every unchanged bound; chunked/missing length/oversize/alternate schema stops. Never fix a mismatch by silent fallback or retry. | Available: strict dormant HTTP parser and typed sanitizers, synthetic tests. Implementation needed: authorized qualification orchestration and sanitized header/length/timing evidence; current `Response` retains only status/type/chunks, losing header provenance. Owner operation for actual requests, unavailable here. |

The sealed **required** runtime profile is `/usr/bin/python3.14 -I -S -B`,
OpenSSL string `OpenSSL 3.5.5 27 Jan 2026`, version tuple `[3,5,0,5,0]`, TLS 1.3
minimum and maximum, `CERT_REQUIRED`, `VERIFY_X509_STRICT`, hostname checking
with IP SAN `192.168.1.30`, explicit CA-only chain and peer SPKI pin, exactly one
static client certificate/key pair and ALPN `http/1.1`. Default trust, default
OpenSSL configuration, environment configuration, automatic provider loading,
compression, renegotiation and session resumption are disabled requirements.
They are **unqualified requirements**, not claims that the current interpreter
or dormant `_connect` implements/enforces them. Peer pin complements chain/IP
validation; it never replaces them. No provider name/configuration is invented.

Fixed limits: one attempt per selector, 23 requests maximum, 15 seconds/request
including connection/handshake/send/read, 300 seconds/lane, 8 KiB headers,
256 KiB/response, 4 MiB total response bodies, 64 KiB final receipt, 64 items and
one page maximum. Require exact Content-Length. No DNS, proxy, redirect, retry,
credential fallback, transfer encoding, content encoding, pagination, discovery,
arbitrary command or expanded selector scope. The receipt producer must retain
only bounded typed metrics/results; secret/raw diagnostics stay outside intake.

### 5. Preservation disposition and conditional later records

| Artifact and role | Authoritative source and exact nonsecret output | Signature, trust, validity and objective acceptance | Tool status |
| --- | --- | --- | --- |
| `preservation-drift-disposition.json` and P producer/reviewer signatures | Original immutable preservation inventories, preserved refusal/exception, full local commit provenance and owners of actual changes. Output every old/new hash below, corresponding full commits/file revisions, explicit prior Story 1.7 and additional Story 1.8 dispositions, author/owner attribution or UNIDENTIFIED, preservation impact and reviewer reasoning. | T must authenticate P's disposition mandate. Explain and disposition each transition separately, preserve originals and the exit 1. An accepted explanation cannot make the old verifier pass. Any future reconciliation uses a new separately reviewed exact revision, with explicit ancestor inventories; no baseline overwrite or new exception in the sealed packet. | Available: offline digest/custody checks and recorded Git provenance. Implementation needed: authenticated disposition ingestion and separate additive revision validation. Owner operation: owner accountability/disposition and signatures. No historical hash/Git sweep rerun here. |
| `activation-revision-manifest.json` — A, conditional future only | Exact final Platform source/runtime/owner artifact set after all substantive and preservation gates pass. Output member digests, explicit predecessor manifests, one identity, ordered selectors, every witness, output-custody binding, all limits/stop conditions/unchanged ceiling and authorized scope. | Authenticated A signature and independent review required. Preserve sealed v4 and original manifests; do not insert real evidence into fixture schemas. Full independent review must cover exact final bytes and preservation solution. No activation revision is produced by this plan. | Implementation needed for a production evidence/activation contract; owner operation for authorization/signing. Existing verifier enforces closed/null synthetic state, not activation acceptance. |
| `activation-independent-review.json` and independent A-reviewer signature — A, conditional future only | Independent human activation review after prerequisites pass. Output reviewed exact manifest/source/runtime/artifact hashes, trust/rights/custody/freshness findings, reviewer identity/authority/independence and explicit unresolved findings/disposition. | Must reference a specific passing exact revision and current authority evidence. Agent review of this plan does not count. Unresolved prerequisites, expiry or refused exact revision preclude readiness. | No current authenticated review. Review-verification implementation and separately mandated owner review required. |
| `bounded-operational-approval-record.json` and Administrator signature — A, conditional future only | A later separately authenticated bounded decision, after prerequisite completion and exact activation review. Output exact approved revision/source/runtime/identity, scope, custody, limits, validity/ceiling and first-failure/unknown stops. | Independent reviewer verifies approval authority and signature via T. It is distinct from native grants, qualification authorization and plan review. No prompt, executable command, approval record or delegated signing is emitted here. | No current applicable record or operational authorization; separate owner decision required. |

Preservation hash/provenance ledger (all historical observations, not newly
measured current hashes):

| Transition | Recorded sprint-status SHA-256 | Evidence meaning |
| --- | --- | --- |
| Original supplement inventory | `1d66b846fb554deefa67acb44711671376d5d354f74558ffb9b34a87a638283d` | Original preserved status anchor. |
| Prior Story 1.7 drift / pre-session baseline | `2d0c7c85dc259481180cd9c65fb327c32706e9ddbfa0c1a01fcd848da2d2d41e` | Sealed revision README attributes original whole-verifier refusal to committed `1711deba08a4e66b46acfff8d08e7f2ad7f74ba1`; owner must supply complete change attribution/disposition. |
| Sealed concurrent exception | `f6c25046d9db40c069cb183cf87f8fef19a0a952b601c87dbe690d0b0aff7e43` | Exact immutable `concurrent-preservation-drift-v1.json` binds full commit `114e08830da2d8cca0e77cd870b8867506b4c68b`, 12,179 bytes, uid 1000/mode 0644, and `initiatedByThisTask=false`. Preserve it verbatim. |
| Additional Story 1.8 intake observation | `c94f8fedcc4d9e362985c3056c2a2b3e6628ef74b525ed377b92f7cf920a12c5` | Intake observes `backlog` to `in-progress` beyond that exception, at Platform `9256c675186b020929d67230a22892bb13488bc2` and Timesheets `ee95f90f529812c744761090691a23196c971c78`. Exact verifier exits 1. |

Current HEAD is separately recorded above. P must include any later intervening
commits/concurrent changes in its eventual disposition rather than assuming the
intake observation still describes the working tree. That remaining provenance
production is owner work, not an assertion that this plan revalidated history.

Preserve both preparation directories
`/home/administrator/hexalith-recovery-evidence/4-0/20261015t162756z` and
`/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision`,
the sealed revision, original supplement, immutable packets, signed originals,
expired `20261014t112353z`, original modes, failures and concurrent changes.
The expired recovery's original deadline stays **2026-10-15T11:40:42Z**.
No restore, retirement, upgrade, deployment or replacement recovery follows from
this plan. No write is made to either preparation directory, source adapter,
existing story/spec, sprint-status, index or gitlink.

## Additional unchanged witness obligations

The following proposed artifact names make the remaining sealed obligations
explicit; they do not expand the 23 selectors or claim these artifacts exist.
The common signature/trust/validity/revocation contract applies to every row.

| Artifact and role | Authoritative source and exact nonsecret output | Signature, trust, validity and objective acceptance | Tool status |
| --- | --- | --- | --- |
| `break-glass-custody-attestation.json` — C and G jointly; independent C/G reviewers | Independent native recovery/access custodian's authorized custody and recovery records. Output named independent custodian, native authority/principal/public identity, custody/dependency inventory digest, KubeSphere-independent recovery mechanism and exact current evidence references. | Custodian and native-authority signatures plus independent reviewer signatures must establish actual custody and independence without private bytes. Current recovery/access proof cannot be inferred from old recovered use; missing proof stops. T must establish both signing purposes. | Synthetic `breakGlass` witness exists only. Production verification/integration needs implementation; any actual access/recovery qualification is a separately authorized owner operation, outside the GET lane. |
| `cnpg-controller-current-attestation.json` — Q with native infrastructure owner UNIDENTIFIED; independent Q reviewer | Authoritative native infrastructure owner records/current separately authorized observation. Output exact current controller name/UID, source record digest, current deployment/watch configuration and exact watch namespace list `keycloak,cnpg-system`, target and observation interval. | Infrastructure owner and reviewer signatures via T; establish actual current identity/watch configuration, not the historical approved string. No current controller selector is supported; no extra request is added. Unknown/changed identity or watch scope stops. | Only fixture witness/string reference exists; sanitizer for that selector remains unsupported. Owner operation plus signed evidence validation needed; no controller change/observation here. |
| `output-custody-attestation.json` — Q/C; independent custody reviewer UNIDENTIFIED | Custody owner's protected output design and authorized actual custody verification. Output final proposed revision's exact root identity/owner/mode/device/inode, absent child/slot preconditions, one reservation, bounded success/failure filenames, modes, byte bound, failure retention and exclusive single result. | Custody owner and reviewer signatures bind exact reviewed revision, runtime and public identity. Require no symlinks/unsafe ancestors or collision, directories 0700/files 0600, 64 KiB result and one attempt. Original sealed `measurement-output` stays absent; any eventual operational root/slot is separately reviewed and authorized, never populated by this plan. | Scratch-only `SyntheticOutputCustody` is available. Actual protected reservation/retention verification and consumer integration need implementation; checking/creating operational custody is an owner operation. |
| Proposed `qualification-authorization.json` — A; independent A reviewer | Separately authenticated Administrator qualification decision before private access/handshake/requests. Output exact qualification source/runtime/provider, one client, target, delivery/custody lifecycle, exact 23 selectors, limits, freshness/unchanged ceiling, stops and permitted observations. | Original Administrator signature and independent reviewer verification via T. This authorizes only its exact qualification operation; it is distinct from later bounded measurement/activation approval. No decision/prompt is supplied or requested here. | No applicable current authorization supplied. Needs an owner decision and authenticated verification; this row is a future prerequisite, not execution permission. |

I3 must define the exact consumer lifetime and single-consumption delivery event
before qualification. A consumed qualification delivery receipt cannot be
replayed as delivery to a second activation consumer. The same chosen public
identity must remain bound throughout; no second credential, fallback, recovery
allocation or unreviewed re-enrollment is inferred. If the proposed lifetime
cannot satisfy qualification and later readiness without violating one-use
custody, stop and leave activation blocked for a separately reviewed design.

## Concrete tooling gaps and proposed implementation order

These paths are **proposed future Platform work**, not files implemented or an
authorization to implement them. Existing sealed sources remain reference bytes;
any later source change requires additive binding/review, not an updated old seal.

| Order | Existing boundary / concrete gap | Proposed owned file/action | Objective future check |
| --- | --- | --- | --- |
| I1 | `native_enrollment_execution.py:_validate_enrollment` accepts `fixtureOnly`, fixture provenance and booleans; it performs no public-key validation. `verify_enrollment_revision.py` verifies bytes and enforces closed/null state. | `eng/recovery-preparation/owner_evidence_contracts.py` and `verify_owner_evidence.py`: bounded original-byte schemas, signature framing/algorithms, role trust/delegation, certificate chains, validity/revocation and independent bootstrap/reviewer records. No secret/network loading in offline verifier. | Given a forged/self-supplied root, wrong-purpose signer, altered original, stale/revoked signer, missing reviewer or fixture-only witness, verification refuses. Authentic synthetic cryptographic fixtures can test validation but never establish real owner authority. |
| I2 | Native binding sanitizer emits historical shape/current authority false. `_rights()` compares an inert allow-list. `_Boundaries.check` repeats fixture times, not authenticated latest-head/revocation resolution. | `eng/recovery-preparation/verify_native_authority.py`: complete native role/subject/rule resolution, original journal/signature verification, independent latest-head/anti-rollback proof and complete post-cut revocations, exact ordered 23-right decisions. Define a separate authorized freshness-input contract before any live lane. | Given a missing predecessor, signer transition, aggregated rule, stale/replayed head, omitted post-cut revoke or any reordered/missing GET, verification refuses; no external authority input can expand lane selectors. |
| I3 | `SyntheticCredentialDelivery` consumes inert prefix bytes; no protected real channel, certificate/key pair validation or replay-proof enrollment exists. `SyntheticOutputCustody` is scratch-only. | `eng/recovery-preparation/protected_enrollment.py`: separately reviewed design for one protected pair, public-only attestation binding, exclusive one-use custody and create-only output reservation; keep current public live functions closed until separately authorized exact activation. | Given duplicate consumption, wrong recipient/pair/public identity, unsafe custody, slot collision or expired evidence, refuse before request. A future secure implementation must show real pair validation without private output; no real credential is used for tests here. |
| I4 | `tls_requirements()` is metadata only. `_connect` checks hostname/CERT_REQUIRED but does not establish TLS 1.3-only, strict flags, CA-only/pin, controlled OpenSSL startup/provider/config, ALPN or resumption policy. | `eng/recovery-preparation/runtime_qualification.py` plus reviewed runtime/bootstrap design: observe/enforce exact approved loaded libraries/providers/config before TLS initialization, strict TLS profile and public-only qualification evidence. No implicit ambient setup. | Given wrong pin/CA/SAN/TLS/provider/config/environment or unknown actual loading, refuse. Actual target qualification is separate authorized owner work after implementation and review; do not infer it from mocks/hashes. |
| I5 | `_exchange` checks strict HTTP framing but returns `Response(status, content_type, chunks)` without header counts/original length/timing provenance. No actual 23-response qualification producer exists. | Extend a separately bound future transport revision and `runtime_qualification.py` to emit bounded typed framing/timing/schema evidence and one-pass exact-selector qualification. Keep failure/unknown and unsupported behaviors closed. | Given chunked/missing or conflicting length, forbidden encoding, redirect, 404/403 ambiguity, schema drift, overflow or timeout, stop without retry; all 23 actual rows must fit. Qualifier and receipt sink must preserve failures without raw diagnostics. |
| I6 | No authenticated drift-disposition intake, additive activation manifest or exact revision review/approval verifier exists; old integrity refusal is permanent evidence. | `verify_owner_evidence.py` and a separately designed additive activation-revision contract: validate P's disposition and full ancestry, all ten enrollment witness obligations and all 31 v4 prerequisites, independently reviewed exact revision and distinct approval. | Given explained drift alone, updated caller hash, null/fixture witness, absent prerequisite or expiry, keep gate closed. Never teach the old verifier to accept the additional drift or overwrite its exception. |

Future implementation requires its own scoped work and meaningful negative tests;
this documentation continuation runs no old suite or implementation. Available
strict parsers, no-follow file readers, fixed selector construction, monotonic/
UTC checks and synthetic create-only custody are reuse candidates, not proof of
production readiness. No ready-for-development sufficiency claim is made while
external evidence, owner assignments and implementation gaps remain unresolved.

## Dependency order and objective acceptance

1. **Clock and immutable boundary:** before dependent work, check actual UTC
   against the fixed ceiling and every shorter validity bound. Given expiry,
   clock regression, identity/source drift or unknown state, stop that candidate's
   dependent work; preserve all originals and finish only historical planning.
2. **T first:** identify/authenticate accountable humans and independent reviewers,
   return `public-trust-roots/` and signed `trust-root-establishment.json` with
   independent establishment evidence. Given only the current digest or a new
   self-asserted key, acceptance fails. I1 must exist and be independently reviewed
   before any automated authority acceptance.
3. **P can proceed alongside public identity production after T:** produce signed
   preservation disposition for every transition and later provenance. Given that
   disposition, the original exact result must still remain exit 1; only a later
   separately reviewed additive revision can have its own integrity conclusion.
4. **S and C public evidence:** after applicable root establishment, authenticate
   trust source, exact CA/chain, current peer and exactly one client/mapping. Given
   a missing pin, identity, issuer, valid interval or revocation source, stop.
   No real delivery occurs simply because a public client certificate is accepted.
5. **G and L:** resolve native grants and full lineage, independent current head,
   custody and all post-cut revocations, then all 23 rights for that one identity.
   Given stale/incomplete/unknown lineage or one denied selector, stop. I2's
   verifier and authorized per-step freshness mechanism are prerequisites.
6. **Runtime and custody preparation:** Q binds the exact runtime/provider/config,
   C and its reviewer establish the protected delivery design and output custody;
   implement/review I3–I5 separately. Owner authorization for qualification must
   identify exact qualification code/runtime, endpoint, one identity, protected
   delivery, 23 selectors, custody, limits, unchanged ceiling and stops. This
   qualification authorization is neither inferred nor requested here.
7. **Authorized owner production only:** after preceding gates and applicable
   authorization, C may produce actual protected delivery/pair evidence; Q may
   produce actual loaded-provider/TLS/HTTP qualification evidence. Given a missing
   authorization, incompatible actual response or any failure, stop without
   retries or altered selectors/profile. Existing already-produced originals are
   usable only if their exact bindings, authority and freshness satisfy these
   gates. Qualification evidence must not be confused with a measurement receipt
   or authority to perform later recovery/retirement/deployment.
8. **Remaining sealed obligations:** independently authenticate break-glass custody,
   current CNPG controller identity/watch scope and output-custody obligations;
   verify all ten witness obligations and all 31 v4 prerequisites. This plan does
   not narrow those prerequisites to its five requested evidence groups. Detailed
   node, pod/init-image, PVC, PV and current CNPG-controller selector families stay
   unsupported: current controller evidence needs separately authorized owner
   records, not an added selector. Missing evidence leaves the gate closed.
9. **Conditional exact activation review:** only after substantive evidence,
   preservation and implementation pass, and strictly before expiry, A may
   prepare the exact additive activation revision and obtain authenticated
   independent review. Only after that review passes could a bounded operational
   approval prompt be considered. There is no such prompt or activation here.
10. **Timesheets acceptance boundary:** X receives nonsecret original owner
    artifacts, signatures, independent verification/disposition and exact Platform
    revision references in a separate Timesheets intake. X records unresolved IDs
    and acceptance; Platform's plan/review cannot close Story 3.6 or release gates.
    Later deployed release/provider/inventory/continuity/subscriber/shared-rebuild
    acceptance remains outside this evidence plan and remains unmet.

Given no newly supplied authenticated owner evidence, this handoff passes its
**documentation** goal only when every requested artifact has an owner role,
independent reviewer, authoritative source, exact output, trust/signature/
validity/revocation rule, tool classification, dependency and objective criterion,
and an independent plan review has been recorded. That does not pass operational
prerequisites. The first owner deliverable is T's public trust/authority package,
not a credential, new grant, signing unlock or live request.

## Scope, preservation and review record

Only new planning/review documents in this create-only Platform handoff directory
are authorized writes (plus bmad-build's local rendered instruction cache). Drafts
and review findings are preserved; a corrected plan uses a new filename. No
existing packet, sealed exception, preparation file, signed original, expired
recovery, failure, concurrent change, source adapter, Timesheets acceptance file,
index or gitlink is modified by this task. There is no new full-byte preservation
claim. Read-only source inspection is not repeated evidence validation.

No actual kubeconfig/private credential read, authentication, network/API/provider/
IAM request, preflight replay, signing, publication, infrastructure change,
capture, restore, retirement, upgrade, deployment, domain write, staging, commit,
push or dependency update is authorized or performed. First failure/unknown,
tampering, rights loss, expiry, clock regression, drift, timeout, overflow and
collision stop dependent work. No old approval/delegation expands this scope.

The independent review and any corrections are recorded separately beside the
final plan. They assess documentation completeness and source-supported claims;
they authenticate no owner, certificate, grant, live behavior or activation.

**Spec-23 stays draft; Story 3.6/Epic 3/Platform 4.27 stay in progress;
4.1 operational gate closed; release FAIL.** Existing story/spec/sprint files
are deliberately not edited or promoted by this planning result.
