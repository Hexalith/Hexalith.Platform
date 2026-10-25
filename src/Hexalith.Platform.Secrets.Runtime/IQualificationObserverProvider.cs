using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent persisted-owner reader; it must have no executor or signing-request permission.</summary>
public interface IQualificationObserverProvider : IQualificationRoleProvider;
