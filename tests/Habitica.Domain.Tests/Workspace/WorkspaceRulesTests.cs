using System.Text.Json;
using Habitica.Domain.Workspace;

namespace Habitica.Domain.Tests.Workspace;

public sealed class WorkspaceRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
    private static CompanionTask Todo(Guid? id = null) => new(id ?? Guid.NewGuid(), 1, "Read chapter", CompanionTaskKind.Todo, CompanionTaskState.Active, CompanionPriority.Normal, Now, Now);
    private static WorkspaceDocument Workspace(params CompanionTask[] tasks) => new() { Id = Guid.NewGuid(), Tasks = tasks };

    [Fact]
    public void IndependentEntitiesRoundTripWithStableRelationshipsAndProjectionMapping()
    {
        var category = new WorkspaceCategory(Guid.NewGuid(), 1, "Personal");
        var project = new WorkspaceProject(Guid.NewGuid(), 1, "Learning", category.Id);
        var section = new WorkspaceSection(Guid.NewGuid(), 1, "Next", project.Id);
        var label = new WorkspaceLabel(Guid.NewGuid(), 1, "Reading");
        var task = Todo() with { CategoryId = category.Id, ProjectId = project.Id, SectionId = section.Id, LabelIds = new[] { label.Id }, PlannedDate = new DateOnly(2026, 10, 16), Notes = "Keep notes private" };
        var child = Todo() with { ParentTaskId = task.Id };
        var externalId = Guid.NewGuid().ToString("D");
        var doc = Workspace(task, child) with { Categories = new[] { category }, Projects = new[] { project }, Sections = new[] { section }, Labels = new[] { label }, Goals = new[] { new WorkspaceGoal(Guid.NewGuid(), 1, "Read books", new[] { task.Id }) }, Projections = new[] { new HabiticaProjection(Guid.NewGuid(), 1, task.Id, ProjectionState.Linked, externalId) } };
        Assert.Empty(WorkspaceRules.Validate(doc));
        var json = WorkspaceJson.Serialize(doc);
        var restored = WorkspaceJson.Deserialize(json);
        Assert.Equal(json, WorkspaceJson.Serialize(restored));
        Assert.Equal(task.Id, restored.Tasks[0].Id);
        Assert.NotEqual(task.Id.ToString("D"), externalId);
        Assert.Equal(externalId, restored.Projections[0].HabiticaTaskId);
        Assert.Equal(task.Id, restored.Tasks[1].ParentTaskId);
    }

    [Fact]
    public void GoalProgressExcludesArchivedCancelledAndRecurringTasks()
    {
        var active = Todo(); var completed = Todo() with { State = CompanionTaskState.Completed };
        var archived = Todo() with { IsArchived = true }; var cancelled = Todo() with { State = CompanionTaskState.Cancelled };
        var daily = Todo() with { Kind = CompanionTaskKind.Daily };
        var tasks = new[] { active, completed, archived, cancelled, daily };
        var goal = new WorkspaceGoal(Guid.NewGuid(), 1, "Progress", tasks.Select(task => task.Id).ToArray());
        var progress = WorkspaceRules.Progress(goal, tasks);
        Assert.Equal(1, progress.CompletedTodos); Assert.Equal(2, progress.EligibleTodos); Assert.Equal(0.5m, progress.Fraction);
        Assert.Null(WorkspaceRules.Progress(goal with { TaskIds = Array.Empty<Guid>() }, tasks).Fraction);
    }

    [Fact]
    public void RejectsCrossProjectSectionsMissingLabelsAndDuplicateIdentities()
    {
        var project = new WorkspaceProject(Guid.NewGuid(), 1, "A"); var other = new WorkspaceProject(Guid.NewGuid(), 1, "B");
        var section = new WorkspaceSection(Guid.NewGuid(), 1, "A section", project.Id);
        var task = Todo() with { ProjectId = other.Id, SectionId = section.Id, LabelIds = new[] { Guid.NewGuid() } };
        var violations = WorkspaceRules.Validate(Workspace(task, task) with { Projects = new[] { project, other }, Sections = new[] { section } });
        Assert.Contains(violations, value => value.Code == "missing_reference");
        Assert.Contains(violations, value => value.Code == "invalid_labels");
        Assert.Contains(violations, value => value.Code == "invalid_or_duplicate_id");
    }

    [Fact]
    public void RejectsDependencyAndParentCyclesWithoutRecursiveTraversal()
    {
        var first = Todo(); var second = Todo();
        var cycle = Workspace(first with { DependsOn = new[] { second.Id } }, second with { DependsOn = new[] { first.Id } });
        Assert.Contains(WorkspaceRules.Validate(cycle), value => value.Code == "dependency_cycle");
        var parentCycle = Workspace(first with { ParentTaskId = second.Id }, second with { ParentTaskId = first.Id });
        Assert.Contains(WorkspaceRules.Validate(parentCycle), value => value.Code == "dependency_cycle");
        var chain = Enumerable.Range(0, 2000).Select(_ => Todo()).ToArray();
        for (var i = 1; i < chain.Length; i++) chain[i] = chain[i] with { DependsOn = new[] { chain[i - 1].Id } };
        Assert.Empty(WorkspaceRules.Validate(Workspace(chain)));
    }

    [Fact]
    public void CompletionAndHabitScoreHistoryRemainSeparateFromGameProjection()
    {
        var todo = Todo() with { State = CompanionTaskState.Completed };
        var daily = Todo() with { Kind = CompanionTaskKind.Daily };
        var habit = Todo() with { Kind = CompanionTaskKind.Habit };
        var occurrence = new TaskOccurrence(Guid.NewGuid(), 1, daily.Id, new DateOnly(2026, 10, 15), OccurrenceState.Completed);
        var todoEvent = new CompletionEvent(Guid.NewGuid(), 1, todo.Id, Guid.NewGuid(), Now);
        var dailyEvent = new CompletionEvent(Guid.NewGuid(), 1, daily.Id, Guid.NewGuid(), Now, occurrence.Id);
        var command = Guid.NewGuid();
        var scores = new[] { new HabitScoreEvent(Guid.NewGuid(), 1, habit.Id, command, Now, HabitScoreDirection.Up), new HabitScoreEvent(Guid.NewGuid(), 1, habit.Id, command, Now, HabitScoreDirection.Up) };
        var doc = Workspace(todo, daily, habit) with { Occurrences = new[] { occurrence }, Completions = new[] { todoEvent, dailyEvent }, HabitScores = scores };
        Assert.Empty(WorkspaceRules.Validate(doc)); Assert.Empty(doc.Projections);
        Assert.Contains(WorkspaceRules.Validate(doc with { Completions = Array.Empty<CompletionEvent>() }), value => value.Code == "missing_completion_event");
        Assert.Contains(WorkspaceRules.Validate(doc with { Completions = new[] { todoEvent, todoEvent with { Id = Guid.NewGuid() }, dailyEvent } }), value => value.Code == "invalid_completion");
        Assert.Contains(WorkspaceRules.Validate(Workspace(habit with { State = CompanionTaskState.Completed })), value => value.Code == "invalid_lifecycle");
    }

    [Fact]
    public void RecurrenceRequiresExplicitScheduleAndPreservesUnknownDueState()
    {
        var daily = Todo() with { Kind = CompanionTaskKind.Daily, Recurrence = new(RecurrenceFrequency.Weekly, 2, new DateOnly(2026, 10, 15), "Asia/Novosibirsk", new[] { DayOfWeek.Thursday }, Array.Empty<int>()) };
        Assert.Empty(WorkspaceRules.Validate(Workspace(daily)));
        Assert.Contains(WorkspaceRules.Validate(Workspace(daily with { Recurrence = daily.Recurrence! with { Weekdays = Array.Empty<DayOfWeek>() } })), value => value.Code == "invalid_recurrence");
        Assert.Contains(WorkspaceRules.Validate(Workspace(daily with { Kind = CompanionTaskKind.Todo })), value => value.Code == "invalid_recurrence_kind");
        Assert.DoesNotContain("isDue", WorkspaceJson.Serialize(Workspace(daily)));
    }

    [Fact]
    public void StrictImportsRejectFutureSchemasUnknownFieldsNumericEnumsAndDuplicateProperties()
    {
        var json = WorkspaceJson.Serialize(Workspace(Todo()));
        Assert.Throws<JsonException>(() => WorkspaceJson.Deserialize(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":2")));
        Assert.Throws<JsonException>(() => WorkspaceJson.Deserialize(json.Replace("\"revision\":1", "\"revision\":9007199254740992")));
        Assert.Throws<JsonException>(() => WorkspaceJson.Deserialize(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1")));
        Assert.Throws<JsonException>(() => WorkspaceJson.Deserialize(json.Replace("\"kind\":\"todo\"", "\"kind\":0")));
        Assert.Throws<JsonException>(() => WorkspaceJson.Deserialize(json.Replace("\"kind\":\"todo\",", "")));
        Assert.Throws<JsonException>(() => WorkspaceJson.Deserialize(json.Replace("\"schemaVersion\":1", "\"unexpected\":true,\"schemaVersion\":1")));
        Assert.Throws<JsonException>(() => WorkspaceJson.Deserialize(json.Replace("\"categories\":[]", "\"categories\":null")));
    }

    [Fact]
    public void NeutralFixtureIsPortableAndContainsNoHabiticaCredentials()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "workspace-v1.json"));
        var doc = WorkspaceJson.Deserialize(json);
        Assert.Single(doc.Tasks); Assert.Single(doc.Projects);
        Assert.DoesNotContain("apiToken", json); Assert.Empty(WorkspaceRules.Validate(doc));
        Assert.Equal(doc.Id, WorkspaceJson.Deserialize(WorkspaceJson.Serialize(doc)).Id);
    }
}
