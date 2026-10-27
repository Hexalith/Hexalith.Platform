using System.Globalization;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>The supported OpenBao secret selector is an immutable positive canonical native version.</summary>
internal static class QualificationNativeVersion
{
    internal static bool IsPinned(string? value)=>ulong.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out ulong version)
        && version>0 && version.ToString(CultureInfo.InvariantCulture)==value;
}
