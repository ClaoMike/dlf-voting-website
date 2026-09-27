using System.Threading.RateLimiting;

namespace DlfVoting.Api;

/// <summary>
/// Limits sign-in attempts per account (not per IP address: most voters share the office's address, and a per-IP
/// limit would lock them all out at once). Counts every attempt, successful or not. In memory, so each app instance
/// counts on its own.
/// </summary>
public sealed class LoginAttemptLimiter : IDisposable
{
    public const string TooManyAttemptsMessage = "Too many sign-in attempts. Please wait a few minutes and try again.";

    private readonly PartitionedRateLimiter<string> _limiter;

    public LoginAttemptLimiter(IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("LoginRateLimit:PermitLimit", 10);
        var window = TimeSpan.FromSeconds(configuration.GetValue("LoginRateLimit:WindowSeconds", 300));

        _limiter = PartitionedRateLimiter.Create<string, string>(account =>
            RateLimitPartition.GetFixedWindowLimiter(account, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0
            }));
    }

    /// <summary>False when this account (of this kind) has had too many sign-in attempts recently.</summary>
    public bool TryAttempt(string scheme, string? username)
    {
        using var lease = _limiter.AttemptAcquire($"{scheme}:{username?.Trim().ToLowerInvariant()}");
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
