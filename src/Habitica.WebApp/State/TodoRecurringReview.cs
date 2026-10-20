using Habitica.Domain.Workspace;

namespace Habitica.WebApp.State;

public sealed record TodoRecurringReview(TodoWorkspaceSelection Source, long Revision, Guid TaskId,
    WorkspaceOperation Operation, RecurrenceRule? Recurrence = null, bool ClearRecurrence = false, DateOnly? OccurrenceDate = null);
