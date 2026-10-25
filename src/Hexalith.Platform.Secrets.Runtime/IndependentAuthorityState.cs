using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Externally enrolled independent authority genesis/current state. Startup never creates this document.</summary>
public sealed record IndependentAuthorityState(string EnrollmentId, string Scope, string TrustRevision,
    long Revision, DateTimeOffset ValidUntil, IndependentCallerGrant[] Callers,
    ReplicatedSecuritySpoolTarget? Spool, long SpoolRevision, string SpoolDigest,
    Dictionary<string, AnchoredStateTransition> Admitted, Dictionary<string, AnchoredStateTransition> Recorded,
    Dictionary<string, SecurityObservationRecoveryGrant> Recoveries, Dictionary<string, IndependentOriginal> Originals,
    Dictionary<string, CustodyKeyRegistration> Registrations, Dictionary<string, DeletionBatchConsumptionRequest> Manifests,
    Dictionary<string, string>? QualificationBindings = null,
    Dictionary<string, JsonElement>? QualificationCases = null);
