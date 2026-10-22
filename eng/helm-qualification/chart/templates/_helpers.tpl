{{- define "reference.graph" -}}
{{- required "canonical reference graph is missing" (.Files.Get "reference-graph.json") -}}
{{- end -}}

{{- define "reference.validate" -}}
{{- if not (has .Values.mode (list "normal" "recovery" "remove-workloads")) -}}{{ fail "unknown mode" }}{{- end -}}
{{- if not (regexMatch "^[1-3]$" (toString .Values.replicas)) -}}{{ fail "replicas must be an integer in 1..3" }}{{- end -}}
{{- range $value := list .Values.imagePullSecret .Values.environment.componentConfigMap .Values.gateway.name .Values.gateway.namespace .Values.gateway.sectionName -}}
{{- if not (regexMatch "^[a-z0-9]([-a-z0-9.]*[a-z0-9])?$" $value) -}}{{ fail "existing credential, ConfigMap and namespaced Gateway references are required" }}{{- end -}}
{{- end -}}
{{- range $value := list .Values.environment.trustDomain .Values.environment.controlPlaneTrustDomain -}}
{{- if not (regexMatch "^[a-z0-9]([-a-z0-9.]*[a-z0-9])?$" $value) -}}{{ fail "hostname and trust domain are required" }}{{- end -}}
{{- end -}}
{{- range $key, $endpoint := .Values.endpoints -}}
{{- include "reference.endpoint" $endpoint -}}
{{- end -}}
{{- $_ := required "existing Sentry endpoint parameter is required" (index .Values.endpoints .Values.environment.sentryEndpoint) -}}
{{- if empty .Values.endpoints -}}{{ fail "non-secret endpoint parameters are required" }}{{- end -}}
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
{{- if or (regexMatch "(?i)(password|secret|token|apikey|authorization|connectionstring)" $key) (eq $key $binding.endpointMetadataKey) (eq $key $binding.credentialMetadataKey) -}}{{ fail "provider credentials and endpoints require references" }}{{- end -}}
{{- if regexMatch "(?i)(://|localhost|127[.]|password=|token=)" (toString $value) -}}{{ fail "unsafe provider metadata literal" }}{{- end -}}
{{- end -}}
{{- $endpoint := required "component endpoint reference is required" $binding.endpoint -}}
{{- $_ := required "component endpoint parameter is missing" (index $.Values.endpoints $endpoint) -}}
{{- include "reference.secretRef" $binding.credential | fromYaml | toJson | trunc 0 -}}
{{- end -}}
{{- $matches := dict -}}
{{- range $graph.workloads -}}
{{- $w := . -}}
{{- $image := required (printf "image binding missing for %s" .identity) (index $.Values.images .identity) -}}
{{- if not (regexMatch "^([a-z0-9][a-z0-9.-]*(:[0-9]+)?/)?[a-z0-9][a-z0-9./_-]*@sha256:[a-f0-9]{64}$" $image) -}}{{ fail "images require repository@sha256 digest; tags are forbidden" }}{{- end -}}
{{- $b := required (printf "bindings missing for %s" .identity) (index $.Values.bindings .identity) -}}
{{- include "reference.secretRef" $b.appToken | trunc 0 -}}
{{- include "reference.secretRef" $b.apiToken | trunc 0 -}}
{{- if or (ne $b.appToken.key "token") (ne $b.apiToken.key "token") -}}{{ fail "Dapr token references must use the token key" }}{{- end -}}
{{- include "reference.environment" (dict "workload" $w "binding" $b "root" $) | trunc 0 -}}
{{- if .publicRoutes -}}
{{- $host := required (printf "hostname binding missing for %s" .identity) (index $.Values.hostnames .identity) | lower | trimSuffix "." -}}
{{- if not (regexMatch "^[a-z0-9]([-a-z0-9.]*[a-z0-9])?$" $host) -}}{{ fail "invalid workload hostname" }}{{- end -}}
{{- range .publicRoutes -}}
{{- $prefix := trimSuffix "/" (regexFind "^[^{]*" .) | default "/" -}}
{{- $match := printf "%s/%s/%s/%s/%s" $.Values.gateway.namespace $.Values.gateway.name $.Values.gateway.sectionName $host $prefix -}}
{{- if and (hasKey $matches $match) (ne (index $matches $match) $w.resourceId) -}}{{ fail "identical normalized host/listener/path-prefix matches cannot select different backends" }}{{- end -}}
{{- $_ := set $matches $match $w.resourceId -}}
{{- end -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{- define "reference.endpoint" -}}
{{- if or (not (kindIs "string" .)) (not (regexMatch "^(https|rediss|postgresql)://[a-zA-Z0-9][-a-zA-Z0-9.:/]*$" (toString .))) (regexMatch "(?i)(localhost|127[.]|0[.]0[.]0[.]0)" (toString .)) -}}{{ fail "unsafe endpoint: use a non-secret TLS endpoint without credentials, query or fragment" }}{{- end -}}
{{- end -}}

{{- define "reference.secretRef" -}}
{{- if not (kindIs "map" .) -}}{{ fail "credential literals are forbidden; existing reference required" }}{{- end -}}
{{- if or (ne (len .) 2) (not (hasKey . "name")) (not (hasKey . "key")) -}}{{ fail "existing reference requires only name and key" }}{{- end -}}
{{- if or (not (regexMatch "^[a-z0-9]([-a-z0-9.]*[a-z0-9])?$" (toString .name))) (not (regexMatch "^[-a-zA-Z0-9._]+$" (toString .key))) -}}{{ fail "invalid existing reference name or key" }}{{- end -}}
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
{{- if kindIs "map" $value -}}
{{- if or (hasKey $value "endpoint") (hasKey $value "configMapKeyRef") -}}
{{- if ne (len $value) 1 -}}{{ fail "reference leaves must contain exactly one reference" }}{{- end -}}
{{- include "reference.nonCredentialKey" $key -}}
{{- if hasKey $value "endpoint" -}}
{{- $endpoint := required "endpoint parameter is missing" (index .root.Values.endpoints (toString $value.endpoint)) -}}
{{- include "reference.endpoint" $endpoint -}}
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
{{- if regexMatch "(?i)(localhost|127[.]0[.]0[.]1|/tmp/|secretsFile|conversation[.]echo|secretstores[.]local|password=|token=|clientsecret|authenticationtoken|accesstoken|privatekey)" (toString $value) -}}{{ fail "unsafe or credential-bearing configuration literal" }}{{- end -}}
{{- if contains "://" (toString $value) -}}{{ include "reference.endpoint" $value }}{{- end -}}
{{- include "reference.put" (dict "key" $key "entry" (dict "value" (toString $value)) "environment" .environment "seen" .seen) -}}
{{- end -}}
{{- end -}}

{{- define "reference.nonCredentialKey" -}}
{{- if regexMatch "(?i)(password|clientsecret|signingkey|authenticationtokens?|accesstokens?|apikeys?|privatekey|appchanneltoken|daprapitoken|apidaprtoken|authorization|secrets?|tokens?)(__|$)" . -}}{{ fail "credential leaves require existing Secret references in credentials" }}{{- end -}}
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
{{- range $key, $value := $env }}
- {{ toYaml $value | nindent 2 | trim }}
{{- end -}}
{{- end -}}
