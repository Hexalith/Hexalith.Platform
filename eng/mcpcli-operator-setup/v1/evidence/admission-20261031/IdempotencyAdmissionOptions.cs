using System;
using System.Collections.Generic;

namespace Hexalith.EventStore.Server.Configuration;

public sealed record IdempotencyAdmissionOptions
{
	public bool RequireLegacyInventory { get; init; }

	public bool Enabled { get; init; }

	public string ActiveDigestKeyVersion { get; init; } = string.Empty;

	public List<string> ReaderDigestKeyVersions { get; init; } = new List<string>();

	public IdempotencyDigestKeySource DigestKeySource { get; init; }

	public string DigestKeySecretStoreName { get; init; } = string.Empty;

	public string DigestKeySecretName { get; init; } = string.Empty;

	public string DigestKeySecretGeneration { get; init; } = string.Empty;

	public Dictionary<string, string> DigestKeys { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
