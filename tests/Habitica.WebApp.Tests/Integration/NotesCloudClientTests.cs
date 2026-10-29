using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Habitica.Application.Notes;
using Habitica.Domain.Auth;
using Habitica.Domain.Notes;
using Habitica.WebApp.Integration;
namespace Habitica.WebApp.Tests.Integration;

public sealed class NotesCloudClientTests
{
    private const string Profile="prf_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly string Scope=NotesModel.Scope(Profile);
    private static readonly Guid Notebook=Guid.Parse("00000000-0000-4000-8000-000000000099");
    private static NoteCommand Command()=>new(Guid.NewGuid(),Guid.NewGuid(),0,0,NoteOperation.Create,"Размер одежды","# 🧥\r\nРазмер L\n```\nmeasurements\n```");
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=JsonContent.Create(value)};
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> action):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>action(request,token);}
    private static HttpResponseMessage Connection()=>Json(new{notebookId=Notebook,ownerScope=Scope,accessEpoch=Guid.NewGuid(),consentVersion="notes-v1",sessionToken=new string('n',43),expiresAtUtc=DateTimeOffset.UtcNow.AddHours(8)});
    private static HttpResponseMessage Enrolled()=>Json(new{profileId=Profile,managementToken=new string('m',43)});
    [Fact]
    public async Task Separate_consent_connects_without_uploading_local_notes_and_export_remains_account_bound()
    {
        var paths=new List<string>();var book=NotesEngine.Apply(NotesModel.Empty(Scope),Command(),DateTimeOffset.UtcNow).Notebook;
        using var http=new HttpClient(new Handler(async(request,token)=>{
            var path=request.RequestUri!.AbsolutePath;paths.Add(path);
            if(path=="/enroll")return Enrolled();
            if(path=="/notes/connect"){Assert.Equal(new string('m',43),request.Headers.Authorization?.Parameter);using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));Assert.Equal("notes-v1",body.RootElement.GetProperty("consentVersion").GetString());return Connection();}
            Assert.Equal("/notes/export",path);Assert.Equal(new string('n',43),request.Headers.Authorization?.Parameter);return new(HttpStatusCode.OK){Content=new StringContent(NotesJson.Serialize(book))};
        }));
        var vault=new ChatGptIntegrationClient(http,"https://auth.invalid");var client=new NotesCloudClient(http,vault,"https://workspace.invalid");
        Assert.True((await client.ConnectAsync(new("fixture-owner","fixture-secret"),default)).Succeeded);Assert.Equal(new[]{"/enroll","/notes/connect"},paths);
        var result=await client.LoadAsync(default);Assert.True(result.Succeeded);Assert.Equal(NotesJson.Serialize(book),NotesJson.Serialize(result.Value!));
        vault.ForgetSession();Assert.False(client.IsConnected);Assert.False((await client.LoadAsync(default)).Succeeded);Assert.Equal(3,paths.Count);
    }
    [Fact]
    public async Task Lost_acceptance_response_recovers_exact_original_receipt_without_replaying_write()
    {
        var command=Command();var saved=NotesEngine.Apply(NotesModel.Empty(Scope),command,DateTimeOffset.UtcNow);var writes=0;
        using var http=new HttpClient(new Handler((request,_)=>{
            var path=request.RequestUri!.AbsolutePath;
            if(path=="/enroll")return Task.FromResult(Enrolled());if(path=="/notes/connect")return Task.FromResult(Connection());
            if(path=="/notes/command"){writes++;throw new HttpRequestException("Synthetic response loss after commit.");}
            Assert.Equal("/notes/receipt",path);return Task.FromResult(Json(new{notebookId=Notebook,ownerScope=Scope,receipt=saved.Receipt,saved=true,revision=1}));
        }));
        var client=new NotesCloudClient(http,new(http,"https://auth.invalid"),"https://workspace.invalid");Assert.True((await client.ConnectAsync(new("fixture","fixture"),default)).Succeeded);
        var unknown=await client.SubmitAsync(command,default);Assert.False(unknown.Succeeded);Assert.True(unknown.OutcomeUnknown);
        var recovered=await client.RecoverAsync(command,default);Assert.True(recovered.Succeeded);Assert.Equal(saved.Receipt,recovered.Value);Assert.Equal(1,writes);
        Assert.False((await client.RecoverAsync(command with{Markdown="changed"},default)).Succeeded);
    }
    [Fact]
    public async Task Late_connect_and_read_responses_cannot_restore_a_forgotten_session()
    {
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http=new HttpClient(new Handler(async(request,_)=>{
            if(request.RequestUri!.AbsolutePath=="/enroll")return Enrolled();entered.TrySetResult();await release.Task;return Connection();
        }));
        var vault=new ChatGptIntegrationClient(http,"https://auth.invalid");var client=new NotesCloudClient(http,vault,"https://workspace.invalid");
        var pending=client.ConnectAsync(new("fixture","fixture"),default);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));vault.ForgetSession();client.ForgetSession();release.SetResult();
        Assert.False((await pending).Succeeded);Assert.False(client.IsConnected);Assert.Null(client.NotebookId);
    }
    [Fact]
    public async Task Foreign_export_and_altered_receipt_are_rejected_without_exposing_unvalidated_data()
    {
        var command=Command();var receipt=NotesEngine.Apply(NotesModel.Empty(Scope),command,DateTimeOffset.UtcNow).Receipt!;
        using var http=new HttpClient(new Handler((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath switch{
            "/enroll"=>Enrolled(),"/notes/connect"=>Connection(),
            "/notes/export"=>new(HttpStatusCode.OK){Content=new StringContent(NotesJson.Serialize(NotesModel.Empty(NotesModel.Scope("other-profile"))))},
            _=>Json(new{notebookId=Notebook,ownerScope=Scope,receipt=receipt with{PayloadHash=new string('0',64)},saved=true,disposition="accepted"})
        })));
        var client=new NotesCloudClient(http,new(http,"https://auth.invalid"),"https://workspace.invalid");await client.ConnectAsync(new("fixture","fixture"),default);
        Assert.False((await client.LoadAsync(default)).Succeeded);var result=await client.SubmitAsync(command,default);Assert.False(result.Succeeded);Assert.Null(result.Value);Assert.True(result.OutcomeUnknown);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("http://remote.invalid")]
    [InlineData("https://user:secret@workspace.invalid")]
    [InlineData("https://workspace.invalid?token=secret")]
    public async Task Unsafe_configuration_does_not_enroll_or_send_credentials(string? url)
    {
        var calls=0;using var http=new HttpClient(new Handler((_,_)=>{calls++;throw new InvalidOperationException();}));
        var client=new NotesCloudClient(http,new(http,"https://auth.invalid"),url);Assert.False((await client.ConnectAsync(new("fixture","fixture"),default)).Succeeded);Assert.Equal(0,calls);
    }
    [Fact]
    public async Task Exact_note_reads_validate_content_hash_and_search_cannot_claim_full_content()
    {
        var note=NotesEngine.Apply(NotesModel.Empty(Scope),Command(),DateTimeOffset.UtcNow).Notebook.Notes[0];var bad=false;
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(note.Current.Markdown))).ToLowerInvariant();
        using var http=new HttpClient(new Handler((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath switch{
            "/enroll"=>Enrolled(),"/notes/connect"=>Connection(),
            "/notes/get"=>Json(new CloudNote(Notebook,Scope,1,note,bad?new string('0',64):hash,16,false)),
            _=>Json(new NotesSearchPage(Notebook,Scope,1,[new(note.Id,note.Current.Title,1,false,note.Current.AtUtc,"snippet",true)],1,null,bad))
        })));
        var client=new NotesCloudClient(http,new(http,"https://auth.invalid"),"https://workspace.invalid");await client.ConnectAsync(new("fixture","fixture"),default);
        var read=await client.ReadAsync(note.Id,default);Assert.True(read.Succeeded);Assert.Equal(note.Current.Markdown,read.Value!.Note.Current.Markdown);
        Assert.True((await client.SearchAsync("",false,0,null,default)).Succeeded);bad=true;
        Assert.False((await client.ReadAsync(note.Id,default)).Succeeded);Assert.False((await client.SearchAsync("",false,0,null,default)).Succeeded);
    }
    [Fact]
    public async Task Project_filters_and_association_receipts_preserve_exact_metadata_and_refuse_foreign_or_unassigned_mismatches()
    {
        var workspace=Guid.NewGuid();var link=new NoteProjectLink(Guid.NewGuid(),workspace,"cloud",Scope);var original=Command();var note=NotesEngine.Apply(NotesModel.Empty(Scope),original,DateTimeOffset.UtcNow).Notebook.Notes[0];var mode=0;
        var command=new NoteCommand(Guid.NewGuid(),note.Id,1,1,NoteOperation.Update,Project:link);var receipt=new NoteReceipt(command.Id,NotesJson.Hash(command),note.Id,2,2);
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(note.Current.Markdown))).ToLowerInvariant();
        using var http=new HttpClient(new Handler(async(request,token)=>{
            var path=request.RequestUri!.AbsolutePath;if(path=="/enroll")return Enrolled();if(path=="/notes/connect")return Connection();
            if(path=="/notes/command"){using var input=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));var c=input.RootElement.GetProperty("command");Assert.Equal(workspace,c.GetProperty("project").GetProperty("workspaceId").GetGuid());Assert.Equal(JsonValueKind.Null,c.GetProperty("markdown").ValueKind);return Json(new{notebookId=Notebook,ownerScope=Scope,receipt,saved=true,disposition="accepted"});}
            var current=mode==1?link with {OwnerScope=NotesModel.Scope("foreign")}:link;
            if(path=="/notes/get")return Json(new CloudNote(Notebook,Scope,1,note with {Current=note.Current with {Project=current}},hash,16,false));
            using var query=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));if(query.RootElement.TryGetProperty("projectId",out var project)){Assert.Equal(link.ProjectId,project.GetGuid());Assert.Equal(workspace,query.RootElement.GetProperty("projectWorkspaceId").GetGuid());}
            return Json(new NotesSearchPage(Notebook,Scope,1,[new(note.Id,note.Current.Title,1,false,note.Current.AtUtc,"snippet",true,current)],1,null,false));
        }));
        var client=new NotesCloudClient(http,new(http,"https://auth.invalid"),"https://workspace.invalid");Assert.True((await client.ConnectAsync(new("fixture","fixture"),default)).Succeeded);var filter=new NotesProjectFilter(link.ProjectId,workspace,"cloud");
        Assert.Equal(link,(await client.ReadAsync(note.Id,default)).Value!.Note.Current.Project);Assert.Equal(link,(await client.SearchAsync("",false,0,null,filter,default)).Value!.Results[0].Project);Assert.True((await client.SubmitAsync(command,default)).Succeeded);
        Assert.False((await client.SearchAsync("",false,0,null,new(Unassigned:true),default)).Succeeded);mode=1;Assert.False((await client.ReadAsync(note.Id,default)).Succeeded);var refused=await client.SearchAsync("",false,0,null,filter,default);Assert.False(refused.Succeeded);Assert.Null(refused.Value);
    }

}
