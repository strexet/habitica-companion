using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
namespace Habitica.WebApp.Tests.Workspace;
public sealed class TodoExtendedRecoveryTests:BunitContext
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-27T12:00:00Z");
    private static WorkspaceJournalSnapshot Apply(WorkspaceJournalSnapshot state,WorkspaceOperation operation,Guid id,WorkspaceTaskPatch? patch=null,DateOnly? date=null)
    {var result=WorkspaceCommandEngine.Accept(state,new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,operation,id,patch,OccurrenceDate:date),Now.AddSeconds(state.Document.Revision));Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);return result.Snapshot!;}
    private static WorkspaceJournalSnapshot Native(CompanionTaskKind kind,bool archive,bool skip=false)
    {
        var id=Guid.NewGuid();var s=new WorkspaceJournalSnapshot(new WorkspaceDocument{Id=Guid.NewGuid()},1,[],[]);
        s=Apply(s,kind==CompanionTaskKind.Todo?WorkspaceOperation.CreateTodo:kind==CompanionTaskKind.Daily?WorkspaceOperation.CreateDaily:WorkspaceOperation.CreateHabit,id,new(Text:"Synthetic retained recovery",Recurrence:kind==CompanionTaskKind.Daily?new(RecurrenceFrequency.Daily,1,new(2026,10,27),"UTC",[],[]):null));
        if(kind!=CompanionTaskKind.Habit)s=Apply(s,skip?WorkspaceOperation.SkipOccurrence:WorkspaceOperation.CompleteTask,id,date:kind==CompanionTaskKind.Daily?new(2026,10,27):null);
        return archive?Apply(s,WorkspaceOperation.DeleteTask,id):s;
    }
    private async Task<(WorkspaceTaskFixture Fixture,TodoWorkspacePresentation Todo)> Setup(WorkspaceTaskFixture.Cloud client)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var f=new WorkspaceTaskFixture();client.Connected=true;
        var commands=new WorkspaceTaskController(f.Session,client,f.Database);await commands.LoadAsync(true);var todo=new TodoWorkspacePresentation(f.Session,client,f.Database,commands);
        Services.AddSingleton<IAppSessionController>(f.Session);Services.AddSingleton<IWorkspaceCloudClient>(client);Services.AddSingleton(f.Database);Services.AddSingleton(commands);Services.AddSingleton(todo);Services.AddSingleton<TodoTaskDraftStore>();return(f,todo);
    }
    private static void Save(string name,string markup,string state,string test)=>Qualification.PageLayoutEvidence.Save(name,markup,"/todo/history",state,["actions","confirmation","disabled-controls","focus","error"],test);
    [Theory][InlineData(CompanionTaskKind.Todo)][InlineData(CompanionTaskKind.Daily)][InlineData(CompanionTaskKind.Habit)]
    public async Task Archived_tasks_review_exact_unarchive_and_recover_original_zero_effect_receipt(CompanionTaskKind kind)
    {
        var before=Native(kind,true);var client=new WorkspaceTaskFixture.Cloud{State=before};var(f,todo)=await Setup(client);var cut=Render<TodoHistoryPanel>();var task=before.Document.Tasks[0];
        cut.Find($"[data-testid='recovery-record-open-{task.Id:D}']").Click();Assert.Contains("Unarchive preserves",cut.Markup);
        cut.Find("[data-testid='extended-native-review']").Click();var preview=todo.Commands.Preview!;Assert.Equal(WorkspaceOperation.UnarchiveTask,preview.Command.Operation);Assert.Equal(task.Revision,preview.Command.Patch!.Unarchive!.ArchivedRevision);Assert.False(preview.MayAffectHabitica);
        if(kind==CompanionTaskKind.Todo)Save("history-native-unarchive-review",cut.Markup,"exact-unarchive-review",nameof(Archived_tasks_review_exact_unarchive_and_recover_original_zero_effect_receipt));
        client.LoseAcceptance=true;cut.Find("[data-testid='todo-confirm']").Click();Assert.True(todo.Commands.AcceptanceUnknown);cut.Find("[data-testid='todo-recover']").Click();
        Assert.Equal(preview.PreparedCommandId,todo.Commands.LastReceipt!.CommandId);Assert.Single(client.AcceptedIds);Assert.False(todo.Snapshot!.Document.Tasks[0].IsArchived);Assert.Equal(task.State,todo.Snapshot.Document.Tasks[0].State);Assert.Equal(before.Document.Completions,todo.Snapshot.Document.Completions);Assert.Empty(todo.Snapshot.Commands[^1].Effects);
        Assert.Contains(JSInterop.Invocations,row=>row.Identifier=="Blazor._internal.domWrapper.focus");
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Terminal_Daily_completion_and_skip_have_separate_exact_native_correction(bool skip)
    {
        var before=Native(CompanionTaskKind.Daily,false,skip);var client=new WorkspaceTaskFixture.Cloud{State=before};var(f,todo)=await Setup(client);var cut=Render<TodoHistoryPanel>();var occurrence=before.Document.Occurrences[0];
        cut.Find("[data-testid='recovery-record-type']").Change("Occurrences");cut.Find($"[data-testid='recovery-record-open-{occurrence.Id:D}']").Click();
        if(skip)Save("history-occurrence-skip",cut.Markup,"retained-skip-and-native-recovery",nameof(Terminal_Daily_completion_and_skip_have_separate_exact_native_correction));
        cut.Find("[data-testid='extended-native-review']").Click();Assert.Equal(WorkspaceOperation.CorrectOccurrence,todo.Commands.Preview!.Command.Operation);Assert.Equal(occurrence.Id,todo.Commands.Preview.Command.Patch!.CorrectOccurrence!.OccurrenceId);Assert.Contains("No Habitica score",cut.Markup);
        if(!skip)Save("history-native-correction-review",cut.Markup,"exact-terminal-correction-review",nameof(Terminal_Daily_completion_and_skip_have_separate_exact_native_correction));
        cut.Find("[data-testid='todo-confirm']").Click();Assert.Equal(OccurrenceState.Pending,todo.Snapshot!.Document.Occurrences[0].State);Assert.Equal(before.Document.Completions,todo.Snapshot.Document.Completions);Assert.Equal(occurrence,todo.Snapshot.Document.OccurrenceCorrections![0].PreviousOccurrence);Assert.Empty(todo.Snapshot.Commands[^1].Effects);
    }
    [Fact]
    public async Task Archived_repair_and_retained_game_identity_explain_refusal_without_unlinking_or_erasing_history()
    {
        var before=Native(CompanionTaskKind.Todo,true);var project=new WorkspaceProject(Guid.NewGuid(),1,"Archived project",IsArchived:true);var task=before.Document.Tasks[0] with {ProjectId=project.Id};before=before with {Document=before.Document with {Tasks=[task],Projects=[project]}};
        var client=new WorkspaceTaskFixture.Cloud{State=before};var(f,todo)=await Setup(client);var cut=Render<TodoHistoryPanel>();cut.Find($"[data-testid='recovery-record-open-{task.Id:D}']").Click();Assert.Contains("explicit repair",cut.Markup);Assert.Empty(cut.FindAll("[data-testid='extended-native-review']"));Save("history-archived-repair",cut.Markup,"archived-reference-repair-required",nameof(Archived_repair_and_retained_game_identity_explain_refusal_without_unlinking_or_erasing_history));Assert.Empty(client.PreparationIds);
    }
    [Fact]
    public async Task Linked_uncheck_lost_acknowledgement_retains_request_filters_draft_and_original_receipt()
    {
        var client=new RecoveryCloud();var(f,todo)=await Setup(client);var cut=Render<TodoHistoryPanel>();var completion=client.Before.Document.Completions[0];cut.Find("[data-testid='history-query']").Change("Synthetic");
        var drafts=Services.GetRequiredService<TodoTaskDraftStore>();var draft=drafts.Get(todo.Selection!,CompanionTaskKind.Todo,client.Before.Document.Tasks[0]);draft.Text="Unsaved draft stays";
        cut.Find($"[data-testid='history-recovery-{completion.Id:D}']").Click();Save("history-recovery-options",cut.Markup,"separate-native-and-linked-actions",nameof(Linked_uncheck_lost_acknowledgement_retains_request_filters_draft_and_original_receipt));
        cut.Find("[data-testid='extended-linked-review']").Click();var id=todo.LinkedRecovery!.CommandId;Assert.True(todo.HasLinkedRecoveryIntent);Assert.False(todo.CanSwitchSource);Assert.Contains("No exact reward refund",cut.Markup);Assert.Equal(0,client.DownCalls);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>todo.PrepareAsync(WorkspaceTaskFixture.Todo()));Save("history-linked-review",cut.Markup,"fresh-exact-native-and-resource-review",nameof(Linked_uncheck_lost_acknowledgement_retains_request_filters_draft_and_original_receipt));
        client.LoseDownAcknowledgement=true;cut.Find("[data-testid='linked-recovery-confirm']").Click();Assert.True(todo.HasLinkedRecoveryIntent);Assert.True(todo.RecoveryAcceptanceAttempted);Assert.Contains("Acceptance response unconfirmed",cut.Markup);Assert.DoesNotContain("Use the cloud owner",cut.Markup);Assert.Equal(id,todo.LinkedRecovery.CommandId);Assert.True(cut.Find("[data-testid='linked-recovery-confirm']").HasAttribute("disabled"));Save("history-linked-unknown",cut.Markup,"uncertain-original-recovery",nameof(Linked_uncheck_lost_acknowledgement_retains_request_filters_draft_and_original_receipt));
        cut.Find("[data-testid='linked-recovery-receipt-read']").Click();Assert.False(todo.HasLinkedRecoveryIntent);Assert.Equal(id,todo.LinkedRecovery.Receipt!.Id);Assert.Equal(1,client.DownCalls);Assert.Contains("Original receipt confirmed",cut.Markup);Assert.Equal("Synthetic",cut.Find("[data-testid='history-query']").GetAttribute("value"));Assert.Equal("Unsaved draft stays",draft.Text);Save("history-linked-confirmed",cut.Markup,"original-receipt-and-preserved-history",nameof(Linked_uncheck_lost_acknowledgement_retains_request_filters_draft_and_original_receipt));
    }
    [Fact]
    public async Task Lost_preview_reuses_request_and_reopening_discovers_original_without_new_dispatch()
    {
        var client=new RecoveryCloud{LosePreview=true};var(f,todo)=await Setup(client);var completion=client.Before.Document.Completions[0];await todo.StartLinkedRecoveryAsync(completion.TaskId,completion.Id);var request=todo.RecoveryRequestId;Assert.Null(todo.LinkedRecovery);await todo.RetryLinkedRecoveryPreviewAsync();Assert.Equal(request,todo.RecoveryRequestId);Assert.Single(client.Requests.Distinct());Assert.Equal(0,client.DownCalls);
        var commands=new WorkspaceTaskController(f.Session,client,f.Database);await commands.LoadAsync(true);using var reopened=new TodoWorkspacePresentation(f.Session,client,f.Database,commands);await reopened.StartLinkedRecoveryAsync(completion.TaskId,completion.Id);Assert.Equal(todo.LinkedRecovery!.CommandId,reopened.LinkedRecovery!.CommandId);Assert.Equal(request,reopened.RecoveryRequestId);Assert.Equal(0,client.DownCalls);
        f.Session.SetState(f.Session.State with {UserId="00000000-0000-4000-8000-000000000012"});Assert.Null(todo.LinkedRecovery);Assert.False(todo.HasLinkedRecoveryIntent);Assert.Null(reopened.LinkedRecovery);
    }
    [Theory][InlineData("unverified")][InlineData("expired")][InlineData("stale")]
    public async Task Unsupported_or_changed_linked_recovery_gives_reason_without_replacing_intent(string control)
    {
        var client=new RecoveryCloud{RefuseEligibility=control=="unverified",ExpiredView=control=="expired"};var(f,todo)=await Setup(client);var cut=Render<TodoHistoryPanel>();var completion=client.Before.Document.Completions[0];
        cut.Find($"[data-testid='history-recovery-{completion.Id:D}']").Click();cut.Find("[data-testid='extended-linked-review']").Click();
        if(control=="unverified"){Assert.Contains("original_score_proof_unavailable",cut.Markup);Assert.False(todo.HasLinkedRecoveryIntent);Assert.Empty(cut.FindAll("[data-testid='linked-recovery-confirm']"));}
        else
        {
            var request=todo.RecoveryRequestId;
            if(control=="stale"){client.State=client.Before with {Document=client.Before.Document with {Revision=client.Before.Document.Revision+1},StoreVersion=client.Before.StoreVersion+1};await cut.InvokeAsync(()=>todo.LoadSelectedAsync());Assert.Contains("Workspace revision changed",cut.Markup);}
            else Assert.Contains("Review expired",cut.Markup);
            Assert.True(cut.Find("[data-testid='linked-recovery-confirm']").HasAttribute("disabled"));Assert.Equal(request,todo.RecoveryRequestId);Assert.True(todo.HasLinkedRecoveryIntent);
        }
        Assert.Equal(0,client.DownCalls);Save("history-linked-"+control,cut.Markup,"explicit-linked-"+control+"-refusal",nameof(Unsupported_or_changed_linked_recovery_gives_reason_without_replacing_intent));
    }
    [Fact]
    public async Task Archive_browser_search_uses_canonical_text_folding_without_preparing_or_losing_filter()
    {
        var client=new WorkspaceTaskFixture.Cloud{State=Native(CompanionTaskKind.Todo,true)};var(f,todo)=await Setup(client);var cut=Render<TodoHistoryPanel>();
        cut.Find("[data-testid='recovery-record-query']").Change("ＳＹＮＴＨＥＴＩＣ");Assert.Single(cut.FindAll("[data-testid^='recovery-record-open-']"));Assert.Empty(client.PreparationIds);Assert.Empty(client.AcceptedIds);
        await cut.InvokeAsync(()=>todo.LoadSelectedAsync());Assert.Equal("ＳＹＮＴＨＥＴＩＣ",cut.Find("[data-testid='recovery-record-query']").GetAttribute("value"));Assert.Single(cut.FindAll("[data-testid^='recovery-record-open-']"));
    }
    public static IEnumerable<object[]> SharedRouteCases()=>new[]{"tasks","dailies","habits","sync","reminders","inbox","plan","today","projects","task-tree","lists"}.SelectMany(route=>new[]{"unarchive","correction","linked-unknown"}.Select(state=>new object[]{route,state}));
    [Theory][MemberData(nameof(SharedRouteCases))]
    public async Task Shared_task_review_and_pending_recovery_preserve_identity_on_every_host_route(string route,string state)
    {
        WorkspaceTaskFixture.Cloud client=state=="linked-unknown"?new RecoveryCloud():new WorkspaceTaskFixture.Cloud(){State=Native(state=="correction"?CompanionTaskKind.Daily:CompanionTaskKind.Todo,state=="unarchive")};
        var(f,todo)=await Setup(client);Services.AddSingleton(TimeProvider.System);Services.AddSingleton<Habitica.Application.Notifications.IForegroundReminderBrowser>(new ReminderBrowserFixture());Services.AddSingleton<ForegroundReminderController>();Services.AddSingleton(new Habitica.Application.Tasks.TaskListViewModelFactory());Services.AddSingleton(new Habitica.Application.Tasks.TaskOrderPlanner());Services.AddSingleton<Habitica.Storage.IKeyValueStorage>(new ReadOnlyPageStorage());Services.AddSingleton(new WorkspaceDataController(f.Session,client,f.Database));f.Client.State=client.State;Services.AddSingleton(new WorkspaceOrganizationController(f.Session,client,new TodoOrganizationFixture.NativeApi(f),f.Database));Services.AddSingleton(new PushDeviceController(f.Session,new PushReminderFixture.Client(),new PushReminderFixture.Browser(),new PushReminderFixture.Store()));Services.AddSingleton(new TaskReminderController(f.Session,todo,new PushReminderFixture.Client(),new PushReminderFixture.Store()));
        Guid original;
        if(client is RecoveryCloud linked){var completion=linked.Before.Document.Completions[0];await todo.StartLinkedRecoveryAsync(completion.TaskId,completion.Id);linked.LoseDownAcknowledgement=true;await todo.AcceptLinkedRecoveryAsync();original=todo.LinkedRecovery!.CommandId;}
        else{var task=client.State.Document.Tasks[0];var occurrence=client.State.Document.Occurrences.FirstOrDefault();var source=client.State.Commands.Last();await todo.PrepareAsync(new(state=="unarchive"?WorkspaceOperation.UnarchiveTask:WorkspaceOperation.CorrectOccurrence,task.Id,task.Text,task.Notes??"",task.Priority,task.Deadline,null,false,HabitScoreDirection.Up,1,null,Unarchive:state=="unarchive"?new(task.Revision):null,CorrectOccurrence:state=="correction"?new(occurrence!.Id,occurrence.Revision,source.Command.Id):null));original=todo.Commands.Preview!.PreparedCommandId;}
        var markup=route switch{
            "tasks"=>Render<Habitica.WebApp.Pages.TasksPage>().Markup,"dailies"=>Render<Habitica.WebApp.Pages.TodoDailiesPage>().Markup,"habits"=>Render<Habitica.WebApp.Pages.TodoHabitsPage>().Markup,"sync"=>Render<Habitica.WebApp.Pages.TodoSyncPage>().Markup,"reminders"=>Render<Habitica.WebApp.Pages.TodoRemindersPage>().Markup,"inbox"=>Render<Habitica.WebApp.Pages.TodoInboxPage>().Markup,"plan"=>Render<Habitica.WebApp.Pages.TodoPlanPage>().Markup,"today"=>Render<Habitica.WebApp.Pages.TodoTodayPage>().Markup,"projects"=>Render<Habitica.WebApp.Pages.TodoProjectsPage>().Markup,"task-tree"=>Render<Habitica.WebApp.Pages.TodoTasksPage>().Markup,_=>Render<Habitica.WebApp.Pages.TodoSavedListsPage>().Markup};
        Assert.False(todo.CanSwitchSource);if(state=="linked-unknown"){Assert.True(todo.HasLinkedRecoveryIntent);Assert.Equal(original,todo.LinkedRecovery!.CommandId);Assert.Contains("original linked recovery owns editing",markup);Assert.Empty(client.PreparationIds);if(route=="tasks")Assert.True(new AngleSharp.Html.Parser.HtmlParser().ParseDocument(markup).QuerySelector("[data-testid=todo-kind]")!.HasAttribute("disabled"));}else{Assert.Equal(original,todo.Commands.Preview!.PreparedCommandId);Assert.Contains(state=="unarchive"?"Return exact task from archive":"Correct native Daily occurrence",markup);Assert.Empty(client.AcceptedIds);}
        var path=route switch{"tasks"=>"/tasks","task-tree"=>"/todo/tasks",_=>"/todo/"+route};Qualification.PageLayoutEvidence.Save("recovery-shared-"+route+"-"+state,markup,path,"shared-"+state+"-identity-and-disabled-state",["actions","confirmation","disabled-controls","focus"],nameof(Shared_task_review_and_pending_recovery_preserve_identity_on_every_host_route));
    }
    private sealed class ReadOnlyPageStorage:Habitica.Storage.IKeyValueStorage
    {
        public Task<T?> GetAsync<T>(string key,CancellationToken token)=>Task.FromResult(default(T));
        public Task SetAsync<T>(string key,T value,CancellationToken token)=>Task.CompletedTask;
        public Task<string?> GetRawJsonAsync(string key,CancellationToken token)=>Task.FromResult<string?>(null);
        public Task SetRawJsonAsync(string key,string json,CancellationToken token)=>Task.CompletedTask;
        public Task RemoveAsync(string key,CancellationToken token)=>Task.CompletedTask;
    }
    internal sealed class RecoveryCloud:WorkspaceTaskFixture.Cloud,IWorkspaceLinkedRecoveryApi
    {
        public WorkspaceJournalSnapshot Before{get;}public WorkspaceJournalSnapshot After{get;}private WorkspaceLinkedRecoveryView? _view;private bool _confirmed;
        public bool RefuseEligibility{get;set;}public bool ExpiredView{get;set;}public bool LosePreview{get;set;}public bool LoseDownAcknowledgement{get;set;}public int DownCalls{get;private set;}public List<Guid> Requests{get;}=[];
        public RecoveryCloud(){After=WorkspaceJournalJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","linked-recovery-todo-journal-v1.json")));var receipt=After.Document.LinkedTaskRecoveries![0];var task=After.Commands.Single(row=>row.Command.Id==receipt.SourceContext.CommandId).TaskSnapshot!;Before=After with {Document=After.Document with {Revision=receipt.ReviewedRevision,Tasks=[task],LinkedTaskRecoveries=null,Restorations=null}};State=Before;}
        public Task<WorkspaceTaskResponse<WorkspaceLinkedRecoveryEligibility>> ReadLinkedRecoveryEligibilityAsync(Guid taskId,Guid completionId,CancellationToken token)
        {if(RefuseEligibility)return Task.FromResult(new WorkspaceTaskResponse<WorkspaceLinkedRecoveryEligibility>(true,new(State.Document.Id,State.Document.Revision,taskId,completionId,false,"original_score_proof_unavailable",null,"No proof"),"Unavailable"));var row=After.Document.LinkedTaskRecoveries![0];var task=Before.Document.Tasks[0];var completion=Before.Document.Completions[0];var plan=new WorkspaceLinkedRecoveryPlan(State.Document.Id,row.ReviewedRevision,task,Before.Document.Projections[0],completion,row.SourceContext,row.SourceAttemptId,row.AccountId,row.ContextHash);return Task.FromResult(new WorkspaceTaskResponse<WorkspaceLinkedRecoveryEligibility>(true,new(State.Document.Id,State.Document.Revision,taskId,completionId,_view is null,_view is null?null:"original_recovery_pending",_view is null?plan:null,"Synthetic recovery disclosure",_view),"Read"));}
        public Task<WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>> PrepareLinkedRecoveryAsync(WorkspaceLinkedRecoveryRequest request,CancellationToken token)
        {Requests.Add(request.RequestId);var row=After.Document.LinkedTaskRecoveries![0];var review=new WorkspaceRecoveryReview("own-task-recovery-v1",row.ContextHash,Guid.Parse(Before.Document.Projections[0].HabiticaTaskId!),CompanionTaskKind.Todo,row.MappingFingerprint,null,new(2026,10,27),row.UpstreamReviewHash,new("unknown","unknown","unknown","unknown","unknown"));_view??=new(row.Id,request.RequestId,Before.Document.Id,row.ReviewedRevision,Before.Document.Tasks[0],Before.Document.Projections[0],Before.Document.Completions[0],row.SourceContext,row.SourceAttemptId,row.AccountId,row.ContextHash,review,"ready",DateTimeOffset.UtcNow.AddMinutes(ExpiredView?-1:1),ExpiredView,false,null,null,null,"Uncheck exact synthetic own task; all resource changes unknown.");if(LosePreview){LosePreview=false;return Task.FromResult(new WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>(false,null,"Synthetic lost preparation",true));}return Task.FromResult(new WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>(true,_view,"Review"));}
        public Task<WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>> AcceptLinkedRecoveryAsync(WorkspaceLinkedRecoveryView review,CancellationToken token)
        {Assert.Equal(_view!.CommandId,review.CommandId);DownCalls++;_confirmed=true;State=After;if(LoseDownAcknowledgement){LoseDownAcknowledgement=false;return Task.FromResult(new WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>(false,null,"Synthetic acknowledgement loss",true));}return ReadLinkedRecoveryAsync(review.CommandId,token);}
        public Task<WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>> ReadLinkedRecoveryAsync(Guid commandId,CancellationToken token)
        {Assert.Equal(_view!.CommandId,commandId);return Task.FromResult(new WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>(true,_confirmed?_view with {State="confirmed",SavedInCompanion=true,Receipt=After.Document.LinkedTaskRecoveries![0]}:_view,"Original"));}
    }
}
