using System.Text.Json;

using Hexalith.Platform.Secrets.Qualification.Observer;
using Hexalith.Platform.Secrets.Runtime;

var journal = new ContentFreeObserverJournal();
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
SecretsRuntimeConfiguration configuration = args.Length > 0 ? SecretsRuntimeConfiguration.Load(args[0]) ?? new SecretsRuntimeConfiguration() : new SecretsRuntimeConfiguration();
app.MapPost("/ext-secrets-1/v1/observe", () => Observe(configuration, journal));
app.Run();

static IResult Observe(SecretsRuntimeConfiguration configuration, ContentFreeObserverJournal journal)
{
    int status = ObserverEndpoint.Observe(configuration, journal);
    QualificationDenial denial = QualificationDenial.Evaluate(configuration);
    string json = JsonSerializer.Serialize(new { code = "DependencyNotAvailable", dependency = "EXT-SECRETS-1", missingInputs = denial.MissingInputs, qualificationEffects = 0, productionMutationAllowed = false });
    return Results.Text(json, "application/json", statusCode: status);
}
