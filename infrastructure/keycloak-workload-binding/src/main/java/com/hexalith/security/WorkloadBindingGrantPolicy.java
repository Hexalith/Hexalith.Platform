package com.hexalith.security;

import com.fasterxml.jackson.core.JsonFactory;
import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

/** Exact, bounded, value-free-on-failure admission. Contains no credentials or signer. */
public final class WorkloadBindingGrantPolicy {
    public static final String PROFILE = "https://hexalith.com/security/workload-binding/v1";
    public static final String GRANTS = "hexalith.workload-binding.grants";
    public static final String WORKLOAD = "hexalith.workload-binding.workload";
    public static final String PREFIX = "eventstore:bound-";
    public static final String QUERY_DIGEST = PREFIX + "query-authority-v1";
    private static final ObjectMapper JSON = new ObjectMapper(new JsonFactory()
        .enable(JsonParser.Feature.STRICT_DUPLICATE_DETECTION));
    private static final Set<String> DETAIL_FIELDS = Set.of("type", "audience", "operation", "bindings");
    private static final Set<String> GRANT_FIELDS = Set.of("grantId", "revision", "clientId", "workload", "audience",
        "operations", "bindings", "allowUnbound", "allowQueryAuthorityDigest", "notBefore", "expiresAt", "revoked");

    private WorkloadBindingGrantPolicy() { }

    /** One approved request, scoped to this token exchange only. */
    public record Admission(String workload, String audience, String operation, Map<String, String> bindings,
        String grantId, long revision, long expiresAt) { }

    /** Throws only a constant error; never interpolate request or protected enrollment values. */
    public static Admission authorize(String clientId, String workload, String scope, String details,
        String enrollment, long now, long signerNotAfter) {
        try {
            if (!text(clientId, 256) || !text(workload, 256) || signerNotAfter <= now) throw denied();
            JsonNode request;
            if (details == null) {
                if (scope == null || scope.length() > 1024) throw denied();
                String[] parts = scope.split(" ", -1);
                if (parts.length != 2 || !parts[0].startsWith("eventstore-audience.")
                    || !parts[1].startsWith("eventstore-operation.")) throw denied();
                var value = JSON.createObjectNode();
                value.put("type", PROFILE).put("audience", parts[0].substring("eventstore-audience.".length()))
                    .put("operation", parts[1].substring("eventstore-operation.".length()).replace('.', ':'));
                value.set("bindings", JSON.createObjectNode());
                request = value;
            } else {
                JsonNode array = parse(details, 8192);
                if (!array.isArray() || array.size() != 1) throw denied();
                request = array.get(0);
            }
            if (!fields(request, DETAIL_FIELDS) || !PROFILE.equals(string(request, "type"))) throw denied();
            String audience = string(request, "audience"), operation = string(request, "operation");
            if (!text(audience, 256) || !text(operation, 256)
                || !("eventstore-audience." + audience + " eventstore-operation." + operation.replace(':', '.')).equals(scope)) throw denied();
            Map<String, String> bindings = bindings(request.get("bindings"));
            boolean query = bindings.containsKey(QUERY_DIGEST);
            if (query && (!operation.equals("domain-service:query") || bindings.size() != 3
                || !bindings.get(QUERY_DIGEST).matches("[0-9A-F]{64}"))) throw denied();
            JsonNode grants = parse(enrollment, 65536);
            if (!grants.isArray() || grants.isEmpty() || grants.size() > 64) throw denied();
            Set<String> ids = new HashSet<>();
            List<Admission> selected = new ArrayList<>();
            for (JsonNode grant : grants) {
                if (!fields(grant, GRANT_FIELDS)) throw denied();
                String id = string(grant, "grantId");
                long revision = number(grant, "revision"), start = number(grant, "notBefore"), end = number(grant, "expiresAt");
                if (!text(id, 128) || !ids.add(id) || revision <= 0 || start < 0 || end <= start || end - start > 2592000
                    || !text(string(grant, "clientId"), 256) || !text(string(grant, "workload"), 256)
                    || !text(string(grant, "audience"), 256)) throw denied();
                Set<String> operations = values(grant.get("operations"));
                JsonNode permitted = grant.get("bindings");
                if (!permitted.isObject() || permitted.size() > 16) throw denied();
                Map<String, Set<String>> allowed = new LinkedHashMap<>();
                permitted.fields().forEachRemaining(entry -> {
                    if (!bindingName(entry.getKey())) throw denied();
                    allowed.put(entry.getKey(), values(entry.getValue()));
                });
                if (!allowed.isEmpty() && (!allowed.containsKey(PREFIX + "tenant") || !allowed.containsKey(PREFIX + "domain"))) throw denied();
                boolean unbound = bool(grant, "allowUnbound"), queryAllowed = bool(grant, "allowQueryAuthorityDigest");
                if (queryAllowed && (allowed.size() != 2 || !allowed.containsKey(PREFIX + "tenant") || !allowed.containsKey(PREFIX + "domain"))) throw denied();
                boolean revoked = bool(grant, "revoked");
                if (revoked || now < start || now >= end || !clientId.equals(string(grant, "clientId"))
                    || !workload.equals(string(grant, "workload")) || !audience.equals(string(grant, "audience"))
                    || !operations.contains(operation)) continue;
                boolean permittedBindings = bindings.isEmpty() ? unbound && !(queryAllowed && operation.equals("domain-service:query"))
                    : (!queryAllowed || !operation.equals("domain-service:query") || query)
                        && (!query || queryAllowed) && bindings.size() == allowed.size() + (query ? 1 : 0)
                        && bindings.entrySet().stream().allMatch(entry -> query && entry.getKey().equals(QUERY_DIGEST)
                            || allowed.containsKey(entry.getKey()) && allowed.get(entry.getKey()).contains(entry.getValue()));
                if (permittedBindings) selected.add(new Admission(workload, audience, operation, Map.copyOf(bindings), id, revision,
                    Math.min(now + 120, Math.min(end, signerNotAfter))));
            }
            if (selected.size() != 1) throw denied();
            return selected.get(0);
        } catch (Exception failure) {
            throw denied();
        }
    }

