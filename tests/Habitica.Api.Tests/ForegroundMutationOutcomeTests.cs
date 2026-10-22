using System.Net;
using Habitica.Domain.Auth;
namespace Habitica.Api.Tests;

public sealed partial class AdaptiveRequestPacerTests
{
    [Theory]
    [InlineData("transport")] [InlineData("malformed")] [InlineData("false-success")]
    public async Task Foreground_spell_lost_or_malformed_acknowledgement_is_unknown_and_never_replayed(string mode)
    {
        var sends=0;using var http=new HttpClient(new Handler((_,_)=>{sends++;if(mode=="transport")throw new HttpRequestException("Private synthetic transport details");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(mode=="malformed"?"{bad":"{\"success\":false,\"data\":{}}")});})){BaseAddress=new("https://synthetic.invalid/api/v3/")};var client=new HabiticaApiClient(http,new("synthetic",MinRequestSpacingMilliseconds:0));
        var error=await Assert.ThrowsAsync<HabiticaMutationException>(()=>client.CastSpellAsync(new("synthetic-user","synthetic-secret"),"fireball","synthetic-task",default));Assert.True(error.RequestDispatched);Assert.Equal(1,sends);Assert.DoesNotContain("Private",error.Message);Assert.DoesNotContain("synthetic-secret",error.Message);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Foreground_spell_cancellation_distinguishes_queued_work_from_dispatched_unknown(bool dispatched)
    {
        var sends=0;using var cancel=new CancellationTokenSource();using var http=new HttpClient(new Handler((_,_)=>{sends++;cancel.Cancel();throw new OperationCanceledException(cancel.Token);})){BaseAddress=new("https://synthetic.invalid/api/v3/")};var client=new HabiticaApiClient(http,new("synthetic",MinRequestSpacingMilliseconds:0));if(!dispatched)cancel.Cancel();
        var error=await Assert.ThrowsAsync<HabiticaMutationCanceledException>(()=>client.CastSpellAsync(new("synthetic-user","synthetic-secret"),"fireball","synthetic-task",cancel.Token));Assert.Equal(dispatched,error.RequestDispatched);Assert.Equal(dispatched?1:0,sends);
    }
    [Fact]
    public async Task Foreground_spell_cooldown_delays_concurrent_read_without_replaying_failed_cast()
    {
        var clock=new AdvancingClock();var casts=0;var requests=new List<DateTimeOffset>();using var http=new HttpClient(new Handler((request,_)=>{requests.Add(clock.GetUtcNow());if(request.Method==HttpMethod.Post){casts++;var limited=new HttpResponseMessage(HttpStatusCode.TooManyRequests){Content=new StringContent("{}")};limited.Headers.RetryAfter=new(TimeSpan.FromSeconds(60));return Task.FromResult(limited);}return Task.FromResult(Success());})){BaseAddress=new("https://synthetic.invalid/api/v3/")};var client=new HabiticaApiClient(http,new("synthetic",MinRequestSpacingMilliseconds:0),clock);var credentials=new HabiticaCredentials("synthetic-user","synthetic-secret");
        var cast=client.CastSpellAsync(credentials,"fireball","synthetic-task",default);var read=client.GetUserAsync(credentials,default);var error=await Assert.ThrowsAsync<HabiticaApiException>(()=>cast);Assert.True(error.IsDefinitiveRefusal);await read;Assert.Equal(1,casts);Assert.Equal(2,requests.Count);Assert.True(requests[1]-requests[0]>=TimeSpan.FromSeconds(60));
    }
    [Fact]
    public async Task Foreground_server_error_remains_unknown_rather_than_a_definitive_refusal()
    {
        using var http=new HttpClient(new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway){Content=new StringContent("{}") }))){BaseAddress=new("https://synthetic.invalid/api/v3/")};var client=new HabiticaApiClient(http,new("synthetic",MinRequestSpacingMilliseconds:0));var error=await Assert.ThrowsAsync<HabiticaApiException>(()=>client.CastSpellAsync(new("synthetic-user","synthetic-secret"),"fireball",null,default));Assert.False(error.IsDefinitiveRefusal);
    }
}
