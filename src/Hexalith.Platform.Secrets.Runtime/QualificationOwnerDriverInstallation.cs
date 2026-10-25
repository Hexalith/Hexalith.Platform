using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Explicit owning-service resource/trust inputs. Loading never enrolls a key, case, role or resource.</summary>
public sealed record QualificationOwnerDriverInstallation(string ReceivingInstallationPath,QualificationOwnerRole Role,string RoleSigningKeyFile,
    IndependentDaprAuthorityBinding ExecutorOriginals,IndependentDaprAuthorityBinding RoleOriginals,IndependentDaprAuthorityBinding IndependentSource,
    QualificationInstalledAnchor ObserverTrust,string? RuntimeConfigurationFile=null,Dictionary<string,JsonElement>? RuntimeCaseInputs=null,
    Dictionary<string,TrustedEnvelopeIdentity>? EnvelopeCaseIdentities=null,Dictionary<string,DeletionBatchCapabilityV1>? CapabilityCasePayloads=null,
    Dictionary<string,QualificationDecisionCaseInput>? DecisionCaseInputs=null,QualificationProviderCredentialInput? ProviderCredential=null,
    Dictionary<string,QualificationHmacResolutionCaseInput>? HmacResolutionCases=null,Dictionary<string,ExportQualificationInput>? ExportCases=null,
    Dictionary<string,DaprExportQualificationProfile>? ExportMaterialProfiles=null);
