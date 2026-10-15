using Bunit;
using Habitica.Application.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class PartyPlanningSettingsPanelTests : BunitContext
{
    private static readonly Guid Party = Guid.NewGuid(), Namespace = Guid.NewGuid();
    private static PartyPlanningView View(bool consented = false, string role = "leader") => new(new(Party, "Synthetic <script>party</script>", 2), role, DateTimeOffset.UtcNow, "habitica-verified-party", consented ? Namespace : null, consented ? 5 : null, consented ? true : null, consented ? [] : null);
    private WorkspaceTaskFixture Setup()
    {
        var f = new WorkspaceTaskFixture(); f.Client.PartyPlanningAvailable = true; f.Client.Connected = true;
        Services.AddMudServices(); Services.AddSingleton<IJSRuntime>(f.Js); Services.AddSingleton<IAppSessionController>(f.Session);
        Services.AddSingleton<IWorkspaceCloudClient>(f.Client); Services.AddSingleton(f.Database);
        Services.AddSingleton(new WorkspaceDataController(f.Session, f.Client, f.Database));
        f.Client.PartyAction = r => Task.FromResult(r.Action switch
        {
            PartyPlanningAction.Context => new PartyPlanningResult(true, "Context", View()),
            PartyPlanningAction.State => new PartyPlanningResult(true, "State", View(true)),
            _ => new PartyPlanningResult(true, "Confirmed")
        });
        return f;
    }
    [Fact]
    public void ConsentBindsTheDisplayedPartyAndRemovalRequiresASeparateConfirmation()
    {
        var f = Setup(); var cut = Render<PartyPlanningSettingsPanel>();
        cut.Find("[data-testid='party-context']").Click();
        Assert.Empty(cut.FindAll("script")); Assert.Contains("<script>party</script>", cut.Find("[data-testid='party-identity']").TextContent);
        Assert.True(cut.Find("[data-testid='party-enable']").HasAttribute("disabled"));
        cut.Find("[data-testid='party-consent']").Change(true); cut.Find("[data-testid='party-enable']").Click();
        Assert.Equal(Party, f.Client.PartyRequests.Single(r => r.Action == PartyPlanningAction.Consent).ExpectedPartyId);
        cut.Find("[data-testid='party-state']").Click(); WorkspaceLayoutEvidence.Save("authenticated-party-controls", cut.Markup);
        cut.Find("[data-testid='party-delete']").Click(); Assert.DoesNotContain(f.Client.PartyRequests, r => r.Action == PartyPlanningAction.DeleteNamespace);
        cut.Find("[data-testid='party-confirm']").Click();
        var deletion = f.Client.PartyRequests.Single(r => r.Action == PartyPlanningAction.DeleteNamespace); Assert.Equal(Namespace, deletion.NamespaceId); Assert.Equal(5, deletion.Revision);
        cut.Find("[data-testid='party-remove-own']").Click(); Assert.DoesNotContain(f.Client.PartyRequests, r => r.Action == PartyPlanningAction.RemoveOwn);
        cut.Find("[data-testid='party-confirm']").Click(); Assert.Single(f.Client.PartyRequests, r => r.Action == PartyPlanningAction.RemoveOwn);
    }
    [Fact]
    public void MemberControlsDoNotExposeLeaderDeletionAndSignoutClearsPartyFacts()
    {
        var f = Setup(); f.Client.PartyAction = _ => Task.FromResult(new PartyPlanningResult(true, "State", View(true, "member")));
        var cut = Render<PartyPlanningSettingsPanel>(); cut.Find("[data-testid='party-state']").Click();
        Assert.Empty(cut.FindAll("[data-testid='party-delete']")); Assert.Empty(cut.FindAll("[data-testid='party-save-settings']"));
        f.Session.SetState(f.Session.State with { IsAuthenticated = false }); cut.WaitForAssertion(() => Assert.DoesNotContain("Synthetic &lt;script&gt;party", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='party-identity']"));
    }
    [Fact]
    public async Task LateAccountResponseCannotRestorePriorPartyData()
    {
        var f = Setup(); var response = new TaskCompletionSource<PartyPlanningResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Client.PartyAction = _ => response.Task;
        var cut = Render<PartyPlanningSettingsPanel>(); var click = cut.Find("[data-testid='party-context']").ClickAsync(new());
        f.Session.SetState(f.Session.State with { UserId = "00000000-0000-4000-8000-000000000099" });
        response.SetResult(new(true, "Private old response", View())); await click;
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='party-identity']")));
        Assert.DoesNotContain("Private old response", cut.Markup);
    }
}
