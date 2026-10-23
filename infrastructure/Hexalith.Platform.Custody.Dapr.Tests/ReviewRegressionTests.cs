using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;

internal static class ReviewRegressionTests
{
    internal static Task StartupAndProfiles()
    {
        using var f = new Fixture(); string directory = Directory.CreateTempSubdirectory("custody-startup-guard-").FullName;
        string socket = Path.Combine(directory, "active.sock"); File.WriteAllText(socket, "fixture-owned-path");
        StartupGuard.Validate("--worker", socket); StartupGuard.Validate("--worker", Path.Combine(directory, "absent", "socket"));
        Refused(() => StartupGuard.Validate("--serve", socket));
        if (OperatingSystem.IsLinux()) { File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); StartupGuard.Validate("--serve", Path.Combine(directory, "new.sock")); }
        foreach (var changed in new[] {
            f.Profile with { Epochs = f.Profile.Epochs.Select((e,i) => i == 1 ? e with { KeyArn = f.Profile.Epochs[0].KeyArn } : e).ToArray() },
            f.Profile with { Epochs = f.Profile.Epochs.Select((e,i) => i == 1 ? e with { KeyId = f.Profile.Epochs[0].KeyId } : e).ToArray() },
            f.Profile with { Epochs = f.Profile.Epochs.Select((e,i) => i == 0 ? e with { KeyArn = f.Profile.Keks[0].ProtectorArn } : e).ToArray() },
            f.Profile with { Epochs = f.Profile.Epochs.Select((e,i) => i == 0 ? e with { KeyId = f.Profile.Keks[0].ProtectorKeyId } : e).ToArray() },
            f.Profile with { Epochs = f.Profile.Epochs.Where(e => e.OpaqueId != "other-e2").ToArray() },
            f.Profile with { Keks = f.Profile.Keks.Select(k => k with { TenantId = "tenant" }).ToArray() },
        }) Refused(changed.Validate);
        return Task.CompletedTask;
    }
    internal static async Task DeliveryTuples()
    {
        using var f = new Fixture(); var registration = (await f.Call(f.Wrap("tuple-root"))).WrappedRegistration!;
        var first = new CustodyBindingKeyUseV1("a:b", "c", "recipient", "transport", registration.OpaqueWrappedKeyReference);
        var second = first with { KeyUseLeaseId = "a", DeliveryAttemptId = "b:c" };
        if (CustodyJson.DeliveryId(first) == CustodyJson.DeliveryId(second)) throw new Exception("Canonical delivery tuples collided.");
        foreach (var use in new[] { first, second }) {
            var result = await f.Call(f.Request("unwrap", registration, keyUse: use));
            if (result.KeyDelivery is null) throw new Exception("Distinct colon tuple skipped actual key delivery.");
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(result.KeyDelivery.RootKey);
            await f.CloseKey(registration, use);
        }
        var before = f.Store.State.Head;
        await RefusedAsync(() => f.Call(f.Request("unwrap", registration, keyUse: first with { RecipientProcessIncarnation = "replacement-process" })));
        await RefusedAsync(() => f.Call(f.Request("unwrap", registration, keyUse: first with { TransportIncarnation = "replacement-transport" })));
        if (f.Native.Decrypts != 2 || f.Store.State.Head != before) throw new Exception("Duplicate changed delivery use emitted a key or changed authority.");
    }
    internal static async Task SignedHoldReservations()
    {
        using var f = new Fixture(); var registration = (await f.Call(f.Wrap("held-root"))).WrappedRegistration!;
        var request = f.Pin(registration, false); var before = f.Store.State.Head;
        foreach (Func<ControlClaim, ControlClaim> fault in new Func<ControlClaim, ControlClaim>[] {
            c => c with { HoldReservation = null },
            c => c with { HoldReservation = c.HoldReservation! with { HoldId = "substituted-hold" } },
            c => c with { HoldReservation = c.HoldReservation! with { TargetDigest = new string('B',64) } },
            c => c with { EffectiveDispatchRevision = 7, EffectiveDispatchReceiptId = "committed-dispatch" },
        }) await RefusedAsync(() => f.Call(request, changeControl: fault));
        if (f.Store.State.Checkpoint.Cycle is not null || f.Store.State.Checkpoint.Pins.Count != 0 || f.Store.State.Head != before)
            throw new Exception("Signed invalid or post-dispatch hold changed pre-cycle authority.");
        await f.Call(request);
        if (f.Store.State.Checkpoint.Pins.Count != 1) throw new Exception("Valid current hold reservation fixture never acquired its pin.");
    }
    private static void Refused(Action action) { try { action(); } catch (AuthorityUnavailableException) { return; } throw new Exception("Expected refusal."); }
    private static async Task RefusedAsync(Func<Task> action) { try { await action(); } catch (AuthorityUnavailableException) { return; } throw new Exception("Expected refusal."); }
}
