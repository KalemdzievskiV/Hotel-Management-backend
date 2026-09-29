using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using HotelManagement.Services.Push;
using Microsoft.Extensions.Options;
using Xunit;

namespace HotelManagement.Tests.Services;

public class ExpoPushClientTests
{
    private class StubHandler : HttpMessageHandler
    {
        private readonly string _response;
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        public StubHandler(string response) => _response = response;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            };
        }
    }

    private static PushMessage Message(string token) =>
        new(token, "Title", "Body", new Dictionary<string, object?> { ["reservationId"] = 7 });

    [Fact]
    public async Task SendAsync_ReturnsTheTokensExpoNoLongerKnows()
    {
        var handler = new StubHandler("""
            {"data":[
              {"status":"ok","id":"a"},
              {"status":"error","message":"not registered","details":{"error":"DeviceNotRegistered"}},
              {"status":"error","message":"too big","details":{"error":"MessageTooBig"}}
            ]}
            """);
        var client = new ExpoPushClient(new HttpClient(handler), Options.Create(new PushOptions()));

        var gone = await client.SendAsync([Message("T1"), Message("T2"), Message("T3")], CancellationToken.None);

        gone.Should().Equal("T2");
        handler.Request!.Headers.Authorization.Should().BeNull();

        using var sent = JsonDocument.Parse(handler.RequestBody!);
        var first = sent.RootElement[0];
        first.GetProperty("to").GetString().Should().Be("T1");
        first.GetProperty("channelId").GetString().Should().Be(ExpoPushClient.AndroidChannelId);
        first.GetProperty("data").GetProperty("reservationId").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task SendAsync_SendsTheAccessTokenWhenConfigured()
    {
        var handler = new StubHandler("""{"data":[{"status":"ok","id":"a"}]}""");
        var client = new ExpoPushClient(new HttpClient(handler), Options.Create(new PushOptions { ExpoAccessToken = "secret" }));

        await client.SendAsync([Message("T1")], CancellationToken.None);

        handler.Request!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Request.Headers.Authorization.Parameter.Should().Be("secret");
    }
}
