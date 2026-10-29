using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.State;

namespace Habitica.WebApp.Tests.Workspace;

internal sealed class TodoOrganizationFixture : IDisposable
{
    public WorkspaceTaskFixture Task { get; } = new();
    public NativeApi Api { get; }
    public WorkspaceOrganizationController Organization { get; }
    public TodoWorkspacePresentation Todo { get; }
    public TodoOrganizationFixture()
    {
        Api=new(Task);Organization=new(Task.Session,Task.Client,Api,Task.Database,TimeProvider.System);
        Todo=new(Task.Session,Task.Client,Task.Database,Task.Controller,Organization);
    }
    public async System.Threading.Tasks.Task Load(bool cloud=true,WorkspaceDocument? document=null)
    {
        document??=new(){Id=Guid.NewGuid()};Assert.Empty(WorkspaceRules.Validate(document));var snapshot=new WorkspaceJournalSnapshot(document,document.Revision,[],[]);Task.Client.State=snapshot;Task.Client.Connected=cloud;
        if(cloud)await Task.Controller.LoadAsync(true);else {Assert.True(await Task.Database.ForScope(WorkspaceTaskFixture.User).TryCommitAsync(0,snapshot,default));await Task.Controller.LoadAsync(false,document.Id);}
        await Todo.RefreshSourcesAsync();await Todo.LoadSelectedAsync();
    }
    public void Dispose(){Todo.Dispose();Organization.Dispose();}
    internal sealed class NativeApi(WorkspaceTaskFixture task):IWorkspaceOrganizationApi
    {
        private readonly Dictionary<Guid,WorkspaceOrganizationPreview> _previews=[];
        private readonly Dictionary<Guid,WorkspaceOrganizationReceipt> _receipts=[];
        public bool LosePreparation,LoseAcceptance;
        public List<Guid> Requests { get; }=[];
        public List<Guid> Acceptances { get; }=[];
        public List<Guid> Reads { get; }=[];
        public System.Threading.Tasks.Task<WorkspaceTaskResponse<WorkspaceOrganizationPreview>> PrepareOrganizationAsync(Guid requestId,WorkspaceOrganizationCommand intent,CancellationToken token)
        {
            Requests.Add(requestId);
            if(!_previews.TryGetValue(requestId,out var preview))
            {
                var command=intent with {Id=Guid.NewGuid(),EntityId=intent.Operation==WorkspaceOrganizationOperation.Create?Guid.NewGuid():intent.EntityId};
                var result=WorkspaceOrganizationEngine.Accept(task.Client.State with {Authority=null},command,DateTimeOffset.UtcNow);
                if(result.SavedInCompanion!=true)return System.Threading.Tasks.Task.FromResult(new WorkspaceTaskResponse<WorkspaceOrganizationPreview>(false,null,"Synthetic refusal"));
                preview=new(command.Id,requestId,DateTimeOffset.UtcNow.AddMinutes(10),false,command,result.Entry!.Snapshot,false,false);_previews.Add(requestId,preview);
            }
            if(LosePreparation){LosePreparation=false;return System.Threading.Tasks.Task.FromResult(new WorkspaceTaskResponse<WorkspaceOrganizationPreview>(false,null,"Synthetic lost preview",true));}
            if(_receipts.TryGetValue(preview.PreparedCommandId,out var receipt))preview=preview with {SavedInCompanion=true,Receipt=receipt};
            return System.Threading.Tasks.Task.FromResult(new WorkspaceTaskResponse<WorkspaceOrganizationPreview>(true,preview,"Synthetic preview"));
        }
        public System.Threading.Tasks.Task<WorkspaceTaskResponse<WorkspaceOrganizationReceipt>> AcceptOrganizationAsync(WorkspaceOrganizationPreview preview,CancellationToken token)
        {
            Acceptances.Add(preview.PreparedCommandId);var result=WorkspaceOrganizationEngine.Accept(task.Client.State with {Authority=null},preview.Command,DateTimeOffset.UtcNow);
            if(result.SavedInCompanion!=true)return System.Threading.Tasks.Task.FromResult(new WorkspaceTaskResponse<WorkspaceOrganizationReceipt>(false,null,"Synthetic refusal"));
            task.Client.State=result.Snapshot!;var receipt=WorkspaceOrganizationController.Receipt(result.Entry!,preview.CorrelationId);_receipts[receipt.CommandId]=receipt;
            if(LoseAcceptance){LoseAcceptance=false;return System.Threading.Tasks.Task.FromResult(new WorkspaceTaskResponse<WorkspaceOrganizationReceipt>(false,null,"Synthetic lost reply",true));}
            return System.Threading.Tasks.Task.FromResult(new WorkspaceTaskResponse<WorkspaceOrganizationReceipt>(true,receipt,"Synthetic native receipt"));
        }
        public System.Threading.Tasks.Task<WorkspaceTaskResponse<WorkspaceOrganizationReceipt>> ReadOrganizationAsync(Guid id,CancellationToken token)
        {
            Reads.Add(id);return System.Threading.Tasks.Task.FromResult(_receipts.TryGetValue(id,out var receipt)?new WorkspaceTaskResponse<WorkspaceOrganizationReceipt>(true,receipt,"Synthetic original receipt"):new(false,null,"Synthetic missing receipt"));
        }
    }
}