    private static JsonNode parse(String value, int maximum) throws Exception {
        if (value == null || value.length() > maximum || value.isBlank()) throw denied();
        // Bound nesting before Jackson allocates a tree; strings cannot hide nesting.
        int depth = 0; boolean quoted = false, escaped = false;
        for (char character : value.toCharArray()) {
            if (quoted) { if (escaped) escaped = false; else if (character == '\\') escaped = true; else if (character == '"') quoted = false; }
            else if (character == '"') quoted = true;
            else if (character == '{' || character == '[') { if (++depth > 5) throw denied(); }
            else if (character == '}' || character == ']') { if (--depth < 0) throw denied(); }
        }
        try (JsonParser parser = JSON.createParser(value)) {
            JsonNode node = JSON.readTree(parser);
            if (parser.nextToken() != null) throw denied();
            return node;
        }
    }

    private static Map<String, String> bindings(JsonNode node) {
        if (node == null || !node.isObject() || node.size() > 16) throw denied();
        Map<String, String> result = new LinkedHashMap<>();
        node.fields().forEachRemaining(entry -> {
            if (!bindingName(entry.getKey()) || !entry.getValue().isTextual() || !text(entry.getValue().textValue(), 256)) throw denied();
            result.put(entry.getKey(), entry.getValue().textValue());
        });
        if (!result.isEmpty() && (!result.containsKey(PREFIX + "tenant") || !result.containsKey(PREFIX + "domain"))) throw denied();
        return result;
    }

    private static boolean bindingName(String name) { return name.startsWith(PREFIX) && text(name, 128); }
    private static boolean fields(JsonNode node, Set<String> expected) {
        if (node == null || !node.isObject() || node.size() != expected.size()) return false;
        Set<String> actual = new HashSet<>(); node.fieldNames().forEachRemaining(actual::add);
        return actual.equals(expected);
    }
    private static String string(JsonNode node, String field) {
        JsonNode value = node.get(field); if (value == null || !value.isTextual()) throw denied(); return value.textValue();
    }
    private static long number(JsonNode node, String field) {
        JsonNode value = node.get(field); if (value == null || !value.isIntegralNumber() || !value.canConvertToLong()) throw denied(); return value.longValue();
    }
    private static boolean bool(JsonNode node, String field) {
        JsonNode value = node.get(field); if (value == null || !value.isBoolean()) throw denied(); return value.booleanValue();
    }
    private static Set<String> values(JsonNode node) {
        if (node == null || !node.isArray() || node.isEmpty() || node.size() > 32) throw denied();
        Set<String> result = new HashSet<>();
        for (JsonNode value : node) if (!value.isTextual() || !text(value.textValue(), 256) || !result.add(value.textValue())) throw denied();
        return result;
    }
    private static boolean text(String value, int maximum) {
        return value != null && !value.isBlank() && value.length() <= maximum && value.equals(value.trim())
            && value.chars().noneMatch(character -> Character.isISOControl(character) || Character.isWhitespace(character) || character == '*');
    }
    private static IllegalArgumentException denied() { return new IllegalArgumentException("workload_binding_denied"); }
}
