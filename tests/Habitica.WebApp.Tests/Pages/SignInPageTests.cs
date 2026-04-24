using Bunit;
using Habitica.Domain.Sync;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class SignInPageTests : BunitContext
{
    [Fact]
    public void Submit_calls_session_controller_with_entered_credentials()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: false,
                DisplayName: null,
                ErrorMessage: null,
                LastSyncedAtUtc: null,
                TaskFreshness: SnapshotFreshnessState.Missing,
                TaskSnapshot: null));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<SignIn>();

        cut.Find("input[name='user-id']").Change("user-id");
        cut.Find("input[name='api-token']").Change("api-token");
        cut.Find("input[name='persist-locally']").Change(true);
        cut.Find("form").Submit();

        Assert.NotNull(controller.LastSignInRequest);
        Assert.Equal("user-id", controller.LastSignInRequest!.UserId);
        Assert.Equal("api-token", controller.LastSignInRequest.ApiToken);
        Assert.True(controller.LastSignInRequest.PersistLocally);
    }
}
