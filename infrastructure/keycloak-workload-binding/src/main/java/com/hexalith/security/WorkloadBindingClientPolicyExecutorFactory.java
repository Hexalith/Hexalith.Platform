package com.hexalith.security;

import java.util.List;
import org.keycloak.Config;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;
import org.keycloak.provider.ProviderConfigProperty;
import org.keycloak.services.clientpolicy.executor.ClientPolicyExecutorProvider;
import org.keycloak.services.clientpolicy.executor.ClientPolicyExecutorProviderFactory;

/** Registers the provider with Keycloak's supported client-policy SPI. */
public final class WorkloadBindingClientPolicyExecutorFactory implements ClientPolicyExecutorProviderFactory {
    @Override public ClientPolicyExecutorProvider create(KeycloakSession session) { return new WorkloadBindingClientPolicyExecutor(session); }
    @Override public String getId() { return WorkloadBindingClientPolicyExecutor.ID; }
    @Override public String getHelpText() { return "Requires exact current protected workload-binding enrollment after client authentication."; }
    @Override public List<ProviderConfigProperty> getConfigProperties() { return List.of(); }
    @Override public void init(Config.Scope config) { }
    @Override public void postInit(KeycloakSessionFactory factory) { }
    @Override public void close() { }
}
