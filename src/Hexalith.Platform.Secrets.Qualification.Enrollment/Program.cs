using System.Text.Json;

using Hexalith.Platform.Secrets.Runtime;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
SecretsRuntimeConfiguration configuration = args.Length > 0 ? SecretsRuntimeConfiguration.Load(args[0]) ?? new SecretsRuntimeConfiguration() : new SecretsRuntimeConfiguration();
app.MapPost("/ext-secrets-1/v1/enroll", () => Deny(configuration));
app.Run();

static IResult Deny(SecretsRuntimeConfiguration configuration)
{
    QualificationDenial denial = QualificationDenial.Evaluate(configuration);
    string json = JsonSerializer.Serialize(new { code = "DependencyNotAvailable", dependency = "EXT-SECRETS-1", missingInputs = denial.MissingInputs, qualificationEffects = denial.QualificationEffects, productionMutationAllowed = false });
    return Results.Text(json, "application/json", statusCode: denial.StatusCode == 200 ? StatusCodes.Status503ServiceUnavailable : denial.StatusCode);
}
