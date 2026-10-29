using Habitica.Application.Notifications;
using Habitica.Domain.Workspace;
using Habitica.WebApp.State;
namespace Habitica.WebApp.Tests.Workspace;
internal sealed class PushReminderFixture
{
    internal const string Profile="prf_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal static readonly Guid Connection=Guid.Parse("40000000-0000-4000-8000-000000000050");
    public WorkspaceTaskFixture Task {get;}=new();
    public Client Api {get;}=new();
    public Browser BrowserApi {get;}=new();
    public Store Local {get;}=new();
    public TodoWorkspacePresentation Todo {get;}
    public PushDeviceController Devices {get;}
    public TaskReminderController Reminders {get;}
    public PushReminderFixture(){Task.Session.HasChatGptConnection=true;Todo=new(Task.Session,Task.Client,Task.Database,Task.Controller);Devices=new(Task.Session,Api,BrowserApi,Local);Reminders=new(Task.Session,Todo,Api,Local);}
    public async Task InitializeAsync()
    {
        Task.Client.Connected=true;var now=DateTimeOffset.UtcNow;var task=new CompanionTask(Guid.NewGuid(),1,"Synthetic push task",CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.Normal,now,now,ReviewAfterUtc:new DateTimeOffset(now.Year,now.Month,now.Day,now.Hour,0,0,TimeSpan.Zero).AddHours(2));
        Task.Client.State=Task.Client.State with{StoreVersion=2,Document=Task.Client.State.Document with{Revision=2,Tasks=[task]}};
        await Todo.RefreshSourcesAsync();await Todo.SelectAsync(new(TodoWorkspaceSource.Cloud,Task.Client.State.Document.Id));
        Api.Read=new(Task.Client.State.Document.Id,2,task.Id,true,[],[],0,null,new(Profile,Connection,true,true,"web-push-v1",Api.Push.ConsentEpoch,true,Api.Push.ApplicationServerKey,Api.Push.Devices),null,null);
        await Devices.RefreshAsync();await Reminders.LoadAsync(task.Id);
    }
    public ReminderIntent Intent=>new("set",null,0,Api.Read!.TaskId,Task.Client.State.Document.Tasks[0].ReviewAfterUtc,"UTC",null,[new(Api.Device.DeviceId,Api.Device.Revision,Api.Device.SubscriptionId)]);
    internal sealed class Client:IPushSubscriptionClient,ITaskReminderClient
    {
        public long ContextVersion {get;set;}
        public PushDevice Device {get;}=new(Guid.NewGuid(),1,Guid.NewGuid(),"active",null);
        public PushStatus Push {get;set;}
        public TaskReminderState? Read;
        public int StatusReads,Preparations,Acceptances,Recoveries;
        public List<PushSubscribeCommand> Subscriptions=[];
        public List<PushConsentCommand> Consents=[];
        public List<PushUnsubscribeCommand> Unsubscribes=[];
        public List<ReminderPreparation> Inputs=[];
        public PushReceipt? PushReceipt;
        public ReminderReceipt? Receipt;
        public ReminderPreview? Preview;
        public bool LosePush, LoseAccept, LosePrepare;
        public TaskCompletionSource? Entered,Gate;
        public Client(){Push=new(Profile,Connection,true,"web-push-v1",Guid.NewGuid(),true,new string('k',87),[Device]);}
        private async Task Wait(){Entered?.TrySetResult();if(Gate is not null)await Gate.Task;}
        public async Task<PushResult<PushStatus>> ReadPushStatusAsync(CancellationToken t){StatusReads++;await Wait();return new(true,"checked",Push);}
        public async Task<PushResult<PushReceipt>> SetPushConsentAsync(PushConsentCommand c,CancellationToken t){Consents.Add(c);await Wait();PushReceipt=new(Profile,Connection,c.RequestId,"consent",true,Guid.NewGuid(),c.Enabled,null,null,null,null);return new(!LosePush,"synthetic",LosePush?null:PushReceipt);}
        public async Task<PushResult<PushReceipt>> SubscribePushAsync(PushSubscribeCommand c,CancellationToken t){Subscriptions.Add(c);await Wait();PushReceipt=new(Profile,Connection,c.RequestId,"subscribe",true,null,null,c.DeviceId,Guid.NewGuid(),c.ExpectedRevision+1,"active");return new(!LosePush,"synthetic",LosePush?null:PushReceipt);}
        public async Task<PushResult<PushReceipt>> UnsubscribePushAsync(PushUnsubscribeCommand c,CancellationToken t){Unsubscribes.Add(c);await Wait();PushReceipt=new(Profile,Connection,c.RequestId,"unsubscribe",true,null,null,c.DeviceId,c.SubscriptionId,c.ExpectedRevision+1,"unsubscribed");return new(!LosePush,"synthetic",LosePush?null:PushReceipt);}
        public Task<PushResult<PushReceipt>> RecoverPushReceiptAsync(Guid id,CancellationToken t){Recoveries++;return System.Threading.Tasks.Task.FromResult(new PushResult<PushReceipt>(true,"original",PushReceipt));}
        public async Task<PushResult<TaskReminderState>> ReadTaskRemindersAsync(Guid id,int offset,CancellationToken t){await Wait();return new(true,"checked",Read! with{Offset=offset});}
        public async Task<PushResult<ReminderPreview>> PrepareReminderAsync(ReminderPreparation p,CancellationToken t)
        {
            Preparations++;Inputs.Add(p);await Wait();Preview??=new(Guid.NewGuid(),p.RequestId,false,ReminderIdentity.Hash(p),new string('t',43),DateTimeOffset.UtcNow.AddMinutes(5),Read!.WorkspaceId,p.ExpectedWorkspaceRevision,p.Intent,p.Intent.ScheduleId??Guid.NewGuid(),"Generic push only.");return new(!LosePrepare,"synthetic",LosePrepare?null:Preview);
        }
        public async Task<PushResult<ReminderCommandStatus>> AcceptReminderAsync(ReminderPreview p,CancellationToken t)
        {
            Acceptances++;await Wait();Receipt=new(p.CommandId,p.RequestId,p.WorkspaceId!.Value,p.ScheduleId!.Value,p.Intent!.ExpectedRevision+1,p.Intent.TaskId,p.Intent.Operation,p.Intent.Operation=="set"?"active":"cancelled",true,p.Intent.Operation=="set",false,false,null,null,p.Intent);
            return new(!LoseAccept,"synthetic",LoseAccept?null:new(p.CommandId,true,Receipt:Receipt));
        }
        public Task<PushResult<ReminderCommandStatus>> RecoverReminderAsync(Guid id,CancellationToken t){Recoveries++;return System.Threading.Tasks.Task.FromResult(new PushResult<ReminderCommandStatus>(true,"original",new(id,Receipt is not null,Receipt:Receipt)));}
    }
    internal sealed class Browser:IPushBrowser
    {
        public BrowserPushCapability Capability=new("available","default");
        public BrowserPushCurrent Current=new("available","default",false,null);
        public BrowserPushResult Result=new("subscribed",new string('f',43),new("https://web.push.apple.com/synthetic-private",new(new string('p',87),new string('a',22)),null));
        public int Permissions,Removals;
        public TaskCompletionSource<BrowserPushResult>? SubscribeGate;
        public Task<BrowserPushCapability> PrepareAsync(CancellationToken t)=>System.Threading.Tasks.Task.FromResult(Capability);
        public Task<BrowserPushCurrent> ReadCurrentAsync(CancellationToken t)=>System.Threading.Tasks.Task.FromResult(Current);
        public Task<BrowserPushResult> SubscribeAsync(string key,bool consent,CancellationToken t){Permissions++;return SubscribeGate?.Task??System.Threading.Tasks.Task.FromResult(Result);}
        public Task<BrowserPushResult> UnsubscribeAsync(string fp,CancellationToken t){Removals++;Current=Current with{Exists=false,Fingerprint=null};return System.Threading.Tasks.Task.FromResult(new BrowserPushResult("unsubscribed"));}
    }
    internal sealed class Store:INotificationLocalStore
    {
        public PushBrowserLocalState Device=new(0,null,null);
        public ReminderLocalState Reminder=new(0,null);
        public bool FailWrites;
        public int Clears;
        public Task<PushBrowserLocalState> ReadDeviceAsync(string a,PushContext c,CancellationToken t=default)=>System.Threading.Tasks.Task.FromResult(Device);
        public Task<bool> WriteDeviceAsync(string a,PushContext c,long r,PushBrowserLocalState v,CancellationToken t=default){var ok=!FailWrites&&r==Device.Revision;if(ok)Device=v;return System.Threading.Tasks.Task.FromResult(ok);}
        public Task<ReminderLocalState> ReadReminderAsync(string a,PushContext c,Guid w,CancellationToken t=default)=>System.Threading.Tasks.Task.FromResult(Reminder);
        public Task<bool> WriteReminderAsync(string a,PushContext c,Guid w,long r,ReminderLocalState v,CancellationToken t=default){var ok=!FailWrites&&r==Reminder.Revision;if(ok)Reminder=v;return System.Threading.Tasks.Task.FromResult(ok);}
        public Task ClearAsync(CancellationToken t=default){Clears++;Device=new(0,null,null);Reminder=new(0,null);return System.Threading.Tasks.Task.CompletedTask;}
    }
}
