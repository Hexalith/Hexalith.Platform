using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Commands;

public sealed class IdempotencyKeyProtector(IIdempotencyDigestKeyProvider keyProvider)
{
	private static readonly byte[] _tenantDomain = "hexalith-eventstore-idempotency-tenant-v1\0"u8.ToArray();

	private static readonly byte[] _keyDomain = "key-partition-v1\0"u8.ToArray();

	private static readonly byte[] _verificationDomain = "key-verification-v1\0"u8.ToArray();

	private static readonly byte[] _intentDomain = "canonical-intent-v1\0"u8.ToArray();

	private readonly IIdempotencyDigestKeyProvider _keyProvider = keyProvider ?? throw new ArgumentNullException("keyProvider");

	public async ValueTask<IdempotencyProtectedIdentitySet> ProtectAsync(string tenant, string rawKey, TrustedIdempotencyDescriptor descriptor, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenant, "tenant");
		ArgumentException.ThrowIfNullOrWhiteSpace(rawKey, "rawKey");
		ArgumentNullException.ThrowIfNull(descriptor, "descriptor");
		if (string.IsNullOrWhiteSpace(descriptor.AdapterId) || string.IsNullOrWhiteSpace(descriptor.OperationId) || descriptor.DescriptorVersion <= 0 || !Enum.IsDefined(descriptor.RetentionTier))
		{
			throw new InvalidOperationException("The trusted canonical idempotency descriptor is invalid.");
		}
		if (descriptor.CanonicalIntent == null || descriptor.CanonicalIntent.Length == 0)
		{
			throw new ArgumentException("Canonical intent bytes are required.", "descriptor");
		}
		using IdempotencyDigestKeyRing idempotencyDigestKeyRing = await _keyProvider.GetKeyRingAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		List<IdempotencyProtectedIdentity> list = new List<IdempotencyProtectedIdentity>(idempotencyDigestKeyRing.Versions.Count);
		foreach (string version in idempotencyDigestKeyRing.Versions)
		{
			byte[] array = idempotencyDigestKeyRing.RentKeyMaterial(version);
			byte[] array2 = null;
			byte[] array3 = null;
			byte[] array4 = null;
			try
			{
				array2 = Encoding.UTF8.GetBytes(tenant);
				array3 = ComputeHmac(array, _tenantDomain, array2);
				array4 = Encoding.UTF8.GetBytes(rawKey);
				string keyDigest = Base64Url(ComputeHmac(array3, _keyDomain, array4));
				string verificationTag = Base64Url(ComputeHmac(array3, _verificationDomain, array4));
				string intentDigest = Base64Url(ComputeHmac(array3, _intentDomain, descriptor.CanonicalIntent));
				string actorId = IdempotencyAdmissionActorIdentity.Build(tenant, version, keyDigest);
				list.Add(new IdempotencyProtectedIdentity(actorId, tenant, version, keyDigest, verificationTag, intentDigest, descriptor.RetentionTier));
			}
			finally
			{
				CryptographicOperations.ZeroMemory(array);
				if (array2 != null)
				{
					CryptographicOperations.ZeroMemory(array2);
				}
				if (array3 != null)
				{
					CryptographicOperations.ZeroMemory(array3);
				}
				if (array4 != null)
				{
					CryptographicOperations.ZeroMemory(array4);
				}
			}
		}
		return new IdempotencyProtectedIdentitySet(list[0], list.AsReadOnly());
	}

	private static byte[] ComputeHmac(byte[] key, byte[] domain, byte[] value)
	{
		byte[] array = new byte[domain.Length + value.Length];
		Buffer.BlockCopy(domain, 0, array, 0, domain.Length);
		Buffer.BlockCopy(value, 0, array, domain.Length, value.Length);
		byte[] result = HMACSHA256.HashData(key, array);
		CryptographicOperations.ZeroMemory(array);
		return result;
	}

	private static string Base64Url(byte[] value)
	{
		return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-')
			.Replace('/', '_');
	}
}
