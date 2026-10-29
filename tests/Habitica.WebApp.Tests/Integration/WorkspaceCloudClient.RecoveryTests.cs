using System.Net;
using System.Text;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed partial class WorkspaceCloudClientTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task Recovery_previews_bind_exact_target_and_receipts_reject_game_effects(bool unarchive, bool tamper)
    {
        var vault = Vault(out var vaultHttp); using var ownedVaultHttp = vaultHttp;
        var operation = unarchive ? WorkspaceOperation.UnarchiveTask : WorkspaceOperation.CorrectOccurrence;
        var intent = new WorkspaceCommand(Guid.NewGuid(), _id, 10, operation, Guid.NewGuid(), unarchive
            ? new(Unarchive: new(9)) : new(CorrectOccurrence: new(Guid.NewGuid(), 9, Guid.NewGuid())));
        var command = intent with { Id = Guid.NewGuid() };
        if (tamper) command = command with { Patch = unarchive ? new(Unarchive: new(8)) : new(CorrectOccurrence: command.Patch!.CorrectOccurrence! with { TargetCommandId = Guid.NewGuid() }) };
        var correlation = Guid.NewGuid();
        var preview = new WorkspaceCommandPreview(command.Id, correlation, DateTimeOffset.UtcNow.AddMinutes(5), false, command, false, false);
        var receipt = new WorkspaceMutationReceipt(unarchive ? "unarchiveTask" : "correctOccurrence", command.TaskId, command.Id, correlation, "Synthetic", true, "notRequested", 1, 0, 0, null, "none", null, true, false);
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Connection();
            using var json = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var tool = json.RootElement.GetProperty("tool").GetString();
            if (tool == "prepare_task_command") return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(WorkspaceTaskApiJson.Serialize(preview), Encoding.UTF8, "application/json") };
            Assert.Equal(unarchive ? "unarchive_task" : "correct_occurrence", tool);
            Assert.Equal(command.Id, json.RootElement.GetProperty("arguments").GetProperty("commandId").GetGuid());
            return Json(receipt);
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        var prepared = await client.PrepareCommandAsync(Guid.NewGuid(), intent, default); Assert.Equal(!tamper, prepared.Succeeded);
        if (tamper) return;
        Assert.True((await client.AcceptCommandAsync(prepared.Value!, default)).Succeeded);
        receipt = receipt with { AcceptedEventCount = 0 }; Assert.False((await client.AcceptCommandAsync(prepared.Value!, default)).Succeeded);
        receipt = receipt with { AcceptedEventCount = 1, TotalProjectionCount = 1, HabiticaProjectionStatus = "pending" };
        Assert.False((await client.AcceptCommandAsync(prepared.Value!, default)).Succeeded);
    }
}
