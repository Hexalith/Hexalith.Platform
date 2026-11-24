package com.hexalith.security;

import java.util.List;
import org.keycloak.Config;
import org.keycloak.OAuthErrorException;
import org.keycloak.jose.jws.JWSInput;
import org.keycloak.jose.jws.JWSInputException;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;
import org.keycloak.protocol.oidc.TokenExchangeContext;
import org.keycloak.provider.ProviderConfigProperty;
import org.keycloak.representations.AccessToken;
import org.keycloak.services.clientpolicy.ClientPolicyContext;
import org.keycloak.services.clientpolicy.ClientPolicyException;
import org.keycloak.services.clientpolicy.context.TokenExchangeRequestContext;
import org.keycloak.services.clientpolicy.executor.ClientPolicyExecutorProvider;
import org.keycloak.services.clientpolicy.executor.ClientPolicyExecutorProviderFactory;

/** Closes the standard token exchange exception for a requester exchanging its own token. */
public final class RequesterAudiencePolicyFactory implements ClientPolicyExecutorProviderFactory {
    private static final String ID = "platform-require-requester-audience";

    @Override
    public ClientPolicyExecutorProvider create(KeycloakSession session) {
        return new Executor();
    }

    @Override public String getId() { return ID; }
    @Override public String getHelpText() { return "Require the authenticated requester in the subject token audience."; }
    @Override public List<ProviderConfigProperty> getConfigProperties() { return List.of(); }
    @Override public void init(Config.Scope config) { }
    @Override public void postInit(KeycloakSessionFactory factory) { }
    @Override public void close() { }

    private static final class Executor implements ClientPolicyExecutorProvider {
        @Override
        public String getProviderId() {
            return ID;
        }

        @Override
        public void executeOnEvent(ClientPolicyContext context) throws ClientPolicyException {
            if (!(context instanceof TokenExchangeRequestContext request)) {
                return;
            }

            TokenExchangeContext exchange = request.getTokenExchangeContext();
            String subject = exchange.getParams().getSubjectToken();
            if (subject == null) {
                throw new ClientPolicyException(OAuthErrorException.INVALID_REQUEST, "Subject access token is required");
            }

            AccessToken token;
            try {
                token = new JWSInput(subject).readJsonContent(AccessToken.class);
            } catch (JWSInputException exception) {
                throw new ClientPolicyException(OAuthErrorException.INVALID_REQUEST, "Invalid subject access token");
            }

            // This check can only deny. Keycloak still validates the subject signature and all other exchange conditions.
            if (token == null || !token.hasAudience(exchange.getClient().getClientId())) {
                throw new ClientPolicyException(OAuthErrorException.INVALID_REQUEST, "Requester is not an audience of the subject token");
            }
        }
    }
}
