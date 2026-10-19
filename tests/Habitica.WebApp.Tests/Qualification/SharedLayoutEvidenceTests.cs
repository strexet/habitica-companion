using Bunit;
using Habitica.WebApp.Components.Navigation;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Qualification;

public sealed class SharedLayoutEvidenceTests : BunitContext
{
    protected override void Dispose(bool disposing)
    {
        if (disposing) { DisposeAsync().AsTask().GetAwaiter().GetResult(); return; }
        base.Dispose(disposing);
    }

    [Fact]
    public void Authenticated_navigation_retains_real_routes_and_drawer_surface()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty with { IsAuthenticated = true }));
        var cut = Render<MudDrawer>(p => p.Add(x => x.Open, true).Add(x => x.Variant, DrawerVariant.Persistent)
            .Add(x => x.Class, "app-drawer").AddChildContent<AppNavMenu>());
        Assert.Contains(cut.FindAll("a"), a => a.GetAttribute("href") == "/tasks");
        PageLayoutEvidence.Save("shared-navigation", cut.Markup, "shared-navigation", "authenticated", ["navigation"], nameof(Authenticated_navigation_retains_real_routes_and_drawer_surface));
    }
}
