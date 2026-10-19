using Habitica.Application.Workspace;
using Habitica.Domain.Diagnostics;

namespace Habitica.WebApp.Integration;

public sealed partial class WorkspaceCloudClient
{
    private Task<WorkspaceTaskResponse<T>> ObserveResponseAsync<T>(string operation, Func<Task<WorkspaceTaskResponse<T>>> action, Guid? commandId = null, Guid? correlationId = null)
    {
        if (_diagnostics is null) return action();
        var generation = _generation;
        return _diagnostics.ObserveAsync(DiagnosticsFeatureArea.Workspace, operation, action, response =>
        {
            var result = response.OutcomeUnknown ? DiagnosticsResult.Unknown : response.Succeeded ? DiagnosticsResult.Success : DiagnosticsResult.Unavailable;
            return response.Value switch
            {
                WorkspaceOrganizationPreview preview when response.Succeeded => new(result, CorrelationId: preview.CorrelationId, CommandId: preview.PreparedCommandId),
                WorkspaceOrganizationReceipt receipt when response.Succeeded => new(result, CorrelationId: receipt.CorrelationId, CommandId: receipt.CommandId,
                    AcceptedCount: receipt.AcceptedEventCount, ConfirmedCount: receipt.ConfirmedEffectCount, RequestedCount: 0),
                _ => new(result, CorrelationId: correlationId, CommandId: commandId)
            };
        }, () => generation == _generation,
            operation is "workspace-organization-recover" or "workspace-saved-list" or "workspace-saved-query" ? DiagnosticsMode.LiveRead : DiagnosticsMode.LiveMutation);
    }
}
