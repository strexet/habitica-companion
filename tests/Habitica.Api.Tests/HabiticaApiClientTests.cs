using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Habitica.Domain.Auth;
using Habitica.Domain.Tasks;

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
