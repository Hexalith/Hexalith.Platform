namespace Hexalith.Platform.Custody;

/// <summary>Closed dedicated export-key uses; offline signature verification never reads private material.</summary>
public enum ExportQualificationMaterialUse
{
    /// <summary>Fresh exact export wrapping.</summary>
    Wrap,
    /// <summary>Exact original export unwrap verification under a healthy retained version.</summary>
    Unwrap,
    /// <summary>Fresh dedicated export-manifest signature.</summary>
    Sign,
}
