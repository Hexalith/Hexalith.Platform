using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Exact already installed independent binding state locator/anchor scope. No initial record is manufactured from this descriptor.</summary>
public sealed record CustodyInteractionBindingResource(string Component,string Key,string OwnerScope,ICustodyOwnerStateAuthority Authority,Dictionary<string,string>? Metadata=null);
