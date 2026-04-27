using Habitica.Domain.Diagnostics;

namespace Habitica.Storage;

public interface IDiagnosticsLogStore
{
    Task<IReadOnlyList<DiagnosticsLogEntry>> GetRecentAsync(CancellationToken cancellationToken);

    Task AppendAsync(DiagnosticsLogEntry entry, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
