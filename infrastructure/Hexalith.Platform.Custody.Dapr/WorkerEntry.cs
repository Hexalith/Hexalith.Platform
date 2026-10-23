namespace Hexalith.Platform.Custody.Dapr;
internal sealed record WorkerPacket(string Action, string TenantId, WorkerOperation? Operation, SignedStatement Evidence, byte[]? ExactOriginal);
internal static class WorkerEntry
{
    internal static Task ExecuteAsync(RecoverableLifecycle worker, WorkerPacket packet, CancellationToken token, KeyExchange? exchange = null) => packet.Action switch {
        "recover-wrap-result" when packet.Operation is not null && exchange is not null => exchange.RecoverWrapAsync(packet.TenantId, packet.Operation, packet.Evidence, token),
        "select-successor" when packet.Operation is not null => worker.SelectSuccessorAsync(packet.TenantId, packet.Operation, packet.Evidence, token),
        "copy-survivor" when packet.Operation is not null => worker.CopySurvivorAsync(packet.TenantId, packet.Operation, packet.Evidence, token),
        "close-native-intent" => worker.CloseNativeIntentAsync(packet.TenantId, packet.Evidence, token),
        "recover-native-result" => worker.RecoverNativeResultAsync(packet.TenantId, packet.Evidence, token),
        "retire-alternate" when packet.Operation is not null => worker.RetireAlternateAsync(packet.TenantId, packet.Operation, packet.Evidence, token),
        "abandon-successor" => worker.AbandonSuccessorAsync(packet.TenantId, packet.Evidence, token),
        "arm" => worker.ArmAsync(packet.TenantId, packet.Evidence, token),
        "schedule-cut" when packet.Operation is not null => worker.ScheduleCutAsync(packet.TenantId, packet.Operation, packet.Evidence, token),
        "publish" when packet.ExactOriginal is not null => worker.PublishAsync(packet.TenantId, packet.Evidence, packet.ExactOriginal, token),
        _ => throw new AuthorityUnavailableException("Unknown or incomplete worker command."),
    };
}
