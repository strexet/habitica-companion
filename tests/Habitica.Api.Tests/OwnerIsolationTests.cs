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
    public async Task Shared_mutations_and_other_spell_targets_never_reach_transport()
    {
        using var handler = new Recorder(); using var http = new HttpClient(handler) { BaseAddress = new Uri("https://habitica.com/api/v3/") };
        var client = new HabiticaApiClient(http, new HabiticaApiClientOptions("fixture-owner-client", MinRequestSpacingMilliseconds: 0));
        var credentials = new HabiticaCredentials("fixture-owner", "fixture-token");
        Func<Task>[] operations = [
            () => client.StartPartyQuestAsync(credentials, CancellationToken.None),
            () => client.InvitePartyToQuestAsync(credentials, "fixture-quest", CancellationToken.None),
            () => client.AcceptPartyQuestAsync(credentials, CancellationToken.None),
            () => client.RejectPartyQuestAsync(credentials, CancellationToken.None),
            () => client.RunCronAsync(credentials, CancellationToken.None),
            () => client.CastSpellAsync(credentials, "healAll", null, CancellationToken.None),
            () => client.CastSpellAsync(credentials, "valorousPresence", null, CancellationToken.None),
            () => client.CastSpellAsync(credentials, "heal", "another-user", CancellationToken.None),
            () => client.CastSpellAsync(credentials, "unknown-spell", null, CancellationToken.None)
        ];
        foreach (var operation in operations) Assert.Equal(HttpStatusCode.Forbidden, (await Assert.ThrowsAsync<HabiticaApiException>(operation)).StatusCode);
        Assert.Empty(handler.Paths);
    }

    [Fact]
    public async Task Safe_self_spell_and_equipment_keep_the_expected_own_account_routes()
    {
        using var handler = new Recorder(); using var http = new HttpClient(handler) { BaseAddress = new Uri("https://habitica.com/api/v3/") };
        var client = new HabiticaApiClient(http, new HabiticaApiClientOptions("fixture-owner-client", MinRequestSpacingMilliseconds: 0));
        var credentials = new HabiticaCredentials("fixture-owner", "fixture-token");
        await client.CastSpellAsync(credentials, "defensiveStance", null, CancellationToken.None);
        await client.EquipGearAsync(credentials, EquipmentSetKind.Costume, "weapon_warrior_0", CancellationToken.None);
        Assert.Equal(new[] { "/api/v3/user/class/cast/defensiveStance", "/api/v3/user/equip/costume/weapon_warrior_0" }, handler.Paths);
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
