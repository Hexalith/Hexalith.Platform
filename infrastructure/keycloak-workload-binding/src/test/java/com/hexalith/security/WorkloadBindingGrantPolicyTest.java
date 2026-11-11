package com.hexalith.security;

import static org.junit.jupiter.api.Assertions.*;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.util.Map;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

/** Component policy tests. These do not establish protected enrollment or TLS trust. */
class WorkloadBindingGrantPolicyTest {
    private static final ObjectMapper JSON = new ObjectMapper();
    private static final long NOW = 1800000000;
    private static final String SCOPE = "eventstore-audience.eventstore eventstore-operation.eventstore.stream-read";

    private ObjectNode grant() {
        ObjectNode grant = JSON.createObjectNode();
        grant.put("grantId", "source-folders").put("revision", 1).put("clientId", "folders-client")
            .put("workload", "folders").put("audience", "eventstore").put("notBefore", NOW - 10).put("expiresAt", NOW + 3600)
            .put("allowUnbound", false).put("allowQueryAuthorityDigest", false).put("revoked", false);
        grant.putArray("operations").add("eventstore:stream-read");
        ObjectNode bindings = grant.putObject("bindings");
        bindings.putArray("eventstore:bound-tenant").add("tenant-a");
        bindings.putArray("eventstore:bound-domain").add("folders");
        return grant;
    }

    private String details() {
        return "[{\"type\":\"" + WorkloadBindingGrantPolicy.PROFILE + "\",\"audience\":\"eventstore\",\"operation\":\"eventstore:stream-read\","
            + "\"bindings\":{\"eventstore:bound-tenant\":\"tenant-a\",\"eventstore:bound-domain\":\"folders\"}}]";
    }

    private String enrollment(ObjectNode grant) { return JSON.createArrayNode().add(grant).toString(); }
    private WorkloadBindingGrantPolicy.Admission authorize(ObjectNode grant, String detail) {
        return WorkloadBindingGrantPolicy.authorize("folders-client", "folders", SCOPE, detail, enrollment(grant), NOW, NOW + 600);
    }

    @Test void exactSourceGrantBindsIdentityScopeAndExpiry() {
        var selected = authorize(grant(), details());
        assertEquals("folders", selected.workload());
        assertEquals("source-folders", selected.grantId());
        assertEquals(1, selected.revision());
        assertEquals(NOW + 120, selected.expiresAt());
        assertEquals(Map.of("eventstore:bound-tenant", "tenant-a", "eventstore:bound-domain", "folders"), selected.bindings());
        ObjectNode expiring = grant().put("expiresAt", NOW + 40);
        assertEquals(NOW + 40, authorize(expiring, details()).expiresAt());
        assertEquals(NOW + 12, WorkloadBindingGrantPolicy.authorize("folders-client", "folders", SCOPE, details(), enrollment(grant()), NOW, NOW + 12).expiresAt());
    }

    @ParameterizedTest @ValueSource(strings = {"clientId", "workload", "audience", "grantId", "revision", "notBefore", "expiresAt", "revoked", "tenant", "domain", "operation"})
    void currentExactAdmissionRejectsEveryWrongDimension(String field) {
        ObjectNode bad = grant();
        switch (field) {
            case "revision" -> bad.put(field, 0);
            case "notBefore" -> bad.put(field, NOW + 1);
            case "expiresAt" -> bad.put(field, NOW);
            case "revoked" -> bad.put(field, true);
            case "grantId" -> bad.put(field, "");
            case "tenant", "domain" -> ((ObjectNode)bad.get("bindings")).putArray("eventstore:bound-" + field).add("other");
            case "operation" -> bad.putArray("operations").add("eventstore:command-submit");
            default -> bad.put(field, "other");
        }
        assertEquals("workload_binding_denied", assertThrows(IllegalArgumentException.class, () -> authorize(bad, details())).getMessage());
    }

    @Test void duplicateAmbiguousAndOldRevisionsAreDenied() {
        ObjectNode first = grant(), second = grant().put("grantId", "second");
        for (ArrayNode grants : new ArrayNode[] {JSON.createArrayNode().add(first).add(second), JSON.createArrayNode().add(first).add(first.deepCopy().put("revision", 2))})
            assertThrows(IllegalArgumentException.class, () -> WorkloadBindingGrantPolicy.authorize("folders-client", "folders", SCOPE, details(), grants.toString(), NOW, NOW + 600));
        ObjectNode revoked = grant().put("revoked", true);
        assertThrows(IllegalArgumentException.class, () -> authorize(revoked, details()));
        // A reload is read afresh: revoked then replacement exact current grant, no process cache.
        assertNotNull(authorize(grant().put("revision", 2), details()));
    }

    @ParameterizedTest @ValueSource(strings = {"missing", "extra", "duplicate", "oversized", "unknown", "depth", "trailing", "wildcard"})
    void malformedAndIncompleteBindingSetsAreDenied(String scenario) {
        String bad = switch (scenario) {
            case "missing" -> details().replace(",\"eventstore:bound-domain\":\"folders\"", "");
            case "extra" -> details().replace("\"folders\"}", "\"folders\",\"eventstore:bound-actor\":\"extra\"}");
            case "duplicate" -> details().replace("\"tenant-a\"", "\"tenant-a\",\"eventstore:bound-tenant\":\"tenant-a\"");
            case "oversized" -> "x".repeat(8193);
            case "unknown" -> details().replace("\"bindings\":", "\"unknown\":0,\"bindings\":");
            case "depth" -> "[[[[[[0]]]]]]";
            case "trailing" -> details() + " []";
            default -> details().replace("tenant-a", "*");
        };
        assertThrows(IllegalArgumentException.class, () -> authorize(grant(), bad));
    }

    @Test void callbackBootstrapAndTenantSourceRequireIndependentExplicitUnboundGrants() {
        assertThrows(IllegalArgumentException.class, () -> authorize(grant(), null));
        for (String operation : new String[] {"domain-service:process", "domain-service:project", "domain-service:metadata", "eventstore:command-submit", "eventstore:stream-read"}) {
            ObjectNode exact = grant().put("allowUnbound", true);
            exact.putArray("operations").add(operation);
            String scope = "eventstore-audience.eventstore eventstore-operation." + operation.replace(':', '.');
            assertTrue(WorkloadBindingGrantPolicy.authorize("folders-client", "folders", scope, null, enrollment(exact), NOW, NOW + 600).bindings().isEmpty());
        }
    }

    @Test void queryDigestIsDynamicOnlyUnderItsSeparateExactTenantDomainGrant() throws Exception {
        ObjectNode queryGrant = grant().put("allowQueryAuthorityDigest", true);
        queryGrant.putArray("operations").add("domain-service:query");
        ObjectNode request = (ObjectNode)JSON.readTree(details()).get(0);
        request.put("operation", "domain-service:query");
        ((ObjectNode)request.get("bindings")).put(WorkloadBindingGrantPolicy.QUERY_DIGEST, "A".repeat(64));
        String scope = "eventstore-audience.eventstore eventstore-operation.domain-service.query";
        assertNotNull(WorkloadBindingGrantPolicy.authorize("folders-client", "folders", scope, JSON.createArrayNode().add(request).toString(), enrollment(queryGrant), NOW, NOW + 600));
        queryGrant.put("allowQueryAuthorityDigest", false);
        assertThrows(IllegalArgumentException.class, () -> WorkloadBindingGrantPolicy.authorize("folders-client", "folders", scope, JSON.createArrayNode().add(request).toString(), enrollment(queryGrant), NOW, NOW + 600));
    }
}
