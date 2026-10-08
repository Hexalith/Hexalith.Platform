namespace Hexalith.Platform.Custody;

/// <summary>Closed safe delivery lookup states; unknown/unavailable never authorize another release.</summary>
public enum ExportKeyDeliveryState
{
    /// <summary>Immutable direct principal delivery is independently confirmed.</summary>
    Delivered,
    /// <summary>The exact original attempt definitively delivered no bytes.</summary>
    NotDelivered,
    /// <summary>A durable reservation exists but its transport effect is unresolved.</summary>
    Unknown,
    /// <summary>Current lookup/backend/authority is unavailable.</summary>
    Unavailable,
    /// <summary>The same delivery id has different immutable fields.</summary>
    Conflict,
}
