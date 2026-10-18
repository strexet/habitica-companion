using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Auth;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed class WorkspaceTaskClientTests
{
    private const string Session = "sssssssssssssssssssssssssssssssssssssssssss";
    private static readonly HabiticaCredentials Credentials = new("00000000-0000-4000-8000-000000000011", "synthetic-token");
    private static HttpResponseMessage Json(object value, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = new StringContent(WorkspaceTaskApiJson.Serialize(value), System.Text.Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request); }
    private static HttpClient VaultHttp() => new(new Handler(_ => Task.FromResult(Json(new { profileId = "prf_" + new string('p', 43), managementToken = new string('m', 43) }, HttpStatusCode.Created))));
    private static HttpResponseMessage Connection(Guid id) => Json(new { workspaceId = id, sessionToken = Session, expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1), consentVersion = "workspace-v1" }, HttpStatusCode.Created);
    private static WorkspaceCommand Intent(Guid id) => new(Guid.NewGuid(), id, 1, WorkspaceOperation.CreateTodo, Guid.NewGuid(), new(Text: "Reviewed native task", HabiticaDetails: new(0.1, HabiticaTaskAttribute.Per, [])), ProjectToHabitica: true);
    private static WorkspaceMutationReceipt Receipt(WorkspaceCommand command) => new("createTodo", command.TaskId, command.Id, Guid.NewGuid(), "Reviewed native task", true, "pending", 0, 0, 1, null, "none", null, true, false);
    [Fact]
    public async Task ReviewedServerIdentityIsReusedAfterUnknownAcceptanceAndReceiptLookup()
    {
        var workspaceId = Guid.NewGuid(); var requestId = Guid.NewGuid(); var intent = Intent(workspaceId);
        var command = intent with { Id = Guid.NewGuid(), TaskId = Guid.NewGuid() };
        var preview = new WorkspaceCommandPreview(command.Id, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(10), false, command, true, false);
        var calls = 0;
        using var vaultHttp = VaultHttp();
        using var http = new HttpClient(new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Connection(workspaceId);
            Assert.Equal(Session, request.Headers.Authorization!.Parameter);
            var text = await request.Content!.ReadAsStringAsync(); Assert.DoesNotContain(Credentials.ApiToken, text);
            using var body = JsonDocument.Parse(text); var args = body.RootElement.GetProperty("arguments"); calls++;
            if (calls == 1)
            {
                Assert.Equal("prepare_task_command", body.RootElement.GetProperty("tool").GetString()); Assert.Equal(requestId, args.GetProperty("requestId").GetGuid());
                Assert.Equal("createTodo", args.GetProperty("operation").GetString()); Assert.False(args.TryGetProperty("taskId", out _));
                Assert.Equal(0.1, args.GetProperty("patch").GetProperty("habiticaDetails").GetProperty("difficulty").GetDouble());
                return Json(preview);
            }
            Assert.Equal(command.Id, args.GetProperty("commandId").GetGuid());
            if (calls == 2) { Assert.Equal("create_todo", body.RootElement.GetProperty("tool").GetString()); throw new HttpRequestException("Synthetic secret-bearing failure must be redacted"); }
            Assert.Equal("get_task_command", body.RootElement.GetProperty("tool").GetString()); return Json(Receipt(command));
        }));
        var client = new WorkspaceCloudClient(http, new ChatGptIntegrationClient(vaultHttp, "https://auth.invalid/", false), "https://workspace.invalid/");
        Assert.True((await client.ConnectAsync(Credentials, default)).Succeeded);
        var prepared = await client.PrepareCommandAsync(requestId, intent, default); Assert.True(prepared.Succeeded); Assert.Equal(command.Id, prepared.Value!.PreparedCommandId);
        var uncertain = await client.AcceptCommandAsync(prepared.Value, default); Assert.False(uncertain.Succeeded); Assert.True(uncertain.OutcomeUnknown); Assert.DoesNotContain("secret-bearing", uncertain.Message);
        var recovered = await client.ReadCommandAsync(command.Id, default); Assert.True(recovered.Succeeded); Assert.True(recovered.Value!.SavedInCompanion); Assert.Equal(0, recovered.Value.ConfirmedProjectionCount); Assert.Equal(3, calls);
    }
    [Fact]
    public async Task InvalidReceiptCountsCannotBecomeConfirmedClientState()
    {
        var workspaceId = Guid.NewGuid(); var command = Intent(workspaceId);
        using var vaultHttp = VaultHttp(); using var http = new HttpClient(new Handler(request => Task.FromResult(request.RequestUri!.AbsolutePath == "/connect" ? Connection(workspaceId) : Json(Receipt(command) with { HabiticaProjectionStatus = "confirmed", ConfirmedProjectionCount = 2, TotalProjectionCount = 1 }))));
        var client = new WorkspaceCloudClient(http, new ChatGptIntegrationClient(vaultHttp, "https://auth.invalid/", false), "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        var result = await client.ReadCommandAsync(command.Id, default); Assert.False(result.Succeeded); Assert.True(result.OutcomeUnknown); Assert.Null(result.Value);
    }
    [Fact]
    public async Task ReviewedLinkCarriesExactWorkspaceEpochAndFingerprintWithoutUpstreamMutation()
    {
        var id = Guid.NewGuid(); var epoch = Guid.NewGuid(); var taskId = Guid.NewGuid(); var externalId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var task = new CompanionTask(taskId, 1, "Canonical title", CompanionTaskKind.Todo, CompanionTaskState.Active, CompanionPriority.Normal, now, now);
        var external = new WorkspaceExternalTask(externalId, "todo", "External title", "External notes", null, 1, "str", null, [], false, false, false, null, new string('a', 64));
        var preview = new WorkspaceProjectionPreview(id, epoch, task, external, 1, 0); var requests = 0;
        using var vaultHttp = VaultHttp(); using var http = new HttpClient(new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Connection(id);
            requests++; Assert.Equal(Session, request.Headers.Authorization!.Parameter);
            if (request.RequestUri.AbsolutePath == "/projection/preview") return Json(preview);
            Assert.Equal("/projection/adopt", request.RequestUri.AbsolutePath); using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(id, body.RootElement.GetProperty("expectedWorkspaceId").GetGuid()); Assert.Equal(epoch, body.RootElement.GetProperty("expectedAccessEpoch").GetGuid());
            Assert.Equal(external.Fingerprint, body.RootElement.GetProperty("expectedFingerprint").GetString());
            return Json(new { adopted = true, taskId, habiticaTaskId = externalId, revision = 2 });
        }));
        var client = new WorkspaceCloudClient(http, new ChatGptIntegrationClient(vaultHttp, "https://auth.invalid/", false), "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        var reviewed = await client.PreviewLinkAsync(taskId, externalId, default); Assert.True(reviewed.Succeeded); Assert.Equal("Canonical title", reviewed.Value!.Task.Text); Assert.Equal("External title", reviewed.Value.External.Text);
        Assert.True((await client.AdoptLinkAsync(reviewed.Value, default)).Succeeded); Assert.Equal(2, requests);
        Assert.False((await client.AdoptLinkAsync(reviewed.Value with { PendingCommandCount = 1 }, default)).Succeeded); Assert.Equal(2, requests);
    }
    [Theory]
    [InlineData(WorkspaceOperation.SkipOccurrence, "skip_occurrence", "skipOccurrence", 1)]
    [InlineData(WorkspaceOperation.PauseTask, "pause_task", "pauseTask", 0)]
    [InlineData(WorkspaceOperation.ResumeTask, "resume_task", "resumeTask", 0)]
    [InlineData(WorkspaceOperation.PlanTask, "plan_task", "planTask", 0)]
    [InlineData(WorkspaceOperation.RestoreTodo, "restore_todo", "restoreTodo", 1)]
    [InlineData(WorkspaceOperation.RelateTodo, "relate_todo", "relateTodo", 0)]
    [InlineData(WorkspaceOperation.SetTodoState, "set_todo_state", "setTodoState", 0)]
    public async Task LifecycleAdaptersUseExactToolIdentityAndRejectGameEffectReceipts(WorkspaceOperation operation, string tool, string name, int count)
    {
        var id = Guid.NewGuid(); var command = new WorkspaceCommand(Guid.NewGuid(), id, 1, operation, Guid.NewGuid(), OccurrenceDate: operation == WorkspaceOperation.SkipOccurrence ? new DateOnly(2026, 10, 17) : null);
        var preview = new WorkspaceCommandPreview(command.Id, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(10), false, command, false, false);
        var receipt = new WorkspaceMutationReceipt(name, command.TaskId, command.Id, preview.CorrelationId, "Companion-only work", true, "notRequested", count, 0, 0, null, "none", null, true, false);
        var invalid = false;
        using var vaultHttp = VaultHttp(); using var http = new HttpClient(new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Connection(id);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(tool, body.RootElement.GetProperty("tool").GetString()); Assert.Equal(command.Id, body.RootElement.GetProperty("arguments").GetProperty("commandId").GetGuid());
            return Json(invalid ? receipt with { HabiticaProjectionStatus = "pending", TotalProjectionCount = 1 } : receipt);
        }));
        var client = new WorkspaceCloudClient(http, new ChatGptIntegrationClient(vaultHttp, "https://auth.invalid/", false), "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        var accepted = await client.AcceptCommandAsync(preview, default); Assert.True(accepted.Succeeded); Assert.Equal(count, accepted.Value!.AcceptedEventCount);
        invalid = true; var refused = await client.AcceptCommandAsync(preview, default); Assert.False(refused.Succeeded); Assert.True(refused.OutcomeUnknown); Assert.Null(refused.Value);
    }
    [Theory]
    [InlineData("none")] [InlineData("effect")] [InlineData("target")] [InlineData("field")] [InlineData("gameField")]
    public async Task NativePlanningPreviewMustMatchTheRequestedTargetAndFields(string change)
    {
        var id=Guid.NewGuid();var intent=new WorkspaceCommand(Guid.NewGuid(),id,1,WorkspaceOperation.PlanTask,Guid.NewGuid(),new(Planning:new(PlannedDate:new(2026,10,20))));
        var command=intent with { Id=Guid.NewGuid() };
        if(change=="target")command=command with { TaskId=Guid.NewGuid() };
        if(change=="field")command=command with { Patch=new(Planning:new(PlannedDate:new(2026,10,21))) };
        if(change=="gameField")command=command with { Patch=command.Patch! with { Text="Unexpected game field" } };
        var preview=new WorkspaceCommandPreview(command.Id,Guid.NewGuid(),DateTimeOffset.UtcNow.AddMinutes(10),false,command,change=="effect",false);
        using var vaultHttp=VaultHttp();using var http=new HttpClient(new Handler(async request=>
        {
            if(request.RequestUri!.AbsolutePath=="/connect")return Connection(id);
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());Assert.Equal("prepare_task_command",body.RootElement.GetProperty("tool").GetString());
            var args=body.RootElement.GetProperty("arguments");Assert.Equal(intent.TaskId,args.GetProperty("taskId").GetGuid());Assert.Equal("2026-10-20",args.GetProperty("patch").GetProperty("planning").GetProperty("plannedDate").GetString());
            return Json(preview);
        }));
        var client=new WorkspaceCloudClient(http,new ChatGptIntegrationClient(vaultHttp,"https://auth.invalid/",false),"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        var result=await client.PrepareCommandAsync(Guid.NewGuid(),intent,default);Assert.Equal(change=="none",result.Succeeded);
        if(change!="none"){Assert.True(result.OutcomeUnknown);Assert.Null(result.Value);}
    }
    [Theory]
    [InlineData("none")] [InlineData("effect")] [InlineData("completion")] [InlineData("target")] [InlineData("mixed")]
    public async Task RestorePreviewRequiresExactCompletionAndNoEffects(string change)
    {
        var id=Guid.NewGuid();var intent=new WorkspaceCommand(Guid.NewGuid(),id,3,WorkspaceOperation.RestoreTodo,Guid.NewGuid(),new(RestoreTodo:new(Guid.NewGuid())));
        var command=intent with { Id=Guid.NewGuid() };
        if(change=="target")command=command with { TaskId=Guid.NewGuid() };
        if(change=="completion")command=command with { Patch=new(RestoreTodo:new(Guid.NewGuid())) };
        if(change=="mixed")command=command with { Patch=command.Patch! with { Text="Unexpected" } };
        var preview=new WorkspaceCommandPreview(command.Id,Guid.NewGuid(),DateTimeOffset.UtcNow.AddMinutes(10),false,command,change=="effect",false);
        using var vaultHttp=VaultHttp();using var http=new HttpClient(new Handler(async request=>
        {
            if(request.RequestUri!.AbsolutePath=="/connect")return Connection(id);
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());var args=body.RootElement.GetProperty("arguments");
            Assert.Equal(intent.TaskId,args.GetProperty("taskId").GetGuid());Assert.Equal(intent.Patch!.RestoreTodo!.CompletionId,args.GetProperty("patch").GetProperty("restoreTodo").GetProperty("completionId").GetGuid());return Json(preview);
        }));
        var client=new WorkspaceCloudClient(http,new ChatGptIntegrationClient(vaultHttp,"https://auth.invalid/",false),"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        var result=await client.PrepareCommandAsync(Guid.NewGuid(),intent,default);Assert.Equal(change=="none",result.Succeeded);if(change!="none"){Assert.True(result.OutcomeUnknown);Assert.Null(result.Value);}
    }

    [Theory]
    [InlineData("none")] [InlineData("effect")] [InlineData("target")] [InlineData("parent")] [InlineData("dependencies")] [InlineData("mixed")]
    public async Task RelationshipPreviewBindsExactNativeReferences(string change)
    {
        var id=Guid.NewGuid();var intent=new WorkspaceCommand(Guid.NewGuid(),id,10,WorkspaceOperation.RelateTodo,Guid.NewGuid(),new(Relationships:new(ParentTaskId:Guid.NewGuid(),DependsOn:[Guid.NewGuid()])));
        var command=intent with { Id=Guid.NewGuid() };
        if(change=="target")command=command with { TaskId=Guid.NewGuid() };
        if(change=="parent")command=command with { Patch=new(Relationships:intent.Patch!.Relationships! with { ParentTaskId=Guid.NewGuid() }) };
        if(change=="dependencies")command=command with { Patch=new(Relationships:intent.Patch!.Relationships! with { DependsOn=[] }) };
        if(change=="mixed")command=command with { Patch=command.Patch! with { Text="Unexpected" } };
        var preview=new WorkspaceCommandPreview(command.Id,Guid.NewGuid(),DateTimeOffset.UtcNow.AddMinutes(10),false,command,change=="effect",false);
        using var vaultHttp=VaultHttp();using var http=new HttpClient(new Handler(async request=>
        {
            if(request.RequestUri!.AbsolutePath=="/connect")return Connection(id);
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());var args=body.RootElement.GetProperty("arguments");Assert.Equal(intent.TaskId,args.GetProperty("taskId").GetGuid());Assert.Equal(intent.Patch!.Relationships!.ParentTaskId,args.GetProperty("patch").GetProperty("relationships").GetProperty("parentTaskId").GetGuid());return Json(preview);
        }));
        var client=new WorkspaceCloudClient(http,new ChatGptIntegrationClient(vaultHttp,"https://auth.invalid/",false),"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);var result=await client.PrepareCommandAsync(Guid.NewGuid(),intent,default);Assert.Equal(change=="none",result.Succeeded);if(change!="none"){Assert.True(result.OutcomeUnknown);Assert.Null(result.Value);}
    }

    [Fact]
    public async Task NativeTodoStatePreviewBindsExactRequestedStateAndTarget()
    {
        var id=Guid.NewGuid();var intent=new WorkspaceCommand(Guid.NewGuid(),id,1,WorkspaceOperation.SetTodoState,Guid.NewGuid(),new(TodoState:new(CompanionTaskState.Waiting)));var wrong=false;
        using var vault=VaultHttp();using var http=new HttpClient(new Handler(async request=>{
            if(request.RequestUri!.AbsolutePath=="/connect")return Connection(id);
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());Assert.Equal("waiting",body.RootElement.GetProperty("arguments").GetProperty("patch").GetProperty("todoState").GetProperty("state").GetString());
            var command=intent with {Id=Guid.NewGuid(),Patch=wrong?new(TodoState:new(CompanionTaskState.Active)):intent.Patch};return Json(new WorkspaceCommandPreview(command.Id,Guid.NewGuid(),DateTimeOffset.UtcNow.AddMinutes(10),false,command,false,false));
        }));var client=new WorkspaceCloudClient(http,new ChatGptIntegrationClient(vault,"https://auth.invalid/",false),"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        Assert.True((await client.PrepareCommandAsync(Guid.NewGuid(),intent,default)).Succeeded);wrong=true;var result=await client.PrepareCommandAsync(Guid.NewGuid(),intent,default);Assert.False(result.Succeeded);Assert.True(result.OutcomeUnknown);
    }

}
