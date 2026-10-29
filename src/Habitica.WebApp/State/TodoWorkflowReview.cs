namespace Habitica.WebApp.State;

public sealed record TodoWorkflowReview(TodoWorkspaceSelection Source,long Revision,WorkspaceTaskInput Input);
