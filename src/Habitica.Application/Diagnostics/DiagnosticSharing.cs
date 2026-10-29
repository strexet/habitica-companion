using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Domain.Diagnostics;
namespace Habitica.Application.Diagnostics;

public sealed record DiagnosticShareFilters(string? Feature,string? Severity,string? Mode);
public sealed record DiagnosticSharedBundle(int Version,DateTimeOffset GeneratedAtUtc,string Source,DiagnosticShareFilters Filters,int AvailableCount,int MatchingCount,int OmittedMatchingCount,IReadOnlyList<DiagnosticsLogEntry> Entries);
public sealed record DiagnosticSharingLimits(int EventRows,int ChargedBytes,int EntriesPerBundle,int RetentionDays);
public sealed record DiagnosticSharingSources(string Browser,string Server);
public sealed record DiagnosticSharingStatus(Guid WorkspaceId,string ConsentVersion,bool Enabled,Guid? ConsentEpoch,long Revision,bool CleanupPending,int EventCount,int ChargedBytes,DiagnosticSharingLimits Limits,DiagnosticSharingSources Sources,IReadOnlyList<string> Omitted);
public sealed record DiagnosticUploadReceipt(Guid WorkspaceId,Guid UploadId,Guid RequestId,string BundleHash,int EventCount,int PayloadBytes,DateTimeOffset AcceptedAtUtc,DateTimeOffset ExpiresAtUtc,bool Saved);
public sealed record DiagnosticUploadView(Guid WorkspaceId,Guid RequestId,bool? Saved,bool OutcomeUnknown,Guid? UploadId=null,string? BundleHash=null,string? State=null,int EventCount=0,int PayloadBytes=0,DateTimeOffset CreatedAtUtc=default,DateTimeOffset AcceptByUtc=default,DateTimeOffset ExpiresAtUtc=default,DiagnosticUploadReceipt? Receipt=null,bool? Unavailable=null);
public sealed record DiagnosticReadBundle(Guid WorkspaceId,Guid RequestId,bool? Saved,bool OutcomeUnknown,Guid? UploadId,string? BundleHash,string? State,int EventCount,int PayloadBytes,DateTimeOffset CreatedAtUtc,DateTimeOffset AcceptByUtc,DateTimeOffset ExpiresAtUtc,DiagnosticUploadReceipt? Receipt,DiagnosticSharedBundle Bundle,string Source,bool BrowserTimesTrusted,bool CompleteDeviceHistory);
public sealed record DiagnosticReceiptSummary(Guid CommandId,string Operation,bool LiveVerification,string? RecordedProjectionState=null,int? RecordedConfirmedCount=null,int? RecordedRequestedCount=null,int? GameEffects=null,bool? SavedInCompanion=null,bool? OutcomeUnknown=null);
public sealed record DiagnosticReadItem(string Id,string Source,DateTimeOffset OccurredAtUtc,DateTimeOffset? ReceivedAtUtc,string TimeBasis,DiagnosticsLogEntry? Entry,DiagnosticReceiptSummary? Receipt,Guid? UploadId);
public sealed record DiagnosticReadCoverage(string BrowserSource,string ServerSource,int OmittedServerReceiptLowerBound,int ServerPerKindLimit,IReadOnlyList<string> Unavailable);
public sealed record DiagnosticReadPage(Guid WorkspaceId,long Revision,long WorkspaceVersion,DateTimeOffset AsOfUtc,DateTimeOffset ValidUntilUtc,IReadOnlyList<DiagnosticReadItem> Items,int MatchedCount,int? NextOffset,bool CleanupPending,DiagnosticReadCoverage Coverage);
public sealed record DiagnosticReadQuery(DateTimeOffset FromUtc,DateTimeOffset ToUtc,string? Feature=null,string? Operation=null,string? Severity=null,Guid? CommandId=null,Guid? CorrelationId=null,string? Source=null,int Offset=0,long? ExpectedRevision=null,long? ExpectedWorkspaceVersion=null,DateTimeOffset? AsOfUtc=null,DateTimeOffset? ValidUntilUtc=null);
public sealed record DiagnosticCloudResult<T>(bool Succeeded,T? Value,string Code,bool OutcomeUnknown=false);
public interface IDiagnosticSharingApi
{
    bool DiagnosticSharingAvailable {get;}
    Guid? WorkspaceId {get;}
    string DiagnosticIdentity=>WorkspaceId?.ToString("D")??"unavailable";
    Task<DiagnosticCloudResult<DiagnosticSharingStatus>> ReadDiagnosticSharingAsync(CancellationToken token);
    Task<DiagnosticCloudResult<DiagnosticSharingStatus>> SetDiagnosticSharingAsync(bool enabled,CancellationToken token);
    Task<DiagnosticCloudResult<DiagnosticUploadView>> PrepareDiagnosticUploadAsync(Guid requestId,DiagnosticSharedBundle bundle,CancellationToken token);
    Task<DiagnosticCloudResult<DiagnosticUploadView>> AcceptDiagnosticUploadAsync(DiagnosticUploadView review,DiagnosticSharedBundle bundle,CancellationToken token);
    Task<DiagnosticCloudResult<DiagnosticUploadView>> RecoverDiagnosticUploadAsync(Guid requestId,CancellationToken token);
    Task<DiagnosticCloudResult<DiagnosticReadBundle>> ReadDiagnosticBundleAsync(Guid uploadId,CancellationToken token);
    Task<DiagnosticCloudResult<DiagnosticReadPage>> QueryDiagnosticsAsync(DiagnosticReadQuery query,CancellationToken token);
}
public static class DiagnosticSharing
{
    public const int MaxBytes=1024*1024,MaxEvents=2000,MaxBundleEntries=100;
    public static JsonSerializerOptions Json {get;}=new(JsonSerializerDefaults.Web){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Converters={new UtcMilliseconds(),new JsonStringEnumConverter()}};
    public static Guid NewRequestId(DateTimeOffset utc)
    {
        var bytes=RandomNumberGenerator.GetBytes(16);var time=utc.ToUnixTimeMilliseconds();for(var i=5;i>=0;i--){bytes[i]=(byte)(time&255);time>>=8;}bytes[6]=(byte)((bytes[6]&15)|0x70);bytes[8]=(byte)((bytes[8]&63)|0x80);var hex=Convert.ToHexString(bytes);return Guid.ParseExact(hex,"N");
    }
    public static bool ValidRequestId(Guid id)=>id!=Guid.Empty&&id.ToString("D")[14]=='7';
    public static DiagnosticSharedBundle Prepare(DiagnosticsBundle bundle)=>new(1,Milliseconds(bundle.GeneratedAtUtc),"browser-local-store",new(bundle.Filters.Feature?.ToString(),bundle.Filters.Severity?.ToString(),bundle.Filters.Mode?.ToString()),bundle.AvailableCount,bundle.MatchingCount,bundle.OmittedMatchingCount,bundle.Entries.Select(entry=>Canonical(DiagnosticsLogSanitizer.Sanitize(entry))).ToArray());
    private static DiagnosticsLogEntry Canonical(DiagnosticsLogEntry entry)=>entry with {OccurredAtUtc=Milliseconds(entry.OccurredAtUtc),Metadata=new SortedDictionary<string,string>(entry.Metadata.ToDictionary(p=>p.Key,p=>p.Value),StringComparer.Ordinal)};
    private static DateTimeOffset Milliseconds(DateTimeOffset value)=>DateTimeOffset.FromUnixTimeMilliseconds(value.ToUnixTimeMilliseconds());
    public static string Serialize(DiagnosticSharedBundle bundle)=>JsonSerializer.Serialize(bundle,Json);
    public static string Hash(DiagnosticSharedBundle bundle)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(bundle)))).ToLowerInvariant();
    public static int Bytes(DiagnosticSharedBundle bundle)=>Encoding.UTF8.GetByteCount(Serialize(bundle));
    public static bool Valid(DiagnosticSharedBundle b)=>b.Version==1&&b.Source=="browser-local-store"&&b.GeneratedAtUtc.Offset==TimeSpan.Zero&&b.Entries is {Count:<=MaxBundleEntries}&&b.Filters is not null
        &&Choice<DiagnosticsFeatureArea>(b.Filters.Feature)&&Choice<DiagnosticsSeverity>(b.Filters.Severity)&&Choice<DiagnosticsMode>(b.Filters.Mode)
        &&b.AvailableCount>=b.MatchingCount&&b.MatchingCount>=b.Entries.Count&&b.MatchingCount-b.Entries.Count==b.OmittedMatchingCount&&b.AvailableCount<=1_000_000_000&&Bytes(b)<=MaxBytes
        &&b.Entries.Select(e=>e.Id).Distinct(StringComparer.Ordinal).Count()==b.Entries.Count
        &&b.Entries.All(e=>e is not null&&e.Context?.DurationMs is not <0 and not >3600000&&JsonSerializer.Serialize(e,Json)==JsonSerializer.Serialize(Canonical(DiagnosticsLogSanitizer.Sanitize(e)),Json));
    private static bool Choice<T>(string? name) where T:struct,Enum=>name is null||Enum.TryParse<T>(name,out var value)&&Enum.IsDefined(value)&&value.ToString()==name;
    private sealed class UtcMilliseconds:JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
        {var text=reader.GetString();if(!DateTimeOffset.TryParseExact(text,"yyyy-MM-dd'T'HH:mm:ss.fff'Z'",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out var value))throw new JsonException("Invalid diagnostic UTC value.");return value;}
        public override void Write(Utf8JsonWriter writer,DateTimeOffset value,JsonSerializerOptions options)=>writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'",CultureInfo.InvariantCulture));
    }
}
