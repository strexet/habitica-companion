using System.Text.Json;
using Habitica.Application.Notifications;
namespace Habitica.Application.Tests.Workspace;
public sealed class ReminderIdentityTests
{
    [Fact]
    public void Three_independent_Worker_hashes_match_exact_UTC_cancel_and_civil_repeat_including_plus_timezone()
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","reminder-command-v1.json")));
        foreach(var row in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var intent=JsonSerializer.Deserialize<ReminderIntent>(row.GetProperty("intent"),ReminderIdentity.Json)!;Assert.True(ReminderIdentity.Valid(intent));
            Assert.Equal(row.GetProperty("hash").GetString(),ReminderIdentity.Hash(new(Guid.NewGuid(),row.GetProperty("expectedWorkspaceRevision").GetInt64(),intent)));
        }
    }
}
