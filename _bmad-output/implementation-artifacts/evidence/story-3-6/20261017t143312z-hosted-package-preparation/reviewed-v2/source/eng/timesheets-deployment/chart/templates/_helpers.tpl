{{- define "platform.validate" -}}
{{- if ne .Release.Namespace "hexalith-staging" -}}{{ fail "candidate namespace must be hexalith-staging" }}{{- end -}}
{{- if not (has .Values.mode (list "normal" "recovery" "remove-workloads")) -}}{{ fail "mode must be normal, recovery or remove-workloads" }}{{- end -}}
{{- if not (regexMatch "^sha256:[a-f0-9]{64}$" .Values.image.digest) -}}{{ fail "retained image digest is required" }}{{- end -}}
{{- if empty .Values.image.pullSecretName -}}{{ fail "existing image.pullSecretName is required" }}{{- end -}}
{{- if not (regexMatch "^[a-f0-9]{40}$" .Values.sourceRevision) -}}{{ fail "exact sourceRevision is required" }}{{- end -}}
{{- if or (not (regexMatch "^https://[^/?#@ ]+(/[^?# ]*)?$" .Values.identity.authority)) (not (regexMatch "^https://[^/?#@ ]+(/[^?# ]*)?$" .Values.identity.issuer)) (not (regexMatch "^https://[^/?#@ ]+(/[^?# ]*)?$" .Values.identity.tokenEndpoint)) -}}{{ fail "HTTPS authority, issuer and tokenEndpoint are required" }}{{- end -}}
{{- if or (not (regexMatch "^https://[^/?#@ ]+(/[^?# ]*)?$" .Values.identity.workloadAuthority)) (not (regexMatch "^https://[^/?#@ ]+(/[^?# ]*)?$" .Values.identity.workloadIssuer)) -}}{{ fail "separate HTTPS workloadAuthority and workloadIssuer are required" }}{{- end -}}
{{- range $key := list "audience" "secretName" "clientIdKey" "clientSecretKey" -}}
{{- if empty (index $.Values.identity $key) -}}{{ fail (printf "identity.%s is required" $key) }}{{- end -}}
{{- end -}}
{{- if or (empty .Values.dapr.configuration) (empty .Values.dapr.appTokenSecretName) (empty .Values.dapr.apiTokenSecretName) -}}{{ fail "Dapr configuration and token Secret names are required" }}{{- end -}}
{{- if or (lt (int .Values.replicas) 1) (gt (int .Values.replicas) 3) -}}{{ fail "replicas must be 1..3" }}{{- end -}}
{{- range $algorithms := list .Values.identity.allowedAlgorithms .Values.identity.workloadAllowedAlgorithms -}}
{{- if empty $algorithms -}}{{ fail "asymmetric algorithms are required" }}{{- end -}}
{{- range $algorithms -}}{{- if not (has . (list "RS256" "RS384" "RS512" "ES256" "ES384" "ES512" "PS256" "PS384" "PS512")) -}}{{ fail "symmetric or unsupported signing algorithm" }}{{- end -}}{{- end -}}
{{- end -}}
{{- if .Values.ingress.enabled -}}
{{- if or (ne .Values.ingress.hostname "timesheets.hexalith.com") (ne .Values.ingress.gateway.namespace "hexalith-staging-data") (empty .Values.ingress.gateway.name) (empty .Values.ingress.gateway.sectionName) -}}{{ fail "qualified staging Gateway/listener and exact hostname are required" }}{{- end -}}
{{- end -}}
{{- end -}}

{{- define "platform.labels" -}}
app.kubernetes.io/name: timesheets
app.kubernetes.io/part-of: hexalith-platform
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{- define "platform.daprAnnotations" -}}
dapr.io/enabled: "true"
dapr.io/app-id: timesheets
dapr.io/app-port: "8081"
dapr.io/app-protocol: http
dapr.io/config: {{ .Values.dapr.configuration | quote }}
dapr.io/app-token-secret: {{ .Values.dapr.appTokenSecretName | quote }}
dapr.io/api-token-secret: {{ .Values.dapr.apiTokenSecretName | quote }}
dapr.io/enable-api-logging: "false"
dapr.io/sidecar-listen-addresses: "127.0.0.1"
{{- end -}}

{{- define "platform.containerSecurity" -}}
allowPrivilegeEscalation: false
readOnlyRootFilesystem: true
capabilities:
  drop: [ALL]
{{- end -}}
