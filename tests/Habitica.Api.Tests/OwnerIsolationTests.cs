using System.Net;
using System.Text;
using Habitica.Domain.Auth;
using Habitica.Domain.User;

namespace Habitica.Api.Tests;

public sealed class OwnerIsolationTests
{
    [Theory]
    [InlineData("groups/party/leave")]
    [InlineData("groups/00000000-0000-4000-8000-000000000001/leave")]
    [InlineData("groups/party/%6ceave")]
    [InlineData("groups/party/%256ceave")]
    [InlineData("/api/v3/GROUPS/party/LEAVE?confirm=true")]
    public void Party_departure_is_forbidden_without_a_profile_override(string path)
    {
        foreach (var isolated in new[] { false, true })
        {
            var error = Assert.Throws<HabiticaApiException>(() => HabiticaMutationGuard.Validate(HttpMethod.Post, path, isolated));
            Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        }
    }

    [Fact]
    public async Task Explicit_test_profile_blocks_party_management_and_Cron_before_transport()
    {
        using var handler = new Recorder(); using var http = new HttpClient(handler) { BaseAddress = new Uri("https://habitica.com/api/v3/") };
        var client = new HabiticaApiClient(http, new HabiticaApiClientOptions("fixture-owner-client", MinRequestSpacingMilliseconds: 0, OwnerIsolatedTestingEnabled: true));
        var credentials = new HabiticaCredentials("fixture-owner", "fixture-token");
        Func<Task>[] operations = [
            () => client.StartPartyQuestAsync(credentials, CancellationToken.None),
            () => client.InvitePartyToQuestAsync(credentials, "fixture-quest", CancellationToken.None),
            () => client.AcceptPartyQuestAsync(credentials, CancellationToken.None),
            () => client.RejectPartyQuestAsync(credentials, CancellationToken.None),
            () => client.RunCronAsync(credentials, CancellationToken.None)
        ];
        foreach (var operation in operations) Assert.Equal(HttpStatusCode.Forbidden, (await Assert.ThrowsAsync<HabiticaApiException>(operation)).StatusCode);
        Assert.Empty(handler.Paths);
    }

    [Fact]
    public async Task Safe_self_spell_and_equipment_keep_the_expected_own_account_routes()
    {
        using var handler = new Recorder(); using var http = new HttpClient(handler) { BaseAddress = new Uri("https://habitica.com/api/v3/") };
        var client = new HabiticaApiClient(http, new HabiticaApiClientOptions("fixture-owner-client", MinRequestSpacingMilliseconds: 0, OwnerIsolatedTestingEnabled: true));
        var credentials = new HabiticaCredentials("fixture-owner", "fixture-token");
        await client.CastSpellAsync(credentials, "defensiveStance", null, CancellationToken.None);
        await client.EquipGearAsync(credentials, EquipmentSetKind.Costume, "weapon_warrior_0", CancellationToken.None);
        Assert.Equal(new[] { "/api/v3/user/class/cast/defensiveStance", "/api/v3/user/equip/costume/weapon_warrior_0" }, handler.Paths);
    }

    public static IEnumerable<object[]> SupportedSpells()
    {
        foreach(var spell in new[]{"fireball","mpheal","earth","frost","smash","defensiveStance","valorousPresence","intimidate","pickPocket","backStab","toolsOfTrade","stealth","heal","brightness","protectAura","healAll"})
            foreach(var isolated in new[]{false,true})yield return [spell,isolated];
    }
    [Theory]
    [MemberData(nameof(SupportedSpells))]
    public async Task Supported_spells_reach_transport_in_normal_and_explicit_test_profiles(string spell,bool isolated)
    {
        using var handler=new Recorder();using var http=new HttpClient(handler){BaseAddress=new Uri("https://habitica.com/api/v3/")};
        var client=new HabiticaApiClient(http,new HabiticaApiClientOptions("fixture-owner-client",MinRequestSpacingMilliseconds:0,OwnerIsolatedTestingEnabled:isolated));
        var target=new[]{"fireball","smash","pickPocket","backStab"}.Contains(spell)?"fixture-task":null;
        await client.CastSpellAsync(new("fixture-owner","fixture-token"),spell,target,default);Assert.Equal("/api/v3/user/class/cast/"+spell,Assert.Single(handler.Paths));
    }
    [Fact]
    public async Task Default_user_client_keeps_existing_party_management_and_manual_Cron_routes()
    {
        using var handler=new Recorder();using var http=new HttpClient(handler){BaseAddress=new Uri("https://habitica.com/api/v3/")};
        var options=new HabiticaApiClientOptions("fixture-owner-client",MinRequestSpacingMilliseconds:0);Assert.False(options.OwnerIsolatedTestingEnabled);var client=new HabiticaApiClient(http,options);var credentials=new HabiticaCredentials("fixture-owner","fixture-token");
        await client.StartPartyQuestAsync(credentials,default);await client.InvitePartyToQuestAsync(credentials,"fixture-quest",default);await client.AcceptPartyQuestAsync(credentials,default);await client.RejectPartyQuestAsync(credentials,default);await client.RunCronAsync(credentials,default);Assert.Equal(5,handler.Paths.Count);
    }

    private sealed class Recorder : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"success\":true,\"data\":{}}", Encoding.UTF8, "application/json") });
        }
    }
}
