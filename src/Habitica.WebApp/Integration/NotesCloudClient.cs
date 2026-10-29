using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Application.Notes;
using Habitica.Domain.Auth;
using Habitica.Domain.Notes;
namespace Habitica.WebApp.Integration;

public sealed class NotesCloudClient(HttpClient http,ChatGptIntegrationClient vault,string? baseUrl):INotesCloudClient
{
    private readonly Uri? _baseUri=BaseUri(baseUrl);
    private string? _token,_profileId;
    private int _generation;
    public Guid? NotebookId {get;private set;}
    public string? OwnerScope {get;private set;}
    public bool IsAvailable=>_baseUri is not null&&vault.IsVaultAvailable;
    public bool IsConnected=>_token is not null&&NotebookId is not null&&vault.HasConnection&&_profileId==vault.ProfileId;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    private static Uri? BaseUri(string? value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&(uri.Scheme=="https"||uri.Scheme=="http"&&uri.IsLoopback)&&uri.AbsolutePath=="/"&&string.IsNullOrEmpty(uri.UserInfo+uri.Query+uri.Fragment)?uri:null;
    public async Task<NotesCloudResult<bool>> ConnectAsync(HabiticaCredentials credentials,CancellationToken token)
    {
        if(!IsAvailable)return new(false,false,"notes_unavailable");
        var generation=_generation;
        try
        {
            // The caller must collect separate Notes cloud consent before enrollment/connection.
            var enrolled=await vault.EnrollProfileAsync(credentials,token);
            if(!enrolled.Succeeded||generation!=_generation)return new(false,false,"connection_changed");
            var profile=vault.ProfileId;var version=vault.SessionVersion;
            using var request=vault.NotesHandshake(new(_baseUri!,"notes/connect"));
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
            if(!response.IsSuccessStatusCode)return new(false,false,"notes_connection_unavailable");
            var value=JsonSerializer.Deserialize<Connection>(await Text(response,4096,token),Json);
            if(generation!=_generation||version!=vault.SessionVersion||profile is null||profile!=vault.ProfileId)return new(false,false,"connection_changed");
            if(value is null||value.NotebookId==Guid.Empty||value.AccessEpoch==Guid.Empty||value.ConsentVersion!="notes-v1"||value.OwnerScope!=NotesModel.Scope(profile)
                ||!System.Text.RegularExpressions.Regex.IsMatch(value.SessionToken??"","^[A-Za-z0-9_-]{43}$")||value.ExpiresAtUtc<=DateTimeOffset.UtcNow||value.ExpiresAtUtc>DateTimeOffset.UtcNow.AddHours(9))return new(false,false,"invalid_notes_connection");
            _generation++;_profileId=profile;_token=value.SessionToken;NotebookId=value.NotebookId;OwnerScope=value.OwnerScope;
            return new(true,true,"connected");
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
        catch{return new(false,false,"connection_unknown",true);}
    }
    public Task<NotesCloudResult<NotesNotebook>> LoadAsync(CancellationToken token)=>Call(HttpMethod.Get,"notes/export",null,NotesModel.NotebookBytes,NotesJson.Deserialize,token);
    public Task<NotesCloudResult<NoteReceipt>> SubmitAsync(NoteCommand command,CancellationToken token)=>Receipt("notes/command",new{expectedNotebookId=NotebookId,command},command,token);
    public Task<NotesCloudResult<NoteReceipt>> RecoverAsync(NoteCommand command,CancellationToken token)=>Receipt("notes/receipt",new{expectedNotebookId=NotebookId,commandId=command.Id},command,token);
    private async Task<NotesCloudResult<NoteReceipt>> Receipt(string path,object input,NoteCommand command,CancellationToken token)
    {
        var result=await Call(HttpMethod.Post,path,input,4096,text=>JsonSerializer.Deserialize<ReceiptPayload>(text,Json)??throw new JsonException(),token,path=="notes/command");
        if(!result.Succeeded||result.Value is not {} value)return new(false,null,result.Code,result.OutcomeUnknown);
        var receipt=value.Receipt;
        if(!value.Saved||value.NotebookId!=NotebookId||value.OwnerScope!=OwnerScope||receipt is null||receipt.CommandId!=command.Id||receipt.NoteId!=command.NoteId||receipt.PayloadHash!=NotesJson.Hash(command)
            ||receipt.NoteRevision!=command.ExpectedNoteRevision+1||receipt.NotebookRevision!=command.ExpectedNotebookRevision+1||receipt.NoteRevision<1||receipt.NotebookRevision<1)
            return new(false,null,"invalid_notes_receipt",true);
        return new(true,receipt,value.Disposition??"recovered");
    }
    public Task<NotesCloudResult<NotesSearchPage>> SearchAsync(string query,bool trash,int offset,long? expectedRevision,CancellationToken token)=>SearchAsync(query,trash,offset,expectedRevision,new NotesProjectFilter(),token);
    public async Task<NotesCloudResult<NotesSearchPage>> SearchAsync(string query,bool trash,int offset,long? expectedRevision,NotesProjectFilter filter,CancellationToken token)
    {
        if(!filter.Valid||filter.ProjectId is not null&&filter.Source!="cloud")return new(false,null,"invalid_project_filter");
        var input=new Dictionary<string,object?>{{"query",query},{"trash",trash},{"offset",offset},{"expectedRevision",expectedRevision}};
        if(filter.ProjectId is {} id){input["projectId"]=id;input["projectWorkspaceId"]=filter.WorkspaceId;}
        if(filter.Unassigned)input["unassigned"]=true;
        var result=await Call(HttpMethod.Post,"notes/search",input,64*1024,text=>JsonSerializer.Deserialize<NotesSearchPage>(text,Json)??throw new JsonException(),token);
        if(result.Succeeded&&result.Value is {} page&&(page.NotebookId!=NotebookId||page.OwnerScope!=OwnerScope||page.Revision<0||page.Revision>NotesModel.MaxRevision||page.FullContentIncluded||page.Results is null||page.Results.Count>20||page.Total<page.Results.Count||page.Total>NotesModel.MaxNotes
            ||page.NextOffset is {} next&&(next!=offset+page.Results.Count||next>=page.Total)||page.Results.Any(item=>item.Id==Guid.Empty||item.IsTrashed!=trash||item.NoteRevision<1||item.NoteRevision>NotesModel.MaxRevision||!NotesModel.TextValid(item.Title,item.Snippet)||!NotesModel.ValidProject(item.Project,page.OwnerScope)||!filter.Matches(item.Project))||page.Results.Select(item=>item.Id).Distinct().Count()!=page.Results.Count
            ||offset>0&&page.Revision!=expectedRevision))return new(false,null,"invalid_notes_search");
        return result;
    }
    public async Task<NotesCloudResult<CloudNote>> ReadAsync(Guid noteId,CancellationToken token)
    {
        var result=await Call(HttpMethod.Post,"notes/get",new{noteId},NotesModel.NotebookBytes,text=>JsonSerializer.Deserialize<CloudNote>(text,NoteJson)??throw new JsonException(),token);
        if(result.Succeeded&&result.Value is {} value&&(value.NotebookId!=NotebookId||value.OwnerScope!=OwnerScope||value.Revision<1||value.Revision>NotesModel.MaxRevision||value.Note is null||value.Note.Id!=noteId||value.Note.Current is null
            ||!ValidNote(value.Note,value.OwnerScope)||value.Revision<value.Note.Current.Revision||value.HistoryLimit!=NotesModel.HistoryLimit||value.HistoryTruncated!=(value.Note.Current.Revision>NotesModel.HistoryLimit+1)
            ||value.ContentHash!=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Note.Current.Markdown))).ToLowerInvariant()))return new(false,null,"invalid_note");
        return result;
    }
    private static bool ValidNote(PersonalNote note,string ownerScope)
    {
        if(note.Current is null||note.Current.Revision<1||note.Current.Revision>NotesModel.MaxRevision||note.History is null||note.History.Count!=Math.Min(note.Current.Revision-1,NotesModel.HistoryLimit)||note.CreatedAtUtc==default||note.CreatedAtUtc.Offset!=TimeSpan.Zero)return false;
        var revision=note.Current.Revision-note.History.Count;var previous=note.CreatedAtUtc;
        foreach(var item in note.History.Append(note.Current))
        {
            if(item is null||item.Revision!=revision++||item.AtUtc==default||item.AtUtc.Offset!=TimeSpan.Zero||item.AtUtc<previous||!NotesModel.TextValid(item.Title,item.Markdown)||!NotesModel.ValidProject(item.Project,ownerScope))return false;
            previous=item.AtUtc;
        }
        return true;
    }
    public async Task<NotesCloudResult<bool>> DisconnectAsync(bool deleteNotebook,CancellationToken token)
    {
        var result=await Call(deleteNotebook?HttpMethod.Delete:HttpMethod.Post,deleteNotebook?"notes":"notes/disconnect",deleteNotebook?new{confirmNotebookId=NotebookId}:(object)new{},4096,text=>JsonSerializer.Deserialize<Cleanup>(text,Json)??throw new JsonException(),token,true);
        if(!result.Succeeded)return new(false,false,result.Code,result.OutcomeUnknown);
        if(result.Value is null||!(deleteNotebook?result.Value.Deleted:result.Value.Disconnected))return new(false,false,"cleanup_unknown",true);
        ForgetSession();return new(true,true,deleteNotebook?"deleted":"disconnected");
    }
    private async Task<NotesCloudResult<T>> Call<T>(HttpMethod method,string path,object? input,int limit,Func<string,T> parse,CancellationToken token,bool mutation=false)
    {
        if(!IsConnected)return new(false,default,"notes_connection_required");
        var generation=_generation;var version=vault.SessionVersion;
        try
        {
            using var request=new HttpRequestMessage(method,new Uri(_baseUri!,path));request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",_token);
            if(input is not null)request.Content=JsonContent.Create(input,options:NoteJson);
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
            if(generation!=_generation||version!=vault.SessionVersion||!IsConnected)return new(false,default,"connection_changed",mutation);
            if(!response.IsSuccessStatusCode)
            {
                if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)ForgetSession();
                var code=response.StatusCode==HttpStatusCode.Conflict?"revision_conflict":response.StatusCode==HttpStatusCode.RequestEntityTooLarge?"notes_capacity":"notes_request_failed";
                return new(false,default,code,mutation&&(int)response.StatusCode>=500);
            }
            var value=parse(await Text(response,limit,token));
            if(generation!=_generation||version!=vault.SessionVersion||!IsConnected)return new(false,default,"connection_changed",mutation);
            if(value is NotesNotebook book&&book.OwnerScope!=OwnerScope)return new(false,default,"account_mismatch");
            return new(true,value,"ok");
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){if(mutation)return new(false,default,"acceptance_unknown",true);throw;}
        catch{return new(false,default,mutation?"acceptance_unknown":"notes_unavailable",mutation);}
    }
    private static readonly JsonSerializerOptions NoteJson=new(JsonSerializerDefaults.Web){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false)}};
    private static async Task<string> Text(HttpResponseMessage response,int limit,CancellationToken token)
    {
        if(response.Content.Headers.ContentLength>limit)throw new JsonException();
        await using var stream=await response.Content.ReadAsStreamAsync(token);using var buffer=new MemoryStream();var chunk=new byte[8192];
        while(true){var count=await stream.ReadAsync(chunk,token);if(count==0)break;if(buffer.Length+count>limit)throw new JsonException();buffer.Write(chunk,0,count);}
        return new UTF8Encoding(false,true).GetString(buffer.GetBuffer(),0,checked((int)buffer.Length));
    }
    public void ForgetSession(){_generation++;_token=_profileId=OwnerScope=null;NotebookId=null;}
    private sealed record Connection(Guid NotebookId,string OwnerScope,Guid AccessEpoch,string ConsentVersion,string SessionToken,DateTimeOffset ExpiresAtUtc);
    private sealed record ReceiptPayload(Guid NotebookId,string OwnerScope,NoteReceipt Receipt,bool Saved,string? Disposition=null,long? Revision=null);
    private sealed record Cleanup(bool Disconnected=false,bool Retained=false,bool Deleted=false);
}
