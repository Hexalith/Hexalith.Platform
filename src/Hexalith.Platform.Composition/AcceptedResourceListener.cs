namespace Hexalith.Platform.Composition;

// Public installation binding; this carries no credential and supplies no port default.
internal sealed record AcceptedResourceListener(string Role,string Address,int Port,string EndpointName)
{
    internal string Url=>"http://"+Address+":"+Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
