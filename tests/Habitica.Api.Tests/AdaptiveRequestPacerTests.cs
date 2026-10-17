using System.Net;
using System.Text;
using Habitica.Domain.Auth;

namespace Habitica.Api.Tests;

public sealed class AdaptiveRequestPacerTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-17T00:00:00Z");

    [Fact]
    public void Sustained_load_slows_at_half_budget_and_preserves_reserve()
    {
        var pacer = new AdaptiveRequestPacer(TimeSpan.Zero, Start);
        var now = Start;
        var sawEarlySlowdown = false;
        var delayed = 0;
        for (var index = 0; index < 100; index++)
        {
            var status = pacer.Inspect(now);
            if (status.NextDelay > TimeSpan.Zero)
            {
                delayed++;
                if (status.EstimatedRemaining > status.Reserve + 5) sawEarlySlowdown = true;
                now += status.NextDelay;
            }
            Assert.True(pacer.Inspect(now).EstimatedRemaining >= status.Reserve + 1 - 0.000001);
            pacer.Dispatch(now);
            Assert.True(pacer.Inspect(now).EstimatedRemaining >= status.Reserve - 0.000001);
        }
        Assert.True(sawEarlySlowdown);
        Assert.True(delayed > 75);
        Assert.True(now - Start > TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void Header_budget_predicts_reset_horizon_and_lower_limits_are_respected()
    {
        var pacer = new AdaptiveRequestPacer(TimeSpan.Zero, Start);
        pacer.Dispatch(Start);
        pacer.Observe(new(null, 30, 14, Start.AddSeconds(60)), true, false, Start, 0);
        Assert.True(pacer.Inspect(Start).NextDelay >= TimeSpan.FromSeconds(5));
        pacer.Observe(new(null, 4, 2, Start.AddSeconds(60)), true, false, Start, 0);
        var status = pacer.Inspect(Start);
        Assert.Equal(2, status.Reserve);
        Assert.True(status.NextDelay >= TimeSpan.FromSeconds(15));
        pacer.Observe(new(null, -1, -1, null), true, false, Start, 0);
        Assert.Equal(status.EstimatedRemaining, pacer.Inspect(Start).EstimatedRemaining);
    }

    [Fact]
    public void Server_pause_never_shortens_and_recovery_requires_sustained_success()
    {
        var pacer = new AdaptiveRequestPacer(TimeSpan.Zero, Start);
        pacer.Dispatch(Start);
        pacer.Observe(new(TimeSpan.FromSeconds(10), 30, 0, Start.AddSeconds(60)), false, true, Start, 1);
        Assert.Equal(Start.AddSeconds(60), pacer.Inspect(Start).CooldownUntilUtc);
        pacer.Observe(new(TimeSpan.FromSeconds(1), null, null, null), false, true, Start, 0);
        Assert.Equal(Start.AddSeconds(60), pacer.Inspect(Start).CooldownUntilUtc);
        Assert.Equal(4, pacer.Inspect(Start).RecoveryFactor);
        pacer.Observe(null, true, false, Start.AddSeconds(60), 0);
        Assert.Equal(3.875, pacer.Inspect(Start.AddSeconds(60)).RecoveryFactor);
        for (var index = 0; index < 24; index++) pacer.Observe(null, true, false, Start.AddSeconds(60), 0);
        Assert.Equal(1, pacer.Inspect(Start.AddSeconds(60)).RecoveryFactor);
    }

    [Fact]
    public void Recovery_backoff_is_bounded_and_independent_from_hard_cooldown()
    {
        var pacer = new AdaptiveRequestPacer(TimeSpan.Zero, Start);
        pacer.Dispatch(Start);
        for (var index = 0; index < 100; index++) pacer.Observe(null, false, true, Start, 1);
        var failed = pacer.Inspect(Start);
        Assert.Equal(16, failed.RecoveryFactor);
        Assert.InRange(failed.NextDelay.TotalSeconds, 16.5, 32);
        Assert.Equal(Start.AddSeconds(16.5), failed.CooldownUntilUtc);
        Assert.Equal(TimeSpan.Zero, pacer.Inspect(Start.AddSeconds(60)).NextDelay);
        // Passing time alone clears the hard pause, but cannot count as successful recovery.
        Assert.Equal(16, pacer.Inspect(Start.AddSeconds(60)).RecoveryFactor);
    }

    [Fact]
    public async Task Concurrent_calls_wait_for_headers_and_cancellation_releases_the_queue()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        using var http = new HttpClient(new Handler(async (_, token) =>
        {
            Interlocked.Increment(ref count); entered.TrySetResult(); await release.Task.WaitAsync(token);
            var response = Success(); response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0");
            response.Headers.RetryAfter = new(TimeSpan.FromMinutes(1)); return response;
        })) { BaseAddress = new Uri("https://synthetic.invalid/api/v3/") };
        var client = new HabiticaApiClient(http, new("synthetic-client", MinRequestSpacingMilliseconds: 0));
        var credentials = new HabiticaCredentials("synthetic-user", "synthetic-token");
        var first = client.GetUserAsync(credentials, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var queuedCancellation = new CancellationTokenSource();
        var queued = client.GetUserAsync(credentials, queuedCancellation.Token);
        queuedCancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        release.TrySetResult(); await first;
        using var cooldownCancellation = new CancellationTokenSource();
        var cooling = client.GetUserAsync(credentials, cooldownCancellation.Token);
        cooldownCancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cooling);
        Assert.Equal(1, count);
        Assert.True(client.PacingStatus.NextDelay > TimeSpan.FromSeconds(50));
    }

    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    [InlineData(0)]
    public async Task Failed_dispatch_is_never_replayed(int status)
    {
        var count = 0;
        using var http = new HttpClient(new Handler((_, _) =>
        {
            count++;
            if (status == 0) throw new HttpRequestException("Synthetic transport failure");
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("{}") });
        })) { BaseAddress = new Uri("https://synthetic.invalid/api/v3/") };
        var client = new HabiticaApiClient(http, new("synthetic-client", MinRequestSpacingMilliseconds: 0));
        var action = () => client.EquipGearAsync(new("synthetic-user", "synthetic-token"), "synthetic-gear", CancellationToken.None);
        if (status == 0) await Assert.ThrowsAsync<HttpRequestException>(action);
        else await Assert.ThrowsAsync<HabiticaApiException>(action);
        Assert.Equal(1, count); Assert.Equal(2, client.PacingStatus.RecoveryFactor);
        Assert.True(client.PacingStatus.CooldownUntilUtc > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Synthetic_clock_load_exercises_actual_client_dispatch_without_waiting_in_real_time()
    {
        var clock = new AdvancingClock();
        var times = new List<DateTimeOffset>();
        using var http = new HttpClient(new Handler((_, _) => { times.Add(clock.GetUtcNow()); return Task.FromResult(Success()); }))
            { BaseAddress = new Uri("https://synthetic.invalid/api/v3/") };
        var client = new HabiticaApiClient(http, new("synthetic-client", MinRequestSpacingMilliseconds: 0), clock);
        for (var index = 0; index < 100; index++)
        {
            await client.GetUserAsync(new("synthetic-user", "synthetic-token"), CancellationToken.None);
            Assert.True(client.PacingStatus.EstimatedRemaining >= client.PacingStatus.Reserve - 0.000001);
        }
        Assert.Equal(100, times.Count);
        Assert.True(times[16] > times[15]);
        Assert.True(times[^1] - times[0] > TimeSpan.FromSeconds(120));
    }

    [Theory]
    [InlineData("999999999999999999")]
    [InlineData("-60")]
    [InlineData("not-a-reset")]
    public async Task Malformed_reset_headers_do_not_turn_success_into_a_parser_failure(string reset)
    {
        using var http = new HttpClient(new Handler((_, _) =>
        {
            var response = Success(); response.Headers.TryAddWithoutValidation("X-RateLimit-Reset", reset);
            return Task.FromResult(response);
        })) { BaseAddress = new Uri("https://synthetic.invalid/api/v3/") };
        var client = new HabiticaApiClient(http, new("synthetic-client", MinRequestSpacingMilliseconds: 0));
        Assert.Equal("Synthetic", (await client.GetUserAsync(new("synthetic-user", "synthetic-token"), CancellationToken.None)).DisplayName);
    }

    // Single-caller virtual load: advancing synthetic time expires the request's own timer.
    private sealed class AdvancingClock : TimeProvider
    {
        private DateTimeOffset _now = Start;
        public override DateTimeOffset GetUtcNow() => _now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.True(dueTime > TimeSpan.Zero);
            _now += dueTime; callback(state); return new CompletedTimer();
        }
        private sealed class CompletedTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private static HttpResponseMessage Success() => new(HttpStatusCode.OK) { Content = new StringContent("""{"success":true,"data":{"profile":{"name":"Synthetic"},"stats":{"lvl":1}}}""", Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
