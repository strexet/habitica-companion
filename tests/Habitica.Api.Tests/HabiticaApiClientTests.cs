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
        Assert.Equal("https://habitica.com/api/v3/user", capturedRequest.RequestUri!.ToString());
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
                  "value": 18.25,
                  "challenge": {
                    "id": "challenge-1",
                    "taskId": "challenge-task-1"
                  },
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
        Assert.Equal(1.5m, snapshot.Items[0].Difficulty);
        Assert.Equal(18.25m, snapshot.Items[0].Value);
        Assert.True(snapshot.Items[0].IsChallengeTask);
        Assert.Equal(TaskType.Daily, snapshot.Items[1].Type);
        Assert.True(snapshot.Items[1].IsCompleted);
        Assert.False(snapshot.Items[1].IsChallengeTask);
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
                  "gp": 88.25,
                  "points": 4,
                  "str": 12,
                  "int": 34,
                  "con": 18,
                  "per": 21,
                  "buffs": {
                    "str": 2,
                    "int": 5,
                    "con": 3,
                    "per": 4,
                    "streaks": true,
                    "stealth": 7
                  }
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
        Assert.Equal(40m, snapshot.MaxMana);
        Assert.Equal(74.9m, snapshot.ToNextLevel);
        Assert.Equal(4, snapshot.UnallocatedStatPoints);
        Assert.Equal(new CharacterStatsSnapshot(12m, 34m, 18m, 21m), snapshot.Stats);
        Assert.Equal(new CharacterStatsSnapshot(2m, 5m, 3m, 4m), snapshot.Buffs);
        Assert.True(snapshot.BuffFlags.ChillingFrost);
        Assert.Equal(7, snapshot.BuffFlags.Stealth);
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
    public async Task CastSpellAsync_sends_spell_cast_request_with_target_id()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{ "success": true, "data": {} }""")
            };
        });
        var client = CreateClient(handler);

        await client.CastSpellAsync(
            new HabiticaCredentials("user-id", "api-token"),
            "fireball",
            "task-123",
            CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://habitica.com/api/v3/user/class/cast/fireball?targetId=task-123", capturedRequest.RequestUri!.ToString());
        Assert.Null(capturedRequest.Content);
    }

    [Fact]
    public async Task CastSpellAsync_sends_spell_cast_request_without_target_for_party_or_self_spells()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{ "success": true, "data": {} }""")
            };
        });
        var client = CreateClient(handler);

        await client.CastSpellAsync(
            new HabiticaCredentials("user-id", "api-token"),
            "earth",
            targetId: null,
            CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://habitica.com/api/v3/user/class/cast/earth", capturedRequest.RequestUri!.ToString());
        Assert.Null(capturedRequest.Content);
    }

    [Fact]
    public async Task AllocateStatsAsync_sends_bulk_allocation_request()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{ "success": true, "data": {} }""")
            };
        });
        var client = CreateClient(handler);

        await client.AllocateStatsAsync(
            new HabiticaCredentials("user-id", "api-token"),
            new StatAllocation(Strength: 1, Intelligence: 2, Constitution: 0, Perception: 1),
            CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://habitica.com/api/v3/user/allocate-bulk", capturedRequest.RequestUri!.ToString());
        Assert.Equal("""{"stats":{"str":1,"int":2,"con":0,"per":1}}""", capturedBody);
    }

    [Fact]
    public async Task GetPartySnapshotAsync_maps_party_and_quest_fields()
    {
        var todayUtc = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var alphaCron = new DateTimeOffset(todayUtc.AddHours(5).AddMinutes(15), TimeSpan.Zero);
        var betaCron = new DateTimeOffset(todayUtc.AddHours(6).AddMinutes(45), TimeSpan.Zero);
        var betaCreated = DateTimeOffset.Parse("2025-01-02T03:04:05Z");
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
            {
              "success": true,
              "data": {
                "_id": "party-123",
                "name": "Night Owls",
                "description": "Quest-focused party notes",
                "leader": "user-1",
                "memberCount": 4,
                "quest": {
                  "key": "seaserpent",
                  "active": true,
                  "progress": {
                    "up": 2.1,
                    "hp": 875.25,
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
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent($$"""
            {
              "success": true,
              "data": [
                {
                  "_id": "user-1",
                  "profile": { "name": "Alpha" },
                  "lastCron": "{{alphaCron:O}}",
                  "party": {
                    "quest": {
                      "progress": {
                        "up": 7.2
                      }
                    }
                  },
                  "preferences": {
                    "dayStart": 0,
                    "timezoneOffset": 0
                  },
                  "stats": {
                    "lvl": 72,
                    "class": "warrior",
                    "str": 30,
                    "int": 70,
                    "con": 0,
                    "per": 0,
                    "buffs": {
                      "str": 80,
                      "int": 50,
                      "con": 213,
                      "per": 192
                    }
                  },
                  "items": {
                    "gear": {
                      "equipped": {
                        "weapon": "weapon_warrior_1"
                      }
                    }
                  }
                },
                {
                  "_id": "user-2",
                  "profile": { "name": "Beta" },
                  "party": {
                    "quest": {
                      "progress": {
                        "up": 5.3
                      }
                    }
                  },
                  "auth": {
                    "timestamps": {
                      "created": "{{betaCreated:O}}",
                      "loggedin": "{{betaCron:O}}"
                    }
                  }
                }
              ]
            }
            """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
            {
              "success": true,
              "data": {
                "gear": {
                  "flat": {
                    "weapon_warrior_1": {
                      "key": "weapon_warrior_1",
                      "klass": "warrior",
                      "str": 10,
                      "int": 0,
                      "con": 0,
                      "per": 0
                    }
                  }
                },
                "quests": {
                  "seaserpent": {
                    "boss": {
                      "hp": 1000
                    },
                    "drop": {
                      "gp": 10,
                      "exp": 100,
                      "items": [
                        { "text": "Sea Serpent Egg" }
                      ]
                    }
                  }
                }
              }
            }
            """)
            }
        });
        var handler = new StubHttpMessageHandler(_ => responses.Dequeue());

        var client = CreateClient(handler);

        var snapshot = await client.GetPartySnapshotAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);

        Assert.Equal("party-123", snapshot.PartyId);
        Assert.Equal("Night Owls", snapshot.Name);
        Assert.Equal("Quest-focused party notes", snapshot.Summary);
        Assert.Equal("user-1", snapshot.LeaderId);
        Assert.Equal(4, snapshot.MemberCount);
        Assert.NotNull(snapshot.Quest);
        Assert.Equal("seaserpent", snapshot.Quest!.Key);
        Assert.True(snapshot.Quest.IsActive);
        Assert.Equal(2.1m, snapshot.Quest.PendingDamage);
        Assert.Equal(12.5m, snapshot.Quest.TotalPendingDamage);
        Assert.Equal(875.25m, snapshot.Quest.BossHealthRemaining);
        Assert.Equal(1000m, snapshot.Quest.BossHealthTotal);
        Assert.Equal(3m, snapshot.Quest.PendingPartyDamage);
        Assert.Equal(new[] { "10 Gold", "100 XP", "Sea Serpent Egg" }, snapshot.Quest.Rewards);
        Assert.Equal(2, snapshot.Quest.ParticipantCount);
        Assert.Equal(2, snapshot.Members.Count);
        Assert.Equal("Alpha", snapshot.Members[0].DisplayName);
        Assert.Equal(alphaCron, snapshot.Members[0].LastCronUtc);
        Assert.Equal(7.2m, snapshot.Members[0].PendingQuestDamage);
        Assert.Equal(PartyCronState.CronedToday, snapshot.Members[0].CronState);
        Assert.Equal(30m, snapshot.Members[0].Stats!.BaseAllocated!.Strength);
        Assert.Equal(80m, snapshot.Members[0].Stats!.Buffs!.Strength);
        Assert.Equal(15m, snapshot.Members[0].Stats!.Gear!.Strength);
        Assert.Equal(36m, snapshot.Members[0].Stats!.LevelBonus!.Strength);
        Assert.Equal(161m, snapshot.Members[0].Stats!.Total!.Strength);
        Assert.Equal("Beta", snapshot.Members[1].DisplayName);
        Assert.Equal(betaCron, snapshot.Members[1].LastCronUtc);
        Assert.Equal(betaCreated, snapshot.Members[1].CreatedAtUtc);
        Assert.Equal(betaCron, snapshot.Members[1].LastLoggedInUtc);
        Assert.Equal(5.3m, snapshot.Members[1].PendingQuestDamage);
        Assert.Equal(PartyCronState.CronedToday, snapshot.Members[1].CronState);
    }

    [Fact]
    public async Task GetPartySnapshotAsync_maps_collection_pending_items_from_party_members()
    {
        var todayUtc = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var alphaCron = new DateTimeOffset(todayUtc.AddHours(5), TimeSpan.Zero);
        var betaCron = new DateTimeOffset(todayUtc.AddHours(6), TimeSpan.Zero);
        var gammaCron = new DateTimeOffset(todayUtc.AddHours(7), TimeSpan.Zero);
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
            {
              "success": true,
              "data": {
                "_id": "party-123",
                "name": "Night Owls",
                "memberCount": 3,
                "quest": {
                  "key": "evilsanta",
                  "active": true,
                  "progress": {
                    "collect": {
                      "milk": 4,
                      "cookies": 5
                    }
                  },
                  "members": {
                    "user-1": true,
                    "user-2": true,
                    "user-3": false
                  }
                },
                "quests": {
                  "seaserpent": {
                    "key": "seaserpent",
                    "text": "Sea Serpent",
                    "boss": {
                      "hp": 1000
                    },
                    "rewards": {
                      "gp": 20,
                      "exp": 250,
                      "items": {
                        "egg": { "text": "Sea Serpent Egg" },
                        "hatchingPotion": { "key": "Shade Hatching Potion" }
                      },
                      "unlock": [
                        { "name": "Sea Serpent Questline" }
                      ]
                    }
                  }
                }
              }
            }
            """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent($$"""
            {
              "success": true,
              "data": [
                {
                  "_id": "user-1",
                  "profile": { "name": "Alpha" },
                  "lastCron": "{{alphaCron:O}}",
                  "party": {
                    "quest": {
                      "progress": {
                        "collectedItems": 3
                      }
                    }
                  }
                },
                {
                  "_id": "user-2",
                  "profile": { "name": "Beta" },
                  "lastCron": "{{betaCron:O}}",
                  "party": {
                    "quest": {
                      "progress": {
                        "collectedItems": 4
                      }
                    }
                  }
                },
                {
                  "_id": "user-3",
                  "profile": { "name": "Gamma" },
                  "lastCron": "{{gammaCron:O}}",
                  "party": {
                    "quest": {
                      "progress": {
                        "collectedItems": 9
                      }
                    }
                  }
                }
              ]
            }
            """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
            {
              "success": true,
              "data": {
                "quests": {
                  "evilsanta": {
                    "text": "Trapper Santa",
                    "notes": "A quest about Santa.",
                    "collect": {
                      "milk": { "text": "Milk", "count": 10 },
                      "cookies": { "text": "Cookies", "count": 10 }
                    }
                  }
                }
              }
            }
            """)
            }
        });
        var handler = new StubHttpMessageHandler(_ => responses.Dequeue());
        var client = CreateClient(handler);

        var snapshot = await client.GetPartySnapshotAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);

        Assert.NotNull(snapshot.Quest);
        Assert.Equal("evilsanta", snapshot.Quest!.Key);
        Assert.Equal("Trapper Santa", snapshot.Quest.Name);
        Assert.Null(snapshot.Quest.TotalPendingDamage);
        Assert.Equal(7m, snapshot.Quest.TotalPendingCollectionItems);
        Assert.Equal(3m, snapshot.Members[0].PendingQuestItems);
        Assert.Equal(4m, snapshot.Members[1].PendingQuestItems);
        Assert.Equal(9m, snapshot.Members[2].PendingQuestItems);
        Assert.Equal(PartyQuestParticipationStatus.Accepted, snapshot.Members[0].ParticipationStatus);
        Assert.Equal(PartyQuestParticipationStatus.Accepted, snapshot.Members[1].ParticipationStatus);
        Assert.Equal(PartyQuestParticipationStatus.Rejected, snapshot.Members[2].ParticipationStatus);
        Assert.Empty(responses);
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

    [Fact]
    public async Task EquipGearAsync_sends_battle_or_costume_equip_request()
    {
        var requestedUris = new List<string>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUris.Add(request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{ "success": true, "data": {} }""")
            };
        });
        var client = CreateClient(handler);
        var credentials = new HabiticaCredentials("user-id", "api-token");

        await client.EquipGearAsync(credentials, EquipmentSetKind.Battle, "weapon_wizard_5", CancellationToken.None);
        await client.EquipGearAsync(credentials, EquipmentSetKind.Costume, "head_special_2", CancellationToken.None);

        Assert.Equal(
            new[]
            {
                "https://habitica.com/api/v3/user/equip/equipped/weapon_wizard_5",
                "https://habitica.com/api/v3/user/equip/costume/head_special_2"
            },
            requestedUris);
    }

    [Fact]
    public async Task GetContentCatalogAsync_maps_flat_gear_catalog()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
            {
              "success": true,
              "data": {
                "gear": {
                  "flat": {
                    "weapon_wizard_5": {
                      "key": "weapon_wizard_5",
                      "text": "Wizard Wand",
                      "type": "weapon",
                      "klass": "wizard",
                      "notes": "A focused casting weapon.",
                      "twoHanded": true,
                      "str": 0,
                      "int": 12,
                      "con": 0,
                      "per": 2
                    }
                  }
                },
                "quests": {
                  "seaserpent": {
                    "key": "seaserpent",
                    "text": "Sea Serpent",
                    "boss": {
                      "hp": 1000
                    },
                    "rewards": {
                      "gp": 20,
                      "exp": 250,
                      "items": {
                        "egg": { "text": "Sea Serpent Egg" },
                        "hatchingPotion": { "key": "Shade Hatching Potion" }
                      },
                      "unlock": [
                        { "name": "Sea Serpent Questline" }
                      ]
                    }
                  }
                }
              }
            }
            """)
        });
        var client = CreateClient(handler);

        var catalog = await client.GetContentCatalogAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);

        var item = Assert.Single(catalog.Items.Values);
        Assert.Equal("weapon_wizard_5", item.Key);
        Assert.Equal("Wizard Wand", item.Text);
        Assert.Equal("Weapon", item.SlotTitle);
        Assert.Equal("wizard", item.ClassName);
        Assert.Equal("A focused casting weapon.", item.Notes);
        Assert.Equal(new GearStatBlock(0m, 12m, 0m, 2m), item.Stats);
        Assert.True(item.TwoHanded);
        var quest = Assert.Single(catalog.QuestItems.Values);
        Assert.Equal("seaserpent", quest.Key);
        Assert.Equal(new[] { "20 Gold", "250 XP", "Sea Serpent Egg", "Shade Hatching Potion", "Sea Serpent Questline" }, quest.RewardSummary);
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
