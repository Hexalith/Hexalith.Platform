package com.hexalith.security;

import jakarta.ws.rs.core.MultivaluedMap;
import java.time.Instant;
import java.util.Set;
import org.keycloak.models.ClientModel;
import org.keycloak.models.KeycloakSession;
import org.keycloak.representations.idm.ClientPolicyExecutorConfigurationRepresentation;
import org.keycloak.services.clientpolicy.ClientPolicyContext;
import org.keycloak.services.clientpolicy.ClientPolicyException;
import org.keycloak.services.clientpolicy.context.ServiceAccountTokenRequestContext;
import org.keycloak.services.clientpolicy.executor.ClientPolicyExecutorProvider;

/** Runs after Keycloak authenticates the confidential service-account client. */
public final class WorkloadBindingClientPolicyExecutor implements ClientPolicyExecutorProvider<ClientPolicyExecutorConfigurationRepresentation> {
    public static final String ID = "hexalith-workload-binding-v1";
    static final String ADMISSION = WorkloadBindingClientPolicyExecutor.class.getName() + ".admission";
    static final String ENABLED = "hexalith.workload-binding.enabled";
    static final String SIGNER_END = "hexalith.workload-binding.signer-not-after";
    private final KeycloakSession session;

    public WorkloadBindingClientPolicyExecutor(KeycloakSession session) { this.session = session; }
    @Override public String getProviderId() { return ID; }

    @Override public void executeOnEvent(ClientPolicyContext context) throws ClientPolicyException {
        if (!(context instanceof ServiceAccountTokenRequestContext request)) return;
        ClientModel client = session.getContext().getClient();
        MultivaluedMap<String, String> form = request.getParams();
        boolean enrolled = client.getAttribute(WorkloadBindingGrantPolicy.GRANTS) != null;
        if (!enrolled && !form.containsKey("authorization_details")) return;
        try {
            if (!"true".equals(session.getContext().getRealm().getAttribute(ENABLED)) || client.isPublicClient()
                || !client.isServiceAccountsEnabled() || client.isFullScopeAllowed()) throw new IllegalArgumentException();
            Set<String> supported = Set.of("grant_type", "client_id", "client_secret", "scope", "authorization_details", "client_assertion", "client_assertion_type");
            if (form.size() > supported.size() || form.entrySet().stream().anyMatch(entry -> !supported.contains(entry.getKey())
                || entry.getValue() == null || entry.getValue().size() != 1 || entry.getValue().get(0) == null
                || entry.getValue().get(0).length() > (entry.getKey().equals("authorization_details") ? 8192 : 8192))
                || !"client_credentials".equals(form.getFirst("grant_type"))
                || form.containsKey("client_id") && !client.getClientId().equals(form.getFirst("client_id"))) throw new IllegalArgumentException();
            WorkloadBindingGrantPolicy.Admission admission = WorkloadBindingGrantPolicy.authorize(client.getClientId(),
                client.getAttribute(WorkloadBindingGrantPolicy.WORKLOAD), form.getFirst("scope"), form.getFirst("authorization_details"),
                client.getAttribute(WorkloadBindingGrantPolicy.GRANTS), Instant.now().getEpochSecond(),
                Long.parseLong(session.getContext().getRealm().getAttribute(SIGNER_END)));
            session.setAttribute(ADMISSION, admission);
        } catch (RuntimeException denied) {
            throw new ClientPolicyException("invalid_scope", "workload_binding_denied");
        }
    }
}
