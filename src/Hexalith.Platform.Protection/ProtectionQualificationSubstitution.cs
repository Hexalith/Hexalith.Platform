using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>One source-owned actual negative request; a mutation label cannot choose an operation or expected result.</summary>
public sealed record ProtectionQualificationSubstitution(string Mutation, DeletionBatchConsumptionRequest Request);
