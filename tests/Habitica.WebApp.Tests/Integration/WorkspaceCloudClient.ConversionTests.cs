using System.Net;
using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;
using Habitica.WebApp.State;
using Habitica.WebApp.Tests.Workspace;

namespace Habitica.WebApp.Tests.Integration;

public sealed partial class WorkspaceCloudClientTests
{
    private static HttpResponseMessage ConversionJson<T>(T data)=>new(HttpStatusCode.OK){Content=new StringContent(WorkspaceTaskApiJson.Serialize(data))};
    [Fact]
    public async Task Conversion_wire_omits_new_target_identity_and_recovers_same_original_after_lost_ack()
    {
        var fake=new ChecklistConversionFixture("create");var f=new WorkspaceTaskFixture();using var controller=new WorkspaceConversionController(f.Session,fake);await controller.LoadAsync(fake.Source.Id);
        foreach(var row in controller.Drafts){var item=fake.Template.Items.Single(item=>item.SourceItemId==row.SourceItemId);row.Mode=item.Mode;row.DesiredState=item.DesiredState;row.Notes=item.Notes??"";}controller.Difficulty=.1;controller.Attribute=HabiticaTaskAttribute.Int;await controller.PrepareAsync();var original=fake.Requests.Single();var preview=fake.Preview!;
        var vault=Vault(out var vaultHttp);using var owned=vaultHttp;var accepts=0;
        var logs=new Habitica.Storage.DiagnosticsLogStore(new InMemoryKeyValueStorage());var diagnostics=new Habitica.Application.Diagnostics.DiagnosticsLogWriter(logs,TimeProvider.System);
        using var http=new HttpClient(new Handler(async(request,token)=>
        {
            if(request.RequestUri!.AbsolutePath=="/connect")return Json(new {workspaceId=fake.State.Document.Id,sessionToken=Session,expiresAtUtc=DateTimeOffset.UtcNow.AddHours(1),consentVersion="workspace-v1"},HttpStatusCode.Created);
            Assert.Equal("/conversion-api",request.RequestUri.AbsolutePath);Assert.Equal(Session,request.Headers.Authorization!.Parameter);var text=await request.Content!.ReadAsStringAsync(token);Assert.DoesNotContain(Credentials.ApiToken,text);using var json=JsonDocument.Parse(text);var args=json.RootElement.GetProperty("arguments");
            switch(json.RootElement.GetProperty("tool").GetString())
            {
                case "prepare_checklist_conversion":
                    Assert.Equal(original.RequestId,args.GetProperty("requestId").GetGuid());Assert.False(args.TryGetProperty("expectedConversionId",out _));
                    foreach(var item in args.GetProperty("items").EnumerateArray()){Assert.False(item.TryGetProperty("targetTaskId",out _));if(item.GetProperty("mode").GetString()=="create"){Assert.Equal(.1,item.GetProperty("gameDetails").GetProperty("difficulty").GetDouble());Assert.Equal("int",item.GetProperty("gameDetails").GetProperty("attribute").GetString());}}
                    return ConversionJson(preview);
                case "accept_checklist_conversion":
                    Assert.Equal(preview.PreparedCommandId,args.GetProperty("commandId").GetGuid());await fake.AcceptConversionAsync(preview,default);if(++accepts==1)throw new HttpRequestException("Synthetic private token must not escape");return ConversionJson(fake.Receipt);
                case "get_checklist_conversion_command":return ConversionJson(fake.Receipt);
                default:return ConversionJson(fake.Status());
            }
        }));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/",diagnostics:diagnostics);await client.ConnectAsync(Credentials,default);
        var prepared=await client.PrepareConversionAsync(original,default);Assert.True(prepared.Succeeded,prepared.Message);Assert.Equal(8,prepared.Value!.NewCaptureCount);
        var lost=await client.AcceptConversionAsync(prepared.Value,default);Assert.False(lost.Succeeded);Assert.True(lost.OutcomeUnknown);Assert.DoesNotContain("private",lost.Message);
        var recovered=await client.ReadConversionCommandAsync(preview.PreparedCommandId,default);Assert.True(recovered.Succeeded,recovered.Message);Assert.Equal(preview.PreparedCommandId,recovered.Value!.CommandId);Assert.True((await client.AcceptConversionAsync(prepared.Value,default)).Succeeded);Assert.Equal(9,fake.State.Document.Tasks.Count);
        var status=await client.ReadConversionAsync(fake.Source.Id,default);Assert.True(status.Succeeded,status.Message);Assert.Equal(2,status.Value!.Targets.Count(row=>row.RequiresHistoricalMatch));Assert.False(status.Value.RetirementReady);
        var events=await logs.GetRecentAsync(default);Assert.Equal(5,events.Count);Assert.Contains(events,row=>row.Operation=="workspace-conversion-accept"&&row.Context!.Result==Habitica.Domain.Diagnostics.DiagnosticsResult.Unknown&&row.Context.CommandId==preview.PreparedCommandId);Assert.Contains(events,row=>row.Operation=="workspace-conversion-read"&&row.Mode==Habitica.Domain.Diagnostics.DiagnosticsMode.LiveRead);Assert.Contains(events,row=>row.Operation=="workspace-conversion-prepare"&&row.Context!.RequestedCount==8);
        var retained=JsonSerializer.Serialize(events);Assert.DoesNotContain(fake.Source.Notes!,retained);Assert.DoesNotContain("Own note",retained);Assert.DoesNotContain(Credentials.ApiToken,retained);Assert.DoesNotContain("private token",retained);
    }
    [Fact]
    public async Task Conversion_adapter_rejects_altered_source_binding_receipts_and_native_presence_claims()
    {
        var fake=new ChecklistConversionFixture();var f=new WorkspaceTaskFixture();using var controller=new WorkspaceConversionController(f.Session,fake);await controller.LoadAsync(fake.Source.Id);fake.ChooseReuse(controller);await controller.PrepareAsync();var original=fake.Requests.Single();var preview=fake.Preview!;object response=preview;
        var vault=Vault(out var vaultHttp);using var owned=vaultHttp;using var http=new HttpClient(new Handler((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath=="/connect"?Json(new {workspaceId=fake.State.Document.Id,sessionToken=Session,expiresAtUtc=DateTimeOffset.UtcNow.AddHours(1),consentVersion="workspace-v1"},HttpStatusCode.Created):ConversionJson(response))));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);Assert.True((await client.PrepareConversionAsync(original,default)).Succeeded);
        response=preview with {NewCaptureCount=1};Assert.False((await client.PrepareConversionAsync(original,default)).Succeeded);
        response=preview with {Source=preview.Source with {Notes="Altered source notes"}};Assert.False((await client.PrepareConversionAsync(original,default)).Succeeded);
        response=preview with {Targets=preview.Targets.Select((row,index)=>index==0?row with {Binding=row.Binding with {DesiredState=ChecklistConversionState.CompletedArchived,HistoricalTimeUncertain=true}}:row).ToArray()};Assert.False((await client.PrepareConversionAsync(original,default)).Succeeded);
        response=preview with {Command=preview.Command with {ProjectName="Different destination"}};Assert.False((await client.PrepareConversionAsync(original,default)).Succeeded);
        await fake.AcceptConversionAsync(preview,default);var status=fake.Status();response=status with {RetirementReady=true};Assert.False((await client.ReadConversionAsync(fake.Source.Id,default)).Succeeded);
        response=status with {Revision=status.Revision+1,Source=status.Source with {Revision=status.Revision+1,Notes="Current edited source",HabiticaDetails=status.Source.HabiticaDetails! with {Checklist=[]}},SourceUnchanged=false,CanonicalComplete=false};var edited=await client.ReadConversionAsync(fake.Source.Id,default);Assert.True(edited.Succeeded,edited.Message);Assert.Equal(10,edited.Value!.Targets.Count);Assert.Equal(status.Source.Notes,edited.Value.Conversion!.SourceSnapshot.Notes);
        response=status with {Targets=status.Targets.Select((row,index)=>index==0?row with {PlacementComplete=false}:row).ToArray()};Assert.False((await client.ReadConversionAsync(fake.Source.Id,default)).Succeeded);
        response=new WorkspaceConversionReceipt(preview.PreparedCommandId);var absent=await client.ReadConversionCommandAsync(preview.PreparedCommandId,default);Assert.False(absent.Succeeded);Assert.True(absent.OutcomeUnknown);
        response=fake.Receipt! with {Receipt=fake.Receipt!.Receipt! with {PayloadHash=new string('a',64)}};Assert.False((await client.ReadConversionCommandAsync(preview.PreparedCommandId,default)).Succeeded);
        // Later reviewed resumptions extend the same ledger. The original receipt remains valid.
        var resumed=WorkspaceChecklistConversionEngine.Accept(fake.State,preview.Command with {Id=Guid.NewGuid(),ExpectedRevision=fake.State.Document.Revision},DateTimeOffset.UtcNow);Assert.Equal(SubmissionDisposition.Accepted,resumed.Disposition);
        response=preview with {Conversion=resumed.Conversion!,Targets=resumed.Conversion!.Items.Select(binding=>new WorkspaceConversionPreviewTarget(binding,resumed.Snapshot!.Document.Tasks.SingleOrDefault(task=>task.Id==binding.TargetTaskId))).ToArray(),SavedInCompanion=true,Receipt=fake.Receipt,NewCaptureCount=0};var savedOriginal=await client.PrepareConversionAsync(original,default);Assert.True(savedOriginal.Succeeded,savedOriginal.Message);Assert.Equal(preview.PreparedCommandId,savedOriginal.Value!.Receipt!.CommandId);
    }
    [Fact]
    public async Task Conversion_late_response_after_signout_is_discarded()
    {
        var fake=new ChecklistConversionFixture();var vault=Vault(out var vaultHttp);using var owned=vaultHttp;var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http=new HttpClient(new Handler(async(request,_)=>{if(request.RequestUri!.AbsolutePath=="/connect")return Json(new {workspaceId=fake.State.Document.Id,sessionToken=Session,expiresAtUtc=DateTimeOffset.UtcNow.AddHours(1),consentVersion="workspace-v1"},HttpStatusCode.Created);entered.SetResult();await release.Task;return ConversionJson(fake.Status());}));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);var read=client.ReadConversionAsync(fake.Source.Id,default);await entered.Task;client.ForgetSession();release.SetResult();Assert.False((await read).Succeeded);Assert.Null(client.WorkspaceId);
    }
}
