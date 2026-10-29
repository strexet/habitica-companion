using Habitica.Application.Notifications;
namespace Habitica.WebApp.Tests.Workspace;
internal sealed class ReminderBrowserFixture : IForegroundReminderBrowser
{
    public event Action<ForegroundReminderState>? Changed;
    public ForegroundReminderState State { get; set; }=new(0,null,false,false,"default","available","available","idle",DateTimeOffset.Parse("2026-10-26T09:00:00Z").ToUnixTimeMilliseconds());
    public List<(ForegroundReminderContext? Context,IReadOnlyList<ForegroundReminderSchedule> Cues,long Version)> Binds { get; }=[];
    public int Permissions, Clears;
    public TaskCompletionSource<ForegroundReminderState>? PermissionGate;
    public Task PrepareAsync(CancellationToken token=default)=>Task.CompletedTask;
    public Task<ForegroundReminderState> BindAsync(ForegroundReminderContext? context,IReadOnlyList<ForegroundReminderSchedule> cues,long version,CancellationToken token=default)
    {
        Binds.Add((context,cues,version));State=State with{BindingVersion=version,ContextKey=context is null?null:$"{context.AccountId}:{context.Source}:{context.WorkspaceId:D}",InApp=false,Browser=false};return Task.FromResult(State);
    }
    public Task<ForegroundReminderState> ConfigureAsync(string kind,bool enabled,long version,CancellationToken token=default)
    {
        State=kind=="inApp"?State with{InApp=enabled,Browser=enabled&&State.Browser}:State with{Browser=enabled};return Task.FromResult(State);
    }
    public Task<ForegroundReminderState> RequestPermissionAsync(long version,CancellationToken token=default){Permissions++;State=State with{Browser=true,Permission="granted"};return PermissionGate?.Task??Task.FromResult(State);}
    public Task ClearAsync(CancellationToken token=default){Clears++;State=State with{ContextKey=null,InApp=false,Browser=false};return Task.CompletedTask;}
    public void Emit(ForegroundReminderState state)=>Changed?.Invoke(state);
}
