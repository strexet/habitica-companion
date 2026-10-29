using System.Text.Json;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Diagnostics;
namespace Habitica.Application.Tests.Diagnostics;
public sealed class DiagnosticSharingTests
{
    [Fact]
    public void Cloud_preview_is_safe_canonical_and_time_bearing_without_acquiring_browser_history()
    {
        var at=new DateTimeOffset(2026,10,29,0,0,0,TimeSpan.Zero);var id=Guid.Parse("019a2f00-0000-7000-8000-000000000001");
        var entry=new DiagnosticsLogEntry("private-log-identity",at,DiagnosticsFeatureArea.Workspace,"workspace-tag-accept",DiagnosticsSeverity.Warning,DiagnosticsMode.LiveMutation,"private-title credential-canary",new Dictionary<string,string>{{"apiToken","credential-canary"},{"requestCount","2"}},new(DiagnosticsResult.Unknown,5,Guid.Parse("00000000-0000-4000-8000-000000000011"),Guid.Parse("00000000-0000-4000-8000-000000000012"),1,0,1));
        var bundle=DiagnosticSharing.Prepare(DiagnosticsBundleExport.Create([entry],new(),at));Assert.True(DiagnosticSharing.Valid(bundle));var json=DiagnosticSharing.Serialize(bundle);Assert.DoesNotContain("credential-canary",json);Assert.DoesNotContain("private-title",json);Assert.DoesNotContain("private-log-identity",json);Assert.Equal(64,DiagnosticSharing.Hash(bundle).Length);Assert.Equal('7',DiagnosticSharing.NewRequestId(at).ToString("D")[14]);
        Assert.Equal(json,DiagnosticSharing.Serialize(JsonSerializer.Deserialize<DiagnosticSharedBundle>(json,DiagnosticSharing.Json)!));Assert.True(DiagnosticSharing.ValidRequestId(id));Assert.False(DiagnosticSharing.ValidRequestId(Guid.NewGuid()));
        Assert.False(DiagnosticSharing.Valid(bundle with {Entries=[entry]}));
        if(Environment.GetEnvironmentVariable("HABITICA_DIAGNOSTIC_FIXTURES") is {} directory){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"diagnostic-sharing-v1.json"),JsonSerializer.Serialize(new {bundle=JsonSerializer.Deserialize<JsonElement>(json),hash=DiagnosticSharing.Hash(bundle),payloadBytes=DiagnosticSharing.Bytes(bundle)},new JsonSerializerOptions{WriteIndented=true}));}
    }
}
