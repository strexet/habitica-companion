using System.Net;
using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed partial class WorkspaceCloudClientTests
{
    [Theory]
    [InlineData(CompanionTaskKind.Todo, true)]
    [InlineData(CompanionTaskKind.Daily, false)]
    [InlineData(CompanionTaskKind.Habit, false)]
    public async Task Exact_retained_adoption_preserves_reviewed_identity_and_accepts_only_the_original_revision(CompanionTaskKind kind, bool completed)
    {
        var taskId = Guid.NewGuid(); var remoteId = Guid.NewGuid(); var epoch = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var task = new CompanionTask(taskId, 2, "Retained", kind, completed ? CompanionTaskState.Completed : CompanionTaskState.Active, CompanionPriority.High, now, now, IsArchived: true);
        var preview = new WorkspaceProjectionPreview(_id, epoch, task, new(remoteId, kind.ToString().ToLowerInvariant(), "Remote", "Notes", null, 1, "str", null, [], completed, true, true, null, new string('a', 64)), 3, 0);
        var vault = Vault(out var vaultHttp); using var ownedVault = vaultHttp; var receiptRevision = 4L;
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Connection();
            if (request.RequestUri.AbsolutePath.EndsWith("preview")) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(WorkspaceTaskApiJson.Serialize(preview)) };
            Assert.Equal("/projection/adopt", request.RequestUri.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal(epoch, body.RootElement.GetProperty("expectedAccessEpoch").GetGuid());
            Assert.Equal(remoteId, body.RootElement.GetProperty("habiticaTaskId").GetGuid());
            Assert.Equal(3, body.RootElement.GetProperty("expectedRevision").GetInt64());
            return Json(new { adopted = true, taskId, habiticaTaskId = remoteId, revision = receiptRevision });
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        var reviewed = await client.PreviewLinkAsync(taskId, remoteId, default); Assert.True(reviewed.Succeeded);
        Assert.True((await client.AdoptLinkAsync(reviewed.Value!, default)).Succeeded);
        receiptRevision = 5; Assert.False((await client.AdoptLinkAsync(reviewed.Value!, default)).Succeeded);
    }

    [Theory]
    [InlineData("habit", false)]
    [InlineData("todo", true)]
    public async Task Adoption_preview_refuses_a_different_kind_or_completion_state(string remoteKind, bool remoteCompleted)
    {
        var taskId = Guid.NewGuid(); var remoteId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var preview = new WorkspaceProjectionPreview(_id, Guid.NewGuid(), new(taskId, 1, "Active", CompanionTaskKind.Todo, CompanionTaskState.Active, CompanionPriority.Normal, now, now),
            new(remoteId, remoteKind, "Remote", "", null, 1, "str", null, [], remoteCompleted, true, true, null, new string('a', 64)), 2, 0);
        var vault = Vault(out var vaultHttp); using var ownedVault = vaultHttp;
        using var http = new HttpClient(new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/connect" ? Connection() : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(WorkspaceTaskApiJson.Serialize(preview)) })));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        Assert.False((await client.PreviewLinkAsync(taskId, remoteId, default)).Succeeded);
    }
}
