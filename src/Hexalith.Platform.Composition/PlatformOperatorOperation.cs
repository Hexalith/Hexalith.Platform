namespace Hexalith.Platform.Composition;

/// <summary>One internal command the operator relay submits, as declared by the selected routing catalog.</summary>
/// <param name="Domain">The declared command domain.</param>
/// <param name="CommandType">The declared command discriminator.</param>
/// <param name="Scope">The declared platform scope, sent as the command tenant and status scope.</param>
/// <param name="Permission">The exact human permission the submission requires.</param>
/// <param name="ReadPermission">The exact human permission the explicit status scope requires.</param>
internal sealed record PlatformOperatorOperation(string Domain, string CommandType, string Scope, string Permission, string ReadPermission);
