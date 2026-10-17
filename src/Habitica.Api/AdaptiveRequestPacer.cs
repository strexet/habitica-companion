namespace Habitica.Api;

// Anonymous state only: no account, request URL or credential values.
public sealed record HabiticaPacingStatus(double EstimatedRemaining, double Reserve, double RecoveryFactor,
    DateTimeOffset? CooldownUntilUtc, TimeSpan NextDelay);

internal sealed class AdaptiveRequestPacer(TimeSpan minimumSpacing, DateTimeOffset startedAt)
{
    private const double WindowSeconds = 60;
    private double _limit = 30;
    private double _tokens = 30;
    private double _recovery = 1;
    private DateTimeOffset _updatedAt = startedAt;
    private DateTimeOffset? _lastDispatch;
    private DateTimeOffset? _resetAt;
    private DateTimeOffset? _cooldown;
    private double Reserve => Math.Min(2, Math.Max(0, _limit - 1));

    public HabiticaPacingStatus Inspect(DateTimeOffset now)
    {
        Refill(now);
        var secondsPerToken = WindowSeconds / _limit;
        var wait = Math.Max(0, minimumSpacing.TotalSeconds - (now - (_lastDispatch ?? DateTimeOffset.MinValue)).TotalSeconds);
        // Start pacing at half capacity, accounting for the request about to spend a token.
        var afterDispatch = _tokens - 1;
        if (afterDispatch < _limit * 0.5)
        {
            var depletion = Math.Clamp((_limit * 0.5 - afterDispatch) / Math.Max(1, _limit * 0.5 - Reserve), 0, 1);
            var pacedGap = secondsPerToken * depletion;
            if (_resetAt > now)
                pacedGap = Math.Max(pacedGap, (_resetAt.Value - now).TotalSeconds / Math.Max(1, _tokens - Reserve));
            wait = Math.Max(wait, pacedGap - SinceLast(now));
        }
        // Waiting must actually replenish the reserve; a ramp alone is insufficient.
        wait = Math.Max(wait, (Reserve + 1 - _tokens) * secondsPerToken);
        if (_recovery > 1) wait = Math.Max(wait, secondsPerToken * _recovery - SinceLast(now));
        if (_cooldown > now) wait = Math.Max(wait, (_cooldown.Value - now).TotalSeconds);
        return new(_tokens, Reserve, _recovery, _cooldown > now ? _cooldown : null, TimeSpan.FromSeconds(Math.Max(0, wait)));
    }

    public void Dispatch(DateTimeOffset now)
    {
        Refill(now); _tokens = Math.Max(0, _tokens - 1); _lastDispatch = now;
    }

    public void Observe(HabiticaRateLimitInfo? rate, bool success, bool transientFailure, DateTimeOffset now, double jitter)
    {
        Refill(now);
        if (rate?.Limit is > 0) _limit = rate.Limit.Value;
        _tokens = Math.Min(_tokens, _limit);
        if (rate?.Remaining is >= 0) { _tokens = Math.Clamp(rate.Remaining.Value, 0, _limit); _updatedAt = now; }
        if (rate?.ResetAtUtc is { } reset && reset > now) _resetAt = reset;
        if (rate?.RetryAfter is { } retry && retry > TimeSpan.Zero) Pause(now + retry);
        if (rate?.Remaining == 0 && rate.ResetAtUtc > now) Pause(rate.ResetAtUtc.Value);
        if (transientFailure)
        {
            _recovery = Math.Min(16, _recovery * 2);
            // Positive bounded jitter spreads later explicit requests; never retries this request.
            Pause(now + TimeSpan.FromSeconds(Math.Min(60, _recovery) + Math.Clamp(jitter, 0, 1) * 0.5));
        }
        else if (success) _recovery = Math.Max(1, _recovery - 0.125);
    }

    private double SinceLast(DateTimeOffset now) => _lastDispatch is { } last ? Math.Max(0, (now - last).TotalSeconds) : double.MaxValue;
    private void Pause(DateTimeOffset until) { if (_cooldown is null || until > _cooldown) _cooldown = until; }
    private void Refill(DateTimeOffset now)
    {
        if (now <= _updatedAt) return;
        _tokens = Math.Min(_limit, _tokens + (now - _updatedAt).TotalSeconds * _limit / WindowSeconds);
        _updatedAt = now;
        if (_resetAt <= now) _resetAt = null;
    }
}
