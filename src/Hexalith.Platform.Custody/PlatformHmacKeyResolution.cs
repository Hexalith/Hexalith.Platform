namespace Hexalith.Platform.Custody;


/// <summary>A content-free result; successful snapshots belong to the caller and must be disposed.</summary>
/// <param name="Status">The safe resolution outcome.</param>
/// <param name="Key">The disposable key capability, absent on failure.</param>
public sealed record PlatformHmacKeyResolution(CustodyStatus Status, PlatformHmacKeySnapshot? Key = null);
