{{- define "reference.graph" -}}
{{- required "canonical reference graph is missing" (.Files.Get "reference-graph.json") -}}
{{- end -}}

{{- define "reference.validate" -}}
{{- if and .Values.capacityProfile (ne .Values.capacityProfile "PG-FINITE-CAMPAIGN-1") -}}{{ fail "unknown capacity profile" }}{{- end -}}
{{- if not (has .Values.mode (list "normal" "recovery" "remove-workloads")) -}}{{ fail "unknown mode" }}{{- end -}}
{{- if not (regexMatch "^[1-3]$" (toString .Values.replicas)) -}}{{ fail "replicas must be an integer in 1..3" }}{{- end -}}
{{- range $value := list .Values.imagePullSecret .Values.environment.componentConfigMap .Values.gateway.name .Values.gateway.sectionName .Values.environment.trustDomain .Values.environment.controlPlaneTrustDomain -}}
{{- include "reference.dnsSubdomain" $value -}}
{{- end -}}
{{- range $value := list .Values.gateway.namespace .Release.Namespace -}}
{{- include "reference.namespace" $value -}}
{{- end -}}
{{- range $key, $endpoint := .Values.endpoints -}}
{{- include "reference.endpoint" $endpoint -}}
{{- end -}}
{{- if empty .Values.endpoints -}}{{ fail "non-secret endpoint parameters are required" }}{{- end -}}
{{- $sentry := required "existing Sentry endpoint parameter is required" (index .Values.endpoints .Values.environment.sentryEndpoint) -}}
{{- include "reference.authority" (dict "authority" (urlParse $sentry).host "requirePort" true) -}}
{{- $graph := include "reference.graph" . | fromJson -}}
{{- range $graph.capabilities -}}
{{- $binding := required (printf "environment component %s is required" .name) (index $.Values.environment.components .name) -}}
{{- if ne $binding.name .name -}}{{ fail "environment component name differs from graph" }}{{- end -}}
{{- $prefix := index (dict "state" "state." "pubsub" "pubsub." "configuration" "configuration." "secret" "secretstores." "conversation" "conversation.") .kind -}}
{{- if or (not (hasPrefix $prefix (required "hosted component type is required" $binding.type))) (regexMatch "(?i)(local|echo)" $binding.type) -}}{{ fail "component provider differs from capability or is local-only" }}{{- end -}}
{{- range $key := list $binding.endpointMetadataKey $binding.credentialMetadataKey -}}
{{- if not (regexMatch "^[a-zA-Z][a-zA-Z0-9]*$" (toString $key)) -}}{{ fail "provider metadata keys are required" }}{{- end -}}
{{- end -}}
{{- if not (has (default "url" $binding.endpointFormat) (list "url" "authority")) -}}{{ fail "unknown endpoint metadata format" }}{{- end -}}
{{- if and (hasSuffix ".redis" $binding.type) (ne (toString $binding.metadata.enableTLS) "true") -}}{{ fail "hosted Redis bindings require native TLS" }}{{- end -}}
{{- range $key, $value := $binding.metadata -}}
{{- if or (kindIs "map" $value) (kindIs "slice" $value) -}}{{ fail "provider metadata values must be scalar" }}{{- end -}}
{{- include "reference.credentialLiteral" $value -}}
{{- if or (eq (include "reference.isCredentialKey" $key) "true") (eq (lower $key) (lower $binding.endpointMetadataKey)) (eq (lower $key) (lower $binding.credentialMetadataKey)) -}}{{ fail "provider credentials and endpoints require references" }}{{- end -}}
{{- if regexMatch "(?i)(://|localhost|127[.]|password=|token=)" (toString $value) -}}{{ fail "unsafe provider metadata literal" }}{{- end -}}
{{- end -}}
{{- $endpoint := required "component endpoint reference is required" $binding.endpoint -}}
{{- $value := required "component endpoint parameter is missing" (index $.Values.endpoints $endpoint) -}}
{{- if eq (default "url" $binding.endpointFormat) "authority" -}}
{{- include "reference.authority" (dict "authority" (urlParse $value).host "requirePort" true) -}}
{{- end -}}
{{- include "reference.secretRef" $binding.credential | fromYaml | toJson | trunc 0 -}}
{{- end -}}
{{- $matches := dict -}}
{{- range $graph.workloads -}}
{{- $w := . -}}
{{- $image := required (printf "image binding missing for %s" .identity) (index $.Values.images .identity) -}}
{{- if not (regexMatch "^([a-z0-9][a-z0-9.-]*(:[0-9]+)?/)?[a-z0-9][a-z0-9./_-]*@sha256:[a-f0-9]{64}$" $image) -}}{{ fail "images require repository@sha256 digest; tags are forbidden" }}{{- end -}}
{{- $b := required (printf "bindings missing for %s" .identity) (index $.Values.bindings .identity) -}}
{{- if eq (default "" $.Values.capacityProfile) "PG-FINITE-CAMPAIGN-1" -}}
{{- $replicas := 1 -}}
{{- if has .identity (list "memories/server" "memories/telemetry") -}}{{- $replicas = 2 -}}{{- end -}}
{{- if ne (toString $b.replicas) (toString $replicas) -}}{{ fail "finite campaign replica allocation differs" }}{{- end -}}
{{- if hasPrefix "memories/" .identity -}}
{{- if ne $b.containerName (index (dict "memories/server" "memories" "memories/telemetry" "lifecycle" "memories/clock" "clock") .identity) -}}{{ fail "finite campaign container name differs" }}{{- end -}}
{{- end -}}
{{- $resources := dict "requests" (dict "cpu" "400m" "memory" "640Mi" "ephemeral-storage" "128Mi") "limits" (dict "cpu" "400m" "memory" "640Mi" "ephemeral-storage" "384Mi") -}}
{{- if ne (toJson $b.resources) (toJson $resources) -}}{{ fail "finite campaign resources differ" }}{{- end -}}
{{- else if or $b.replicas $b.resources -}}{{ fail "resource overrides require an explicit capacity profile" }}{{- end -}}
{{- include "reference.secretRef" $b.appToken | trunc 0 -}}
{{- include "reference.secretRef" $b.apiToken | trunc 0 -}}
{{- if or (ne $b.appToken.key "token") (ne $b.apiToken.key "token") -}}{{ fail "Dapr token references must use the token key" }}{{- end -}}
{{- include "reference.environment" (dict "workload" $w "binding" $b "root" $) | trunc 0 -}}
{{- if .publicRoutes -}}
{{- $host := required (printf "hostname binding missing for %s" .identity) (index $.Values.hostnames .identity) | lower | trimSuffix "." -}}
{{- include "reference.dnsSubdomain" $host -}}
{{- range .publicRoutes -}}
{{- $prefix := trimSuffix "/" (regexFind "^[^{]*" .) | default "/" -}}
{{- $match := printf "%s/%s/%s/%s/%s" $.Values.gateway.namespace $.Values.gateway.name $.Values.gateway.sectionName $host $prefix -}}
{{- if and (hasKey $matches $match) (ne (index $matches $match) $w.resourceId) -}}{{ fail "identical normalized host/listener/path-prefix matches cannot select different backends" }}{{- end -}}
{{- $_ := set $matches $match $w.resourceId -}}
{{- end -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{- define "reference.dnsSubdomain" -}}
{{- if or (not (kindIs "string" .)) (gt (len (toString .)) 253) (not (regexMatch "^[a-z0-9]([-a-z0-9]*[a-z0-9])?([.][a-z0-9]([-a-z0-9]*[a-z0-9])?)*$" (toString .))) -}}{{ fail "existing reference or DNS name must be a DNS subdomain of at most 253 characters" }}{{- end -}}
{{- end -}}

{{- define "reference.namespace" -}}
{{- if or (not (kindIs "string" .)) (gt (len (toString .)) 63) (not (regexMatch "^[a-z0-9]([-a-z0-9]*[a-z0-9])?$" (toString .))) -}}{{ fail "namespace must be a DNS label of at most 63 characters" }}{{- end -}}
{{- end -}}

{{- define "reference.endpoint" -}}
{{- if or (not (kindIs "string" .)) (not (regexMatch "^(https|rediss)://[a-zA-Z0-9][a-zA-Z0-9.-]*(:[0-9]{1,5})?(/[a-zA-Z0-9._~/-]*)?$|^postgresql://[a-zA-Z0-9][a-zA-Z0-9.-]*(:[0-9]{1,5})?(/[a-zA-Z0-9._~/-]*)?[?]sslmode=(verify-full|require|verify-ca)$" (toString .))) -}}{{ fail "unsafe endpoint: use a non-secret TLS endpoint; PostgreSQL requires a single sslmode=verify-full, require or verify-ca query" }}{{- end -}}
{{- include "reference.authority" (dict "authority" (urlParse .).host "requirePort" false) -}}
{{- end -}}

{{- define "reference.authority" -}}
{{- $authority := .authority -}}
{{- if not (regexMatch "^[a-zA-Z0-9][a-zA-Z0-9.-]*(:[0-9]{1,5})?$" $authority) -}}{{ fail "invalid endpoint authority" }}{{- end -}}
{{- if and .requirePort (not (contains ":" $authority)) -}}{{ fail "endpoint authority requires an explicit port" }}{{- end -}}
{{- $host := regexFind "^[^:]+" $authority | lower -}}
{{- if contains ":" $authority -}}
{{- $port := int (last (splitList ":" $authority)) -}}
{{- if or (lt $port 1) (gt $port 65535) -}}{{ fail "invalid endpoint port" }}{{- end -}}
{{- end -}}
{{- if regexMatch "^(0x[0-9a-f]+|[0-9]+)([.](0x[0-9a-f]+|[0-9]+))*[.]?$" $host -}}
{{- $parts := splitList "." $host -}}
{{- if ne (len $parts) 4 -}}{{ fail "numeric endpoint authorities must use canonical non-loopback IPv4" }}{{- end -}}
{{- range $parts -}}
{{- if or (not (regexMatch "^(0|[1-9][0-9]{0,2})$" .)) (gt (int .) 255) -}}{{ fail "ambiguous numeric endpoint authority" }}{{- end -}}
{{- end -}}
{{- if has (first $parts) (list "0" "127") -}}{{ fail "loopback or unspecified endpoint authority" }}{{- end -}}
{{- else -}}
{{- $dns := trimSuffix "." $host -}}
{{- include "reference.dnsSubdomain" $dns -}}
{{- if or (eq $dns "localhost") (hasSuffix ".localhost" $dns) -}}{{ fail "localhost endpoint authority" }}{{- end -}}
{{- end -}}
{{- end -}}

{{- define "reference.connectionString" -}}
{{- $literal := trim (toString .value) -}}
{{- $key := lower .key -}}
{{- $duration := and (regexMatch "^[0-9]{1,2}:[0-5][0-9]$" $literal) (not (hasSuffix "__endpoint" $key)) -}}
{{- $authority := and (regexMatch "^[^:[:space:],/]+[[:space:]]*:[[:space:]]*[0-9]+[[:space:]]*(,.*)?$" $literal) (not $duration) -}}
{{- if or (hasPrefix "connectionstrings__" $key) $authority -}}
{{- if contains "://" $literal -}}
{{- include "reference.authority" (dict "authority" (urlParse $literal).host "requirePort" true) -}}
{{- else -}}
{{- $endpoint := false -}}
{{- $tls := false -}}
{{- range splitList "," $literal -}}
{{- $part := trim . -}}
{{- if contains "=" $part -}}
{{- $option := splitList "=" $part -}}
{{- if eq (lower (trim (first $option))) "ssl" -}}
{{- if or (ne (len $option) 2) (ne (lower (trim (last $option))) "true") -}}{{ fail "scheme-less data-service endpoints require ssl=true and forbid ssl=false" }}{{- end -}}
{{- $tls = true -}}
{{- end -}}
{{- else -}}
{{- include "reference.authority" (dict "authority" $part "requirePort" true) -}}
{{- $endpoint = true -}}
{{- end -}}
{{- end -}}
{{- if not $endpoint -}}{{ fail "connection strings require at least one endpoint authority" }}{{- end -}}
{{- if not $tls -}}{{ fail "scheme-less data-service endpoints require ssl=true and forbid ssl=false" }}{{- end -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{- define "reference.credentialLiteral" -}}
{{- if regexMatch `(?i)(^|[;,\s\p{Zs}])(password|pwd|user[ _]*id|uid|user[ _]*name|access[ _]*token|authentication[ _]*token|token|api[ _-]*key|client[ _]*secret|secret|authorization|access[ _]*key|account[ _]*key|master[ _]*key|shared[ _]*access[ _]*key|passphrase|credentials?)[\s\p{Zs}]*=` (toString .) -}}{{ fail "credential-bearing connection-string literals require existing references" }}{{- end -}}
{{- end -}}

{{- define "reference.secretRef" -}}
{{- if not (kindIs "map" .) -}}{{ fail "credential literals are forbidden; existing reference required" }}{{- end -}}
{{- if or (ne (len .) 2) (not (hasKey . "name")) (not (hasKey . "key")) -}}{{ fail "existing reference requires only name and key" }}{{- end -}}
{{- include "reference.dnsSubdomain" .name -}}
{{- if or (gt (len (toString .key)) 253) (not (regexMatch "^[-a-zA-Z0-9._]+$" (toString .key))) -}}{{ fail "invalid existing reference key" }}{{- end -}}
name: {{ .name | quote }}
key: {{ .key | quote }}
{{- end -}}

{{- define "reference.security" -}}
allowPrivilegeEscalation: false
readOnlyRootFilesystem: true
runAsNonRoot: true
runAsUser: 1654
capabilities:
  drop: [ALL]
seccompProfile:
  type: RuntimeDefault
{{- end -}}

{{- define "reference.dapr" -}}
dapr.io/enabled: "true"
dapr.io/app-id: {{ .workload.appId | quote }}
dapr.io/app-port: "8080"
dapr.io/app-protocol: http
dapr.io/config: {{ printf "platform-%s" .workload.appId | quote }}
dapr.io/app-token-secret: {{ .binding.appToken.name | quote }}
dapr.io/api-token-secret: {{ .binding.apiToken.name | quote }}
dapr.io/sidecar-listen-addresses: "127.0.0.1"
dapr.io/enable-api-logging: "false"
{{- if .binding.resources }}
dapr.io/sidecar-cpu-request: "100m"
dapr.io/sidecar-cpu-limit: "100m"
dapr.io/sidecar-memory-request: "128Mi"
dapr.io/sidecar-memory-limit: "128Mi"
{{- end }}
{{- end -}}

{{/* All configuration is decoded before recursive validation and flattening. */}}
{{- define "reference.put" -}}
{{- $key := replace ":" "__" .key -}}
{{- if not (regexMatch "^[A-Za-z_][A-Za-z0-9_]*$" $key) -}}{{ fail "invalid environment key" }}{{- end -}}
{{- $normalized := lower $key -}}
{{- if hasKey .seen $normalized -}}{{ fail (printf "equivalent configuration/environment keys: %s and %s" (index .seen $normalized) $key) }}{{- end -}}
{{- $_ := set .seen $normalized $key -}}
{{- $_ := set .environment $key (merge (dict "name" $key) .entry) -}}
{{- end -}}

{{- define "reference.flatten" -}}
{{- $key := replace ":" "__" .key -}}
{{- $value := .value -}}
{{- include "reference.nonCredentialKey" $key -}}
{{- if and (kindIs "string" $value) (or (hasPrefix "[" (trim $value)) (hasPrefix "{" (trim $value))) -}}
{{- $value = mustFromJson $value -}}
{{- end -}}
{{- if kindIs "invalid" $value -}}{{ fail "recursive configuration leaves cannot be null" }}{{- end -}}
{{- if and (or (kindIs "map" $value) (kindIs "slice" $value)) (empty $value) -}}{{ fail "configuration binding must produce at least one leaf" }}{{- end -}}
{{- if kindIs "map" $value -}}
{{- if or (hasKey $value "endpoint") (hasKey $value "configMapKeyRef") -}}
{{- if ne (len $value) 1 -}}{{ fail "reference leaves must contain exactly one reference" }}{{- end -}}
{{- include "reference.nonCredentialKey" $key -}}
{{- if hasKey $value "endpoint" -}}
{{- $endpoint := required "endpoint parameter is missing" (index .root.Values.endpoints (toString $value.endpoint)) -}}
{{- include "reference.endpoint" $endpoint -}}
{{- include "reference.connectionString" (dict "key" $key "value" $endpoint) -}}
{{- include "reference.put" (dict "key" $key "entry" (dict "value" $endpoint) "environment" .environment "seen" .seen) -}}
{{- else -}}
{{- include "reference.secretRef" $value.configMapKeyRef | trunc 0 -}}
{{- include "reference.put" (dict "key" $key "entry" (dict "valueFrom" (dict "configMapKeyRef" $value.configMapKeyRef)) "environment" .environment "seen" .seen) -}}
{{- end -}}
{{- else -}}
{{- range $k, $v := $value -}}
{{- include "reference.flatten" (dict "key" (printf "%s__%s" $key $k) "value" $v "environment" $.environment "seen" $.seen "root" $.root) -}}
{{- end -}}
{{- end -}}
{{- else if kindIs "slice" $value -}}
{{- range $k, $v := $value -}}
{{- include "reference.flatten" (dict "key" (printf "%s__%d" $key $k) "value" $v "environment" $.environment "seen" $.seen "root" $.root) -}}
{{- end -}}
{{- else -}}
{{- include "reference.nonCredentialKey" $key -}}
{{- include "reference.credentialLiteral" $value -}}
{{- if regexMatch "(?i)(localhost|127[.]0[.]0[.]1|/tmp/|secretsFile|conversation[.]echo|secretstores[.]local|password=|token=|clientsecret|authenticationtoken|accesstoken|privatekey)" (toString $value) -}}{{ fail "unsafe or credential-bearing configuration literal" }}{{- end -}}
{{- if contains "://" (toString $value) -}}{{ include "reference.endpoint" $value }}{{- end -}}
{{- include "reference.connectionString" (dict "key" $key "value" $value) -}}
{{- include "reference.put" (dict "key" $key "entry" (dict "value" (include "reference.scalar" $value)) "environment" .environment "seen" .seen) -}}
{{- end -}}
{{- end -}}

{{- define "reference.nonCredentialKey" -}}
{{- if eq (include "reference.isCredentialKey" .) "true" -}}{{ fail "credential leaves require existing Secret references in credentials" }}{{- end -}}
{{- end -}}

{{- define "reference.isCredentialKey" -}}
{{- regexMatch "(?i)(password|pwd|clientsecret|signingkey|authenticationtokens?|accesstokens?|apikeys?|accesskey|accountkey|masterkey|sharedaccesskey|passphrase|credentials?|privatekey|appchanneltoken|daprapitoken|apidaprtoken|authorization|secrets?|tokens?)(__|$)|connectionstrings?$" . -}}
{{- end -}}

{{/* JSON preserves ordinary integral floats without Helm's scientific notation. */}}
{{- define "reference.scalar" -}}
{{- if kindIs "string" . -}}{{ . }}{{- else -}}{{ toJson . }}{{- end -}}
{{- end -}}

{{- define "reference.environment" -}}
{{- $env := dict -}}
{{- $seen := dict -}}
{{- $binding := .binding -}}
{{- $root := .root -}}
{{- $workload := .workload -}}
{{- $declared := dict -}}
{{- $reserved := dict "app_api_token" true "dapr_api_token" true "aspnetcore_urls" true "dapr__declarativesubscriptions" true -}}
{{- range $key, $_ := $workload.environment -}}{{- $_ := set $reserved (lower $key) true -}}{{- end -}}
{{- range $workload.inputs -}}
{{- $_ := set $declared .key true -}}
{{- if not (hasKey $binding.configuration .key) -}}{{ fail (printf "%s/%s binding is missing" $workload.identity .key) }}{{- end -}}
{{- $value := index $binding.configuration .key -}}
{{- if or (kindIs "invalid" $value) (and (kindIs "string" $value) (eq (trim $value) "")) -}}{{ fail "configuration binding cannot be null or empty" }}{{- end -}}
{{- $key := replace ":" "__" .key -}}
{{- if has .source (list "secret" "dynamic-secret") -}}
{{- include "reference.secretRef" $value | trunc 0 -}}
{{- if and (hasSuffix ":appchanneltoken" (lower .key)) (ne (toJson $value) (toJson $binding.appToken)) -}}{{ fail "app channel input must match the shared Dapr token reference" }}{{- end -}}
{{- if and (or (hasSuffix ":daprapitoken" (lower .key)) (hasSuffix ":apidaprtoken" (lower .key))) (ne (toJson $value) (toJson $binding.apiToken)) -}}{{ fail "Dapr API input must match the shared Dapr token reference" }}{{- end -}}
{{- include "reference.put" (dict "key" $key "entry" (dict "valueFrom" (dict "secretKeyRef" $value)) "environment" $env "seen" $seen) -}}
{{- else -}}
{{- include "reference.flatten" (dict "key" $key "value" $value "environment" $env "seen" $seen "root" $root) -}}
{{- end -}}
{{- end -}}
{{- range $key, $_ := $binding.configuration -}}
{{- if not (hasKey $declared $key) -}}{{ fail (printf "unknown or case-equivalent configuration key: %s" $key) }}{{- end -}}
{{- end -}}
{{- range $key, $reference := $binding.credentials -}}
{{- if hasKey $reserved (lower (replace ":" "__" $key)) -}}{{ fail "credentials cannot override canonical Dapr/security values" }}{{- end -}}
{{- include "reference.secretRef" $reference | trunc 0 -}}
{{- include "reference.put" (dict "key" $key "entry" (dict "valueFrom" (dict "secretKeyRef" $reference)) "environment" $env "seen" $seen) -}}
{{- end -}}
{{- range $key, $value := $workload.environment -}}
{{/* Exact declared keys retain shared graph precedence; aliases always fail. */}}
{{- if and (hasKey $seen (lower $key)) (ne (index $seen (lower $key)) $key) -}}{{ fail "case-equivalent canonical Dapr key" }}{{- end -}}
{{- $_ := set $env $key (dict "name" $key "value" $value) -}}
{{- end -}}
{{- range $key, $value := dict "ASPNETCORE_URLS" (dict "value" "http://0.0.0.0:8080") "Dapr__DeclarativeSubscriptions" (dict "value" "true") "APP_API_TOKEN" (dict "valueFrom" (dict "secretKeyRef" $binding.appToken)) "DAPR_API_TOKEN" (dict "valueFrom" (dict "secretKeyRef" $binding.apiToken)) -}}
{{- include "reference.put" (dict "key" $key "entry" $value "environment" $env "seen" $seen) -}}
{{- end -}}
{{- if and (eq (default "" $root.Values.capacityProfile) "PG-FINITE-CAMPAIGN-1") (hasPrefix "memories/" $workload.identity) -}}
{{- include "reference.put" (dict "key" "ASPNETCORE_ENVIRONMENT" "entry" (dict "value" "Qualification") "environment" $env "seen" $seen) -}}
{{- include "reference.put" (dict "key" "AccessTelemetryQualification__ProfileId" "entry" (dict "value" "PG-FINITE-CAMPAIGN-1") "environment" $env "seen" $seen) -}}
{{- include "reference.put" (dict "key" "AccessTelemetryQualification__ApprovedProfileHash" "entry" (dict "valueFrom" (dict "configMapKeyRef" (dict "name" "access-telemetry-campaign-identity" "key" "profileSha256"))) "environment" $env "seen" $seen) -}}
{{- end -}}
{{- range $key, $value := $env }}
- {{ toYaml $value | nindent 2 | trim }}
{{- end -}}
{{- end -}}
