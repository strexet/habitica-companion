using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;
using Habitica.Storage;
using Habitica.WebApp.State;
using System.Text.Json;

namespace Habitica.WebApp.Tests.State;

public sealed partial class AppSessionControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pwa_relaunch_restores_only_explicitly_persisted_credentials(bool remember)
    {
        var credentials = new PwaCredentialStore();
        var first = CreateController(new FakeDiagnosticsLogStore([]), credentialStore: credentials);
        await first.SignInAsync(new SignInRequest { UserId = "fixture-user", ApiToken = "pwa-secret-canary", PersistLocally = remember });
        Assert.True(first.State.IsAuthenticated);
        Assert.Equal(remember, credentials.Saved is not null);

        var relaunched = CreateController(new FakeDiagnosticsLogStore([]), credentialStore: credentials);
        await relaunched.InitializeAsync();

        Assert.Equal(remember, relaunched.State.IsAuthenticated);
        Assert.Equal(remember ? "fixture-user" : null, relaunched.State.UserId);
        Assert.DoesNotContain("pwa-secret-canary", JsonSerializer.Serialize(relaunched.State));
    }

    [Fact]
    public async Task Pwa_relaunch_with_unavailable_storage_stays_signed_out_and_reports_recovery()
    {
        var credentials = new PwaCredentialStore { ReadFailure = new Microsoft.JSInterop.JSException("private storage detail canary") };
        var controller = CreateController(new FakeDiagnosticsLogStore([]), credentialStore: credentials);

        await controller.InitializeAsync();

        Assert.False(controller.State.IsAuthenticated);
        Assert.Contains("Sign in again", controller.State.ErrorMessage);
        Assert.DoesNotContain("private storage detail canary", JsonSerializer.Serialize(controller.State));
        Assert.Null(credentials.Saved);
    }

    [Fact]
    public async Task Pwa_relaunch_preserves_cancellation_without_restoring_credentials()
    {
        var credentials = new PwaCredentialStore { ReadFailure = new OperationCanceledException() };
        var controller = CreateController(new FakeDiagnosticsLogStore([]), credentialStore: credentials);

        await Assert.ThrowsAsync<OperationCanceledException>(() => controller.InitializeAsync());

        Assert.False(controller.State.IsAuthenticated);
        Assert.Null(credentials.Saved);
    }

    private sealed class PwaCredentialStore : ICredentialStore
    {
        public HabiticaCredentials? Saved { get; private set; }
        public Exception? ReadFailure { get; init; }
        public Task<HabiticaCredentials?> GetPersistentCredentialsAsync(CancellationToken cancellationToken) =>
            ReadFailure is null ? Task.FromResult(Saved) : Task.FromException<HabiticaCredentials?>(ReadFailure);
        public Task SavePersistentCredentialsAsync(HabiticaCredentials credentials, CancellationToken cancellationToken) { Saved = credentials; return Task.CompletedTask; }
        public Task ClearPersistentCredentialsAsync(CancellationToken cancellationToken) { Saved = null; return Task.CompletedTask; }
    }
}
