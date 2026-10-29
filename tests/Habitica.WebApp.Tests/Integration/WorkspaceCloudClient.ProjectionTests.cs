using System.Net;
using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed partial class WorkspaceCloudClientTests
{
    private WorkspaceProjectionPreview LinkReview(CompanionTaskKind kind = CompanionTaskKind.Todo, bool completed = false, bool archived = false)
    {
        var now = DateTimeOffset.UtcNow; var taskId = Guid.NewGuid(); var nativeId = Guid.NewGuid(); var commandId = Guid.NewGuid();
        return new(_id, Guid.NewGuid(), new(taskId, 2, "Canonical retained", kind, completed ? CompanionTaskState.Completed : CompanionTaskState.Active, CompanionPriority.High, now, now, IsArchived: archived),
            new(nativeId, kind.ToString().ToLowerInvariant(), "Native retained", "Independent notes", null, 1, "str", null, [], completed, true, true, null, new string('a', 64)), 3, 0,
            commandId, Guid.NewGuid(), new string('b', 64), now.AddMinutes(10), Guid.NewGuid(), MappingAfter: new(Guid.NewGuid(), 4, taskId, ProjectionState.Linked, nativeId.ToString("D"), Fingerprint: new string('a', 64)), CommandId: commandId, State: "prepared");
    }
    private static object LinkCommand(WorkspaceProjectionPreview p, string state, long? revision = null) => new
    {
        commandId = p.PreparedCommandId, preparedCommandId = p.PreparedCommandId, requestId = p.RequestId, workspaceId = p.WorkspaceId, reviewHash = p.ReviewHash,
        state, savedInCompanion = state == "accepted", outcomeUnknown = false, gameEffects = 0, nativeWrites = 0,
        receipt = state != "accepted" ? null : new { commandId = p.PreparedCommandId, requestId = p.RequestId, workspaceId = p.WorkspaceId, accessEpoch = p.AccessEpoch, connectionId = p.ConnectionId,
            taskId = p.Task.Id, habiticaTaskId = p.External.Id, mappingId = p.MappingAfter!.Id, reviewHash = p.ReviewHash, revision = revision ?? p.ExpectedRevision + 1, adopted = true, acceptedAtUtc = DateTimeOffset.UtcNow, gameEffects = 0, nativeWrites = 0 }
    };

    [Theory]
    [InlineData(CompanionTaskKind.Todo, false, false)]
    [InlineData(CompanionTaskKind.Todo, true, true)]
    [InlineData(CompanionTaskKind.Daily, false, true)]
    [InlineData(CompanionTaskKind.Habit, false, false)]
    public async Task Exact_adoption_reviews_server_command_and_recovers_original_receipt_after_later_revisions(CompanionTaskKind kind, bool completed, bool archived)
    {
        var preview = LinkReview(kind, completed, archived); var accepted = false; var lose = true; var accepts = 0;
        var vault = Vault(out var vaultHttp); using var ownedVault = vaultHttp;
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Connection();
            Assert.Equal("/adoption-api", request.RequestUri.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)); var tool = body.RootElement.GetProperty("tool").GetString(); var args = body.RootElement.GetProperty("arguments");
            if (tool == "prepare_task_adoption")
            {
                preview = preview with { RequestId = args.GetProperty("requestId").GetGuid() };
                Assert.Equal(preview.Task.Id, args.GetProperty("taskId").GetGuid()); Assert.Equal(preview.External.Id, args.GetProperty("habiticaTaskId").GetGuid());
                return new(HttpStatusCode.OK) { Content = new StringContent(WorkspaceTaskApiJson.Serialize(preview)) };
            }
            Assert.Equal(preview.PreparedCommandId, args.GetProperty("commandId").GetGuid());
            if (tool == "accept_task_adoption") { accepts++; Assert.Equal(preview.ReviewHash, args.GetProperty("reviewHash").GetString()); accepted = true; if (lose) { lose = false; throw new HttpRequestException("Synthetic lost acknowledgement"); } }
            return Json(LinkCommand(preview, accepted ? "accepted" : "prepared"));
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        var reviewed = await client.PreviewLinkAsync(preview.Task.Id, preview.External.Id, default); Assert.True(reviewed.Succeeded, reviewed.Message);
        Assert.True((await client.AdoptLinkAsync(reviewed.Value!, default)).OutcomeUnknown);
        Assert.True((await client.AdoptLinkAsync(reviewed.Value!, default)).Succeeded); Assert.Equal(1, accepts);
        Assert.False((await client.AdoptLinkAsync(reviewed.Value! with { PendingCommandCount = 1 }, default)).Succeeded); Assert.Equal(1, accepts);
    }
    [Theory]
    [InlineData("kind")]
    [InlineData("completion")]
    [InlineData("command")]
    [InlineData("mapping")]
    [InlineData("effects")]
    [InlineData("mapping-id")]
    [InlineData("mapping-fingerprint")]
    [InlineData("mapping-state")]
    public async Task Adoption_preview_refuses_incompatible_or_malformed_review(string mode)
    {
        var p = LinkReview(); var vault = Vault(out var vaultHttp); using var ownedVault = vaultHttp;
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Connection();
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)); p = p with { RequestId = body.RootElement.GetProperty("arguments").GetProperty("requestId").GetGuid() };
            p = mode switch { "kind" => p with { External = p.External with { Kind = "habit" } }, "completion" => p with { External = p.External with { Completed = true } }, "command" => p with { CommandId = Guid.NewGuid() }, "mapping" => p with { MappingAfter = p.MappingAfter! with { HabiticaTaskId = Guid.NewGuid().ToString("D") } }, "mapping-id" => p with { MappingAfter = p.MappingAfter! with { Id = Guid.Empty } }, "mapping-fingerprint" => p with { MappingAfter = p.MappingAfter! with { Fingerprint = new string('c',64) } }, "mapping-state" => p with { MappingAfter = p.MappingAfter! with { State = ProjectionState.UnknownOutcome } }, _ => p with { GameEffects = 1 } };
            return new(HttpStatusCode.OK) { Content = new StringContent(WorkspaceTaskApiJson.Serialize(p)) };
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        Assert.False((await client.PreviewLinkAsync(p.Task.Id, p.External.Id, default)).Succeeded);
    }
    [Theory]
    [InlineData(503)]
    [InlineData(408)]
    [InlineData(409)]
    [InlineData(200)]
    public async Task A_transport_refusal_or_malformed_lookup_cannot_discard_unknown_original_acceptance(int status)
    {
        var p = LinkReview(); var calls = 0; var vault = Vault(out var vaultHttp); using var ownedVault = vaultHttp;
        using var http = new HttpClient(new Handler((request, _) => { if (request.RequestUri!.AbsolutePath == "/connect") return Task.FromResult(Connection()); calls++; return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("{}") }); }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        var result = await client.AdoptLinkAsync(p, default); Assert.False(result.Succeeded); Assert.True(result.OutcomeUnknown); Assert.False(result.KnownNotAccepted); Assert.Equal(1, calls);
    }
    [Fact]
    public async Task Original_terminal_nonacceptance_requires_fresh_review_without_sending_accept_again()
    {
        var p = LinkReview(); var calls = 0; var vault = Vault(out var vaultHttp); using var ownedVault = vaultHttp;
        using var http = new HttpClient(new Handler(async (request, token) => { if (request.RequestUri!.AbsolutePath == "/connect") return Connection(); calls++; using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)); Assert.Equal("get_task_adoption_command", body.RootElement.GetProperty("tool").GetString()); return Json(LinkCommand(p, "refused")); }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        var result = await client.AdoptLinkAsync(p, default); Assert.True(result.KnownNotAccepted); Assert.False(result.OutcomeUnknown); Assert.Equal(1, calls);
    }
    [Fact]
    public async Task Lost_preparation_response_reuses_exact_request_and_blocks_a_different_native_record()
    {
        var p = LinkReview(); var ids = new List<Guid>(); var vault = Vault(out var vaultHttp); using var ownedVault = vaultHttp;
        using var http = new HttpClient(new Handler(async (request, token) => { if (request.RequestUri!.AbsolutePath == "/connect") return Connection(); using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)); ids.Add(body.RootElement.GetProperty("arguments").GetProperty("requestId").GetGuid()); if (ids.Count == 1) throw new HttpRequestException("Synthetic lost preview"); p = p with { RequestId = ids.Last() }; return new(HttpStatusCode.OK) { Content = new StringContent(WorkspaceTaskApiJson.Serialize(p)) }; }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        Assert.False((await client.PreviewLinkAsync(p.Task.Id, p.External.Id, default)).Succeeded);
        Assert.False((await client.PreviewLinkAsync(p.Task.Id, Guid.NewGuid(), default)).Succeeded); Assert.Single(ids);
        Assert.True((await client.PreviewLinkAsync(p.Task.Id, p.External.Id, default)).Succeeded); Assert.Equal(ids[0], ids[1]);
    }
    [Theory]
    [InlineData("accessEpoch")]
    [InlineData("connectionId")]
    [InlineData("taskId")]
    [InlineData("habiticaTaskId")]
    [InlineData("mappingId")]
    [InlineData("commandId")]
    [InlineData("requestId")]
    [InlineData("revision")]
    [InlineData("reviewHash")]
    [InlineData("gameEffects")]
    [InlineData("nativeWrites")]
    public async Task An_unrelated_original_receipt_never_confirms_the_reviewed_pair(string field)
    {
        var p=LinkReview();var vault=Vault(out var vaultHttp);using var ownedVault=vaultHttp;
        using var http=new HttpClient(new Handler((request,_)=>{
            if(request.RequestUri!.AbsolutePath=="/connect")return Task.FromResult(Connection());
            var node=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(LinkCommand(p,"accepted")))!;if(field=="revision")node["receipt"]![field]=p.ExpectedRevision+2;else if(field is "gameEffects" or "nativeWrites")node["receipt"]![field]=1;else node["receipt"]![field]=field=="reviewHash"?new string('c',64):Guid.NewGuid().ToString("D");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(node.ToJsonString())});
        }));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        var result=await client.AdoptLinkAsync(p,default);Assert.False(result.Succeeded);Assert.True(result.OutcomeUnknown);
    }

    [Theory]
    [InlineData(true,2)]
    [InlineData(true,0)]
    [InlineData(false,-1)]
    public async Task Durable_Daily_adoption_keeps_complete_schedule_metadata_and_rejects_malformed_values(bool valid,int interval)
    {
        var p=LinkReview(CompanionTaskKind.Daily);p=p with {External=p.External with {Schedule=new("weekly",interval,DateTimeOffset.UtcNow,new Dictionary<string,bool>{{"su",false},{"m",true},{"t",false},{"w",false},{"th",false},{"f",false},{"s",false}},[],[])}};
        var vault=Vault(out var vaultHttp);using var ownedVault=vaultHttp;
        using var http=new HttpClient(new Handler(async(request,token)=>{
            if(request.RequestUri!.AbsolutePath=="/connect")return Connection();using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));p=p with {RequestId=body.RootElement.GetProperty("arguments").GetProperty("requestId").GetGuid()};
            return new(HttpStatusCode.OK){Content=new StringContent(WorkspaceTaskApiJson.Serialize(p))};
        }));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);var result=await client.PreviewLinkAsync(p.Task.Id,p.External.Id,default);Assert.Equal(valid,result.Succeeded);if(valid)Assert.Equal(interval,result.Value!.External.Schedule!.EveryX);
    }

}
