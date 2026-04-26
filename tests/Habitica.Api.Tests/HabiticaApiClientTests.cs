using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Habitica.Domain.Auth;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;

namespace Habitica.Api.Tests;

public sealed class HabiticaApiClientTests
{
    [Fact]
    public async Task GetUserAsync_sends_required_habitica_auth_headers()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(_ =>
        {
            capturedRequest = _;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
                {
                  "success": true,
                  "data": {
                    "profile": { "name": "Mage Tester" },
                    "stats": { "class": "wizard", "lvl": 15 }
                  }
                }
                """)
            };
        });

        var client = CreateClient(handler);

        await client.GetUserAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal(
            "https://habitica.com/api/v3/user?userFields=profile.name,stats.class,stats.lvl,stats.hp,stats.maxHealth,stats.mp,stats.maxMP,stats.exp,stats.toNextLevel,stats.gp,party._id,items.currentPet,items.currentMount,items.gear.equipped,items.gear.costume,items.gear.owned,items.eggs,items.food,items.hatchingPotions,items.quests,items.pets,items.mounts",
            capturedRequest.RequestUri!.ToString());
        Assert.Equal("user-id", capturedRequest.Headers.GetValues("x-api-user").Single());
        Assert.Equal("api-token", capturedRequest.Headers.GetValues("x-api-key").Single());
        Assert.Equal("habitica-tool-author-habitica-tool", capturedRequest.Headers.GetValues("x-client").Single());
    }

    [Fact]
    public async Task GetTasksAsync_maps_task_payload_into_domain_snapshots()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
            {
              "success": true,
              "data": [
                {
                  "id": "todo-1",
                  "type": "todo",
                  "text": "Buy milk",
                  "notes": "2 liters",
                  "completed": false,
                  "priority": 1.5,
                  "date": "2026-04-24T12:00:00.000Z"
                },
                {
                  "id": "daily-1",
                  "type": "daily",
                  "text": "Exercise",
                  "notes": null,
                  "completed": true,
                  "priority": 1.0,
                  "date": null
                }
              ]
            }
            """)
        });

        var client = CreateClient(handler);

        var snapshot = await client.GetTasksAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);

        Assert.Equal(2, snapshot.Items.Count);
        Assert.Equal(TaskType.Todo, snapshot.Items[0].Type);
        Assert.Equal("2 liters", snapshot.Items[0].Notes);
        Assert.Equal(TaskType.Daily, snapshot.Items[1].Type);
        Assert.True(snapshot.Items[1].IsCompleted);
    }

    [Fact]
    public async Task GetUserSnapshotAsync_maps_account_and_inventory_fields()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
            {
              "success": true,
              "data": {
                "profile": { "name": "Mage Tester" },
                "stats": {
                  "class": "wizard",
                  "lvl": 15,
                  "hp": 42.5,
                  "maxHealth": 50,
                  "mp": 33.5,
                  "maxMP": 40,
                  "exp": 125.1,
                  "toNextLevel": 74.9,
                  "gp": 88.25
                },
                "party": {
                  "_id": "party-123"
                },
                "items": {
                  "currentPet": "Wolf-Base",
                  "currentMount": "Wolf-Base",
                  "gear": {
                    "equipped": {
                      "head": "head_wizard_3",
                      "armor": "armor_wizard_4",
                      "weapon": "weapon_wizard_5",
                      "shield": "shield_wizard_2",
                      "back": "back_wizard_1"
                    },
                    "costume": {
                      "head": "head_special_2",
                      "armor": "armor_special_2",
                      "weapon": "weapon_special_2",
                      "shield": "shield_special_2",
                      "back": "back_special_2"
                    },
                    "owned": {
                      "head_wizard_3": true,
                      "armor_wizard_4": true,
                      "weapon_wizard_5": true,
                      "shield_wizard_2": true,
                      "armor_warrior_6": false
                    }
                  },
                  "eggs": {
                    "Wolf": 2,
                    "TigerCub": 0
                  },
                  "food": {
                    "Meat": 5
                  },
                  "hatchingPotions": {
                    "Base": 3,
                    "Golden": 0
                  },
                  "quests": {
                    "whale": 1
                  },
                  "pets": {
                    "Wolf-Base": 5,
                    "TigerCub-Base": -1
                  },
                  "mounts": {
                    "Wolf-Base": true,
                    "TigerCub-Base": false
                  }
                }
              }
            }
            """)
        });

        var client = CreateClient(handler);

        var snapshot = await client.GetUserSnapshotAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);

        Assert.Equal("Mage Tester", snapshot.DisplayName);
        Assert.Equal("wizard", snapshot.ClassName);
        Assert.Equal(15, snapshot.Level);
        Assert.Equal(42.5m, snapshot.Health);
        Assert.Equal(50m, snapshot.MaxHealth);
        Assert.Equal("party-123", snapshot.PartyId);
        Assert.Equal("Wolf-Base", snapshot.CurrentPetKey);
        Assert.Equal("head_wizard_3", snapshot.Equipment.Battle.Head);
        Assert.Equal("weapon_special_2", snapshot.Equipment.Costume.Weapon);
        Assert.Equal(new[] { "armor_wizard_4", "head_wizard_3", "shield_wizard_2", "weapon_wizard_5" }, snapshot.Inventory.OwnedGearKeys);
        Assert.Equal(1, snapshot.Inventory.EggCount);
        Assert.Equal(1, snapshot.Inventory.HatchingPotionCount);
        Assert.Equal(1, snapshot.Inventory.OwnedPetCount);
        Assert.Equal(1, snapshot.Inventory.OwnedMountCount);
    }

    [Fact]
    public async Task GetPartySnapshotAsync_maps_party_and_quest_fields()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
            {
              "success": true,
              "data": {
                "_id": "party-123",
                "name": "Night Owls",
                "summary": "Quest-focused party",
                "memberCount": 4,
                "quest": {
                  "key": "dragon",
                  "active": true,
                  "progress": {
                    "up": 12.5,
                    "down": 3
                  },
                  "members": {
                    "user-1": true,
                    "user-2": true,
                    "user-3": false
                  }
                }
              }
            }
            """)
        });

        var client = CreateClient(handler);

        var snapshot = await client.GetPartySnapshotAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);

        Assert.Equal("party-123", snapshot.PartyId);
        Assert.Equal("Night Owls", snapshot.Name);
        Assert.Equal("Quest-focused party", snapshot.Summary);
        Assert.Equal(4, snapshot.MemberCount);
        Assert.NotNull(snapshot.Quest);
        Assert.Equal("dragon", snapshot.Quest!.Key);
        Assert.True(snapshot.Quest.IsActive);
        Assert.Equal(12.5m, snapshot.Quest.ProgressUp);
        Assert.Equal(3m, snapshot.Quest.ProgressDown);
        Assert.Equal(2, snapshot.Quest.ParticipantCount);
    }

    [Fact]
    public async Task GetUserAsync_throws_normalized_exception_for_error_responses()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = JsonContent("""
            {
              "success": false,
              "error": "NotAuthorized",
              "message": "Invalid API key."
            }
            """)
        });

        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<HabiticaApiException>(
            () => client.GetUserAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Contains("Invalid API key.", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("api-token", exception.Message, StringComparison.Ordinal);
    }

    private static HabiticaApiClient CreateClient(HttpMessageHandler handler)
    {
        return new HabiticaApiClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://habitica.com/api/v3/")
            },
            new HabiticaApiClientOptions("habitica-tool-author-habitica-tool"));
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}
