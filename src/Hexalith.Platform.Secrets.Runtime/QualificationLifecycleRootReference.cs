namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Explicit exact existing canary root secret protocol. Loading it creates no key, version, native admission or holder claim.</summary>
public sealed record QualificationLifecycleRootReference(string Component,string Key,string Version,string Field);
