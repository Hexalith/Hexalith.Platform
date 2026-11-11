package com.hexalith.security;

import jakarta.ws.rs.BadRequestException;
import java.time.Instant;
import java.util.List;
import org.keycloak.crypto.KeyUse;
import org.keycloak.crypto.KeyWrapper;
import org.keycloak.models.ClientSessionContext;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.ProtocolMapperModel;
import org.keycloak.models.UserSessionModel;
import org.keycloak.protocol.oidc.mappers.AbstractOIDCProtocolMapper;
import org.keycloak.protocol.oidc.mappers.OIDCAccessTokenMapper;
import org.keycloak.provider.ProviderConfigProperty;
import org.keycloak.representations.AccessToken;

/** Maps only the approved exchange; Keycloak retains credential verification and RSA signing. */
public final class WorkloadBindingProtocolMapper extends AbstractOIDCProtocolMapper implements OIDCAccessTokenMapper {
    @Override public String getId() { return WorkloadBindingClientPolicyExecutor.ID; }
    @Override public String getDisplayType() { return "Hexalith workload binding v1"; }
    @Override public String getDisplayCategory() { return TOKEN_MAPPER_CATEGORY; }
    @Override public String getHelpText() { return "Maps exact client-policy admission; refuses missing admission or unusable realm signer."; }
    @Override public List<ProviderConfigProperty> getConfigProperties() { return List.of(); }
    @Override public int getPriority() { return Integer.MAX_VALUE; }

    @Override public AccessToken transformAccessToken(AccessToken token, ProtocolMapperModel model, KeycloakSession session,
        UserSessionModel userSession, ClientSessionContext clientSession) {
        try {
            var admission = (WorkloadBindingGrantPolicy.Admission) session.getAttribute(WorkloadBindingClientPolicyExecutor.ADMISSION);
            if (admission == null || !"true".equals(session.getContext().getRealm().getAttribute(WorkloadBindingClientPolicyExecutor.ENABLED))
                || !session.getContext().getClient().getClientId().equals(clientSession.getClientSession().getClient().getClientId())
                || !"RS256".equals(session.getContext().getRealm().getDefaultSignatureAlgorithm())
                || session.getContext().getClient().getAttribute("access.token.signed.response.alg") != null
                    && !"RS256".equals(session.getContext().getClient().getAttribute("access.token.signed.response.alg"))) throw new IllegalArgumentException();
            KeyWrapper key = session.keys().getActiveKey(session.getContext().getRealm(), KeyUse.SIG, "RS256");
            long now = Instant.now().getEpochSecond();
            if (key == null || key.getCertificate() == null || key.getCertificate().getNotBefore().toInstant().getEpochSecond() > now) throw new IllegalArgumentException();
            long expiry = Math.min(admission.expiresAt(), key.getCertificate().getNotAfter().toInstant().getEpochSecond());
            if (token.getExp() != null) expiry = Math.min(expiry, token.getExp());
            if (expiry <= now) throw new IllegalArgumentException();
            // Reject conflicting protected mapper output; never combine grants from other mappers.
            if (token.getOtherClaims().keySet().stream().anyMatch(name -> name.startsWith(WorkloadBindingGrantPolicy.PREFIX))) throw new IllegalArgumentException();
            token.audience(admission.audience()).issuedFor(admission.workload()).iat(now).nbf(now).exp(expiry);
            token.getOtherClaims().put("eventstore:operation", admission.operation());
            token.getOtherClaims().put("eventstore:surface", "service");
            token.getOtherClaims().put("eventstore:grant-id", admission.grantId());
            token.getOtherClaims().put("eventstore:grant-revision", admission.revision());
            admission.bindings().forEach((name, value) -> token.getOtherClaims().put(name, value));
            return token;
        } catch (RuntimeException denied) {
            throw new BadRequestException("workload_binding_denied");
        }
    }
}
