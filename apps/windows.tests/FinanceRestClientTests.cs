using System.Net;
using System.Text;
using Andivum_Windows.Auth;
using Andivum_Windows.Finance;
using Xunit;

namespace Andivum_Windows.Tests;

public sealed class FinanceRestClientTests
{
    [Fact]
    public async Task Sends_transaction_only_to_the_authenticated_rpc_endpoint()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new CapturingHandler(request =>
        {
            captured = request;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"duplicate\":false,\"transaction_id\":\"txn-1\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var client = new FinanceClient(
            new HttpClient(handler),
            new FakeTokenStore(),
            new Uri("https://example.supabase.co"),
            "publishable-key");
        var draft = new FinanceDraft(
            FinanceTransactionType.Expense,
            "Покупки",
            "2026-08-03",
            "RUB",
            100,
            [new FinanceDraftItem("Молоко", 1m, 100, "food.dairy")]);

        var result = await client.CreateTransactionAsync(draft, "account-1");

        Assert.False(result.IsDuplicate);
        Assert.Equal("txn-1", result.TransactionId);
        Assert.NotNull(captured);
        Assert.Equal("/rest/v1/rpc/finance_create_transaction", captured!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer test-access-token", captured.Headers.Authorization!.ToString());
        Assert.DoesNotContain("publishable-key", capturedBody);
    }

    private sealed class CapturingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public TokenSet? Read() => new(
            "test-access-token",
            "test-refresh-token",
            "Bearer",
            3600,
            null);

        public void Save(TokenSet tokenSet) { }

        public void Clear() { }
    }
}
