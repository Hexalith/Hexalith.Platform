using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Executor installation; observer and enrollment installations cannot satisfy this port.</summary>
public interface IQualificationExecutorProvider : IQualificationRoleProvider;
