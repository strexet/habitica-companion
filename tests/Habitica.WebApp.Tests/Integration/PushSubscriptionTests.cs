using Bunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Application.Notifications;
using Habitica.Domain.Auth;
using Habitica.WebApp.Integration;
using Microsoft.JSInterop;

namespace Habitica.WebApp.Tests.Integration;

public sealed class PushSubscriptionTests
{
    private const string Profile = "prf_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Token = "mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm";
    private static readonly Guid Connection = Guid.NewGuid();
    private static readonly PushContext Context = new(Profile, Connection);
    private static readonly PushMaterial Material = new("https://web.push.apple.com/private-endpoint", new(new string('p', 87), new string('a', 22)), null);
    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = JsonContent.Create(value) };
    private static PushReceipt Receipt(PushSubscribeCommand c) => new(Profile, Connection, c.RequestId, "subscribe", true, null, null, c.DeviceId, Guid.NewGuid(), c.ExpectedRevision + 1, "active");
    private static PushStatus Status => new(Profile, Connection, true, "web-push-v1", null, false, new string('k',87), []);
    private static PushSubscribeCommand Command => new(Context, Guid.NewGuid(), Guid.NewGuid(), 0, Guid.NewGuid(), Material);
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => request.RequestUri!.AbsolutePath == "/enroll"
            ? Task.FromResult(Json(new { profileId = Profile, managementToken = Token }, HttpStatusCode.Created)) : respond(request, cancellationToken);
    }
    private static async Task<ChatGptIntegrationClient> Client(HttpClient http)
    {
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/", false);
        Assert.True((await client.RevalidateAsync(new HabiticaCredentials("fixture", "credential"), default)).Succeeded);
        return client;
    }

    [Fact]
    public async Task Management_transport_uploads_only_explicit_subscription_and_preserves_original_request_identity()
    {
        var c=Command;var calls=0;var receipt=Receipt(c);
        using var http=new HttpClient(new Handler(async(request,ct)=>
        {
            calls++;Assert.Equal(Token,request.Headers.Authorization!.Parameter);Assert.Equal("auth.invalid",request.RequestUri!.Host);
            Assert.DoesNotContain(Material.Endpoint,request.RequestUri.ToString());
            if(request.Method==HttpMethod.Get){Assert.EndsWith("/push/commands/"+c.RequestId.ToString("D"),request.RequestUri.AbsolutePath);return Json(new{profileId=Profile,requestId=c.RequestId,receipt});}
            using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));var root=json.RootElement;
            Assert.Equal(c.RequestId,root.GetProperty("requestId").GetGuid());Assert.Equal(Connection,root.GetProperty("connectionId").GetGuid());Assert.True(root.GetProperty("userVisibleOnly").GetBoolean());
            Assert.Equal(Material.Endpoint,root.GetProperty("subscription").GetProperty("endpoint").GetString());Assert.False(root.TryGetProperty("managementToken",out _));return Json(receipt);
        }));
        var client=await Client(http);var result=await client.SubscribePushAsync(c,default);Assert.True(result.Succeeded);Assert.Equal(receipt,result.Value);Assert.Equal(receipt,(await client.RecoverPushReceiptAsync(c.RequestId,default)).Value);Assert.Equal(2,calls);
        Assert.DoesNotContain(Material.Endpoint,JsonSerializer.Serialize(result));Assert.DoesNotContain(Material.Endpoint,c.ToString());Assert.DoesNotContain(Material.Keys.Auth,Material.Keys.ToString());
    }

    [Theory]
    [InlineData("account")]
    [InlineData("device")]
    [InlineData("connection")]
    [InlineData("request")]
    [InlineData("revision")]
    public async Task Mismatched_receipt_is_not_exposed_as_success(string mismatch)
    {
        var c=Command;var receipt=Receipt(c);receipt=mismatch switch{"account"=>receipt with{ProfileId="prf_"+new string('b',43)},"device"=>receipt with{DeviceId=Guid.NewGuid()},"connection"=>receipt with{ConnectionId=Guid.NewGuid()},"request"=>receipt with{RequestId=Guid.NewGuid()},_=>receipt with{Revision=2}};
        using var http=new HttpClient(new Handler((_,_)=>Task.FromResult(Json(receipt))));var client=await Client(http);var result=await client.SubscribePushAsync(c,default);Assert.False(result.Succeeded);Assert.Null(result.Value);
    }

    [Fact]
    public async Task Unknown_or_secret_bearing_status_and_missing_required_fields_are_rejected()
    {
        object response=new{profileId=Profile,connectionId=Connection,available=false,consentVersion="web-push-v1",consentEpoch=(Guid?)null,enabled=false,applicationServerKey=(string?)null,devices=Array.Empty<PushDevice>(),endpoint=Material.Endpoint};
        using var http=new HttpClient(new Handler((_,_)=>Task.FromResult(Json(response))));var client=await Client(http);
        Assert.False((await client.ReadPushStatusAsync(default)).Succeeded);
        response=new{profileId=Profile,connectionId=Connection,consentVersion="web-push-v1",devices=Array.Empty<PushDevice>()};Assert.False((await client.ReadPushStatusAsync(default)).Succeeded);
        response=Status;Assert.True((await client.ReadPushStatusAsync(default)).Succeeded);
    }

    [Fact]
    public async Task Stale_session_discards_late_status_and_write_receipt()
    {
        foreach(var write in new[]{false,true})
        {
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var c=Command;
            using var http=new HttpClient(new Handler(async(_,_)=>{entered.SetResult();await release.Task;return write?Json(Receipt(c)):Json(Status);}));var client=await Client(http);
            Task<bool> Pending()=>write?Check(client.SubscribePushAsync(c,default)):Check(client.ReadPushStatusAsync(default));
            var pending=Pending();await entered.Task;client.ForgetSession();release.SetResult();Assert.False(await pending);
        }
        static async Task<bool> Check<T>(Task<PushResult<T>> task)=>(await task).Succeeded;
    }

    [Fact]
    public async Task Uncertain_transport_is_not_retried_and_absent_receipt_is_not_failed_effect_evidence()
    {
        var calls=0;
        using var http=new HttpClient(new Handler((request,_)=>{calls++;return request.Method==HttpMethod.Post?throw new HttpRequestException(Material.Endpoint):Task.FromResult(Json(new{profileId=Profile,requestId=CurrentRequest,receipt=(PushReceipt?)null}));}));
        var client=await Client(http);var c=Command;CurrentRequest=c.RequestId;var result=await client.SubscribePushAsync(c,default);Assert.False(result.Succeeded);Assert.DoesNotContain(Material.Endpoint,result.Message);Assert.Equal(1,calls);
        var recovery=await client.RecoverPushReceiptAsync(c.RequestId,default);Assert.True(recovery.Succeeded);Assert.Null(recovery.Value);Assert.Contains("do not infer",recovery.Message);Assert.Equal(2,calls);
    }
    private Guid CurrentRequest;

    [Fact]
    public async Task Cancellation_propagates_without_replay_and_cross_account_intent_sends_nothing()
    {
        var calls=0;using var http=new HttpClient(new Handler((_,ct)=>{calls++;throw new OperationCanceledException(ct);}));var client=await Client(http);
        var c=Command;Assert.False((await client.SubscribePushAsync(c with{Context=new("other",Connection)},default)).Succeeded);Assert.Equal(0,calls);
        using var source=new CancellationTokenSource();source.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>client.SubscribePushAsync(c,source.Token));Assert.InRange(calls,0,1);
    }

    [Fact]
    public async Task Consent_and_unsubscribe_receipts_match_reviewed_context_and_exact_revision()
    {
        var consent=new PushConsentCommand(Context,Guid.NewGuid(),null,true);var epoch=Guid.NewGuid();var device=Guid.NewGuid();var subscription=Guid.NewGuid();var unsubscribe=new PushUnsubscribeCommand(Context,Guid.NewGuid(),device,1,epoch,subscription);
        using var http=new HttpClient(new Handler((request,_)=>Task.FromResult(Json(request.RequestUri!.AbsolutePath.EndsWith("consent")
            ?new PushReceipt(Profile,Connection,consent.RequestId,"consent",true,epoch,true,null,null,null,null)
            :new PushReceipt(Profile,Connection,unsubscribe.RequestId,"unsubscribe",true,null,null,device,subscription,2,"unsubscribed")))));
        var client=await Client(http);Assert.True((await client.SetPushConsentAsync(consent,default)).Succeeded);Assert.True((await client.UnsubscribePushAsync(unsubscribe,default)).Succeeded);
    }
}

public sealed class PwaPushBrowserTests : BunitContext
{
    [Fact]
    public async Task Preparing_browser_only_reads_capability_and_click_path_never_imports_a_module()
    {
        var module=JSInterop.SetupModule("./js/pwaPush.js");module.Setup<BrowserPushCapability>("capability").SetResult(new("available","default"));module.Setup<BrowserPushResult>("subscribe",_=>true).SetResult(new("denied"));
        await using var browser=new PwaPushBrowser(JSInterop.JSRuntime);
        Assert.Equal("unprepared",(await browser.SubscribeAsync("fixture",true,default)).State);Assert.Empty(JSInterop.Invocations);
        Assert.Equal("available",(await browser.PrepareAsync(default)).State);Assert.Single(module.Invocations);Assert.Equal("capability",module.Invocations.Single().Identifier);
        Assert.Equal("denied",(await browser.SubscribeAsync("fixture",true,default)).State);Assert.Equal(2,module.Invocations.Count);Assert.Single(JSInterop.Invocations, invocation => invocation.Identifier == "import");
    }
}
