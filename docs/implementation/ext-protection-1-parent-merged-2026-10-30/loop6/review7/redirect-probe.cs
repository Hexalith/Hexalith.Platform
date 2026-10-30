using System.Net;
using System.Net.Sockets;
int Port() { using var p = new TcpListener(IPAddress.Loopback, 0); p.Start(); return ((IPEndPoint)p.LocalEndpoint).Port; }
int first=Port(), second=Port(); using var a = new HttpListener(); using var b = new HttpListener(); a.Prefixes.Add($"http://127.0.0.1:{first}/"); b.Prefixes.Add($"http://127.0.0.1:{second}/"); a.Start(); b.Start();
var redirect = Task.Run(async () => {var x=await a.GetContextAsync();x.Response.StatusCode=302;x.Response.RedirectLocation=$"http://127.0.0.1:{second}/foreign";x.Response.Close();});
var destination = Task.Run(async () => {var x=await b.GetContextAsync(); bool received=x.Request.Headers["X-Vault-Token"]=="synthetic-review-value";x.Response.StatusCode=204;x.Response.Close();return received;});
using var client = new HttpClient { BaseAddress=new Uri($"http://127.0.0.1:{first}/"),Timeout=TimeSpan.FromSeconds(10)};using var request=new HttpRequestMessage(HttpMethod.Get,"v1/heads/data/tenants/tenant-a");request.Headers.Add("X-Vault-Token","synthetic-review-value");using var response=await client.SendAsync(request);await redirect;Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {DefaultHandlerCrossOriginRedirect=true,CustomNativeBearerForwarded=await destination,Status=(int)response.StatusCode}));
