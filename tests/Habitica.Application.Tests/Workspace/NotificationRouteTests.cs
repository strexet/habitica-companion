using Habitica.Application.Notifications;
namespace Habitica.Application.Tests.Workspace;
public sealed class NotificationRouteTests
{
    [Fact] public void Only_exact_opaque_same_origin_notification_target_survives_sign_in()
    {
        var target=new NotificationTarget(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid());var route=NotificationRoute.Build(target);
        Assert.True(NotificationRoute.TryParse(route,out var parsed));Assert.Equal(target,parsed);
        Assert.Equal(route,NotificationRoute.SignInDestination("https://companion.invalid/sign-in?returnTo="+Uri.EscapeDataString(route)));
        foreach(var invalid in new[]{"https://evil.invalid"+route,"//evil.invalid"+route,route+"&taskId="+target.TaskId,route+"#private",route+"&extra=1",route.Replace(target.TaskId.ToString(),Guid.Empty.ToString()),"/todo/tasks?taskId="+target.TaskId})
        {Assert.False(NotificationRoute.TryParse(invalid,out _));Assert.Equal("/dashboard",NotificationRoute.SignInDestination("https://companion.invalid/sign-in?returnTo="+Uri.EscapeDataString(invalid)));}
        Assert.Equal("/dashboard",NotificationRoute.SignInDestination("https://companion.invalid/sign-in?returnTo="+Uri.EscapeDataString(route)+"&returnTo="+Uri.EscapeDataString(route)));
    }
}
