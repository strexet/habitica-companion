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
    public void Renders_credential_help_and_safety_copy()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: false,
                DisplayName: null,
                ErrorMessage: null,
                LastSyncedAtUtc: null,
                TaskFreshness: SnapshotFreshnessState.Missing,
                TaskSnapshot: null)));

        var cut = Render<SignIn>();

        Assert.Contains("Where to find them", cut.Markup);
        Assert.Contains("Open Habitica API settings", cut.Markup);
        Assert.Contains("https://habitica.com/user/settings/api", cut.Markup);
        Assert.Contains("Website: User icon menu", cut.Markup);
        Assert.Contains("Android: Settings", cut.Markup);
        Assert.Contains("iOS: Settings", cut.Markup);
        Assert.Contains("Party quest queue and CRON timing", cut.Markup);
        Assert.Contains("Inventory gear comparison and presets", cut.Markup);
        Assert.Contains("Session-only sign-in is the default.", cut.Markup);
        Assert.Contains("Credentials are never saved in exports, diagnostics, logs, or Cloudflare sync.", cut.Markup);
        Assert.Contains("Treat this like a password", cut.Markup);
    }

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
