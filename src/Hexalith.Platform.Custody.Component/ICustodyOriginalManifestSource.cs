namespace Hexalith.Platform.Custody.Component;

/// <summary>Reads an already admitted manifest. It does not create or reorder one.</summary>
internal interface ICustodyOriginalManifestSource
{
    /// <summary>Returns the admitted original, or null when this reservation was not admitted.</summary>
    CustodyOriginalManifest? FindOriginal(string reservationId);
}
