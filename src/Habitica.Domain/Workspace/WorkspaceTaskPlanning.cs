namespace Habitica.Domain.Workspace;

public sealed record WorkspaceTaskPlanning(DateOnly? PlannedDate = null, bool ClearPlannedDate = false,
    Guid? CategoryId = null, bool ClearCategory = false, Guid? ProjectId = null, bool ClearProject = false,
    Guid? SectionId = null, bool ClearSection = false, IReadOnlyList<Guid>? LabelIds = null)
{
    public bool IsValid() => this != new WorkspaceTaskPlanning() && PlannedDate != DateOnly.MinValue
        && CategoryId != Guid.Empty && ProjectId != Guid.Empty && SectionId != Guid.Empty
        && !(ClearPlannedDate && PlannedDate is not null || ClearCategory && CategoryId is not null
            || ClearProject && ProjectId is not null || ClearSection && SectionId is not null)
        && (LabelIds is null || LabelIds.Count <= 100 && LabelIds.All(id => id != Guid.Empty) && LabelIds.Distinct().Count() == LabelIds.Count);

    public CompanionTask Apply(CompanionTask task) => task with
    {
        PlannedDate = ClearPlannedDate ? null : PlannedDate ?? task.PlannedDate,
        CategoryId = ClearCategory ? null : CategoryId ?? task.CategoryId,
        ProjectId = ClearProject ? null : ProjectId ?? task.ProjectId,
        SectionId = ClearSection ? null : SectionId ?? task.SectionId,
        LabelIds = LabelIds ?? task.LabelIds
    };

    public bool ReferencesAvailable(WorkspaceDocument document, CompanionTask task)
    {
        if (CategoryId is { } category && !document.Categories.Any(row => row.Id == category && !row.IsArchived)
            || ProjectId is { } project && !document.Projects.Any(row => row.Id == project && !row.IsArchived)
            || SectionId is { } section && !document.Sections.Any(row => row.Id == section && !row.IsArchived)
            || LabelIds is not null && LabelIds.Any(id => !document.Labels.Any(row => row.Id == id && !row.IsArchived))) return false;
        var planned = Apply(task);
        if (SectionId is not null && !document.Projects.Any(row => row.Id == planned.ProjectId && !row.IsArchived)) return false;
        return planned.SectionId is null || document.Sections.Any(row => row.Id == planned.SectionId && row.ProjectId == planned.ProjectId);
    }

    public bool Matches(CompanionTask? task) => task is not null && IsValid()
        && (PlannedDate is null || task.PlannedDate == PlannedDate) && (!ClearPlannedDate || task.PlannedDate is null)
        && (CategoryId is null || task.CategoryId == CategoryId) && (!ClearCategory || task.CategoryId is null)
        && (ProjectId is null || task.ProjectId == ProjectId) && (!ClearProject || task.ProjectId is null)
        && (SectionId is null || task.SectionId == SectionId) && (!ClearSection || task.SectionId is null)
        && (LabelIds is null || task.LabelIds is not null && task.LabelIds.SequenceEqual(LabelIds));
}
