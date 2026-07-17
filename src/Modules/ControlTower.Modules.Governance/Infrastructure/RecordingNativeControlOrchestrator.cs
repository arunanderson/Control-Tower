using ControlTower.Modules.Governance.Application;

namespace ControlTower.Modules.Governance.Infrastructure;

/// <summary>
/// Records native-control requests as intents and performs NO enforcement (ADR-002). Enforcement is
/// delegated to native platforms via C4.6 control adapters (V2). This makes it structurally true that
/// C2 never enforces — the receipt always reports Enforced=false.
/// </summary>
public sealed class RecordingNativeControlOrchestrator : INativeControlOrchestrator
{
    public Task<NativeControlReceipt> RequestAsync(NativeControlIntent intent, CancellationToken ct = default) =>
        Task.FromResult(new NativeControlReceipt(
            Recorded: true,
            Enforced: false,
            Note: "Recorded as intent; enforcement delegated to the native platform (ADR-002). C2 performs no enforcement."));
}
