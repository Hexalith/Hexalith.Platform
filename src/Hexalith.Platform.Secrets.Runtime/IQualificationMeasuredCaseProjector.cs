using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent public projection of measured owner reads. No client expected outcomes or metrics are arguments.</summary>
public interface IQualificationMeasuredCaseProjector
{
    /// <summary>Names any missing concrete source reader or installed measurement protocol before execution.</summary>
    IReadOnlyList<string> MissingInputs(string caseId);
    /// <summary>Projects only the actual original source operation and four verified independent reads.</summary>
    JsonObject? Project(string role,JsonObject original,JsonObject observations);
}
