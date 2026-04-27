namespace Habitica.Application.Diagnostics;

public enum DiagnosticsPreset
{
    UserAccount,
    UserInventory,
    TasksUser,
    Party
}

public sealed record DiagnosticsPresetRunResult(
    DiagnosticsPreset Preset,
    bool Succeeded,
    int RequestCount,
    string Summary,
    string ResponsePreview);
