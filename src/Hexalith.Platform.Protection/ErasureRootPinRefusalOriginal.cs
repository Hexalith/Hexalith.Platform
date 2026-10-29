namespace Hexalith.Platform.Protection;

/// <summary>Exact immutable manifest request digest and native refusal vector.</summary>
public sealed record ErasureRootPinRefusalOriginal(string RequestDigest, ErasureRootPinRefusal[] Refusals);
