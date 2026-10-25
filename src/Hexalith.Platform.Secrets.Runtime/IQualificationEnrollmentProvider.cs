using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent isolated-target enrollment and current ACL verifier.</summary>
public interface IQualificationEnrollmentProvider : IQualificationRoleProvider;
