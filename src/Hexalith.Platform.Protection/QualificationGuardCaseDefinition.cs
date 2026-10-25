using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Exact accepted case input, including an optional concurrent trigger boundary. These are real owner requests, never expected metrics.</summary>
public sealed record QualificationGuardCaseDefinition(string CaseId, string TenantId, string BatchId, string EnrolledScopeSha256,
    IReadOnlyList<QualificationGuardStep> Before, QualificationGuardStep Effect, QualificationGuardStep? Trigger,
    string Boundary, IReadOnlyList<QualificationGuardStep> After);
