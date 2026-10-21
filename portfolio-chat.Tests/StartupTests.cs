using System.Net;

namespace portfolio_chat.Tests;

public class StartupTests {
    [Fact]
    public async Task StartsAndDisablesChatWhenValkeyIsUnavailable() {
        await using var factory = new ChatWebApplicationFactory(
            "localhost:1,connectTimeout=250,connectRetry=0",
            overrideRedis: false);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/ws/chat/negotiate?negotiateVersion=1",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
