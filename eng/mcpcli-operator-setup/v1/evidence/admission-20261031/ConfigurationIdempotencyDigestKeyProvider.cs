using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Hexalith.EventStore.Server.Configuration;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;

public sealed class ConfigurationIdempotencyDigestKeyProvider(IOptions<IdempotencyAdmissionOptions> options) : IIdempotencyDigestKeyProvider
{
	private readonly IdempotencyAdmissionOptions _options = options?.Value ?? throw new ArgumentNullException("options");

	public ValueTask<IdempotencyDigestKeyRing> GetKeyRingAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (!_options.Enabled || _options.DigestKeySource != IdempotencyDigestKeySource.Configuration)
		{
			throw new InvalidOperationException("The configured idempotency digest key ring is unavailable.");
		}
		Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>(StringComparer.Ordinal);
		try
		{
			foreach (string item in new string[1] { _options.ActiveDigestKeyVersion }.Concat(_options.ReaderDigestKeyVersions))
			{
				if (!_options.DigestKeys.TryGetValue(item, out string value))
				{
					throw new InvalidOperationException("The configured idempotency digest key ring is unavailable.");
				}
				dictionary.Add(item, Convert.FromBase64String(value));
			}
			return ValueTask.FromResult(new IdempotencyDigestKeyRing(_options.ActiveDigestKeyVersion, dictionary, _options.ReaderDigestKeyVersions));
		}
		catch (FormatException)
		{
			throw new InvalidOperationException("The configured idempotency digest key ring is invalid.");
		}
		finally
		{
			foreach (byte[] value2 in dictionary.Values)
			{
				CryptographicOperations.ZeroMemory(value2);
			}
		}
	}
}
