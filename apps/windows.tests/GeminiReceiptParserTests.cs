using System.Text;
using Andivum_Windows.Finance;
using Xunit;

namespace Andivum_Windows.Tests;

public sealed class GeminiReceiptParserTests
{
    [Fact]
    public void Builds_a_structured_request_with_allowed_category_slugs()
    {
        var request = GeminiReceiptParser.BuildRequest(
            Encoding.UTF8.GetBytes("receipt"),
            "image/jpeg",
            [new FinanceCategory("1", "food.dairy", "Молочное", "expense", "food")],
            "test-key",
            "gemini-2.5-flash");

        Assert.Equal("test-key", request.ApiKeyHeader);
        Assert.Contains("food.dairy", request.Body);
        Assert.Contains("application/json", request.Body);
        Assert.Contains("inline_data", request.Body);
        Assert.DoesNotContain("test-key", request.Body);
    }

    [Fact]
    public void Parses_only_the_model_candidate_and_keeps_the_transaction_as_a_draft()
    {
        var response = """
            {
              "candidates": [{
                "content": {"parts": [{"text": "{\"title\":\"Чек\",\"type\":\"expense\",\"occurred_on\":\"2026-08-03\",\"currency\":\"RUB\",\"total_minor\":120,\"items\":[{\"name\":\"Молоко\",\"quantity\":1,\"unit_price_minor\":120,\"line_total_minor\":120,\"category_slug\":\"food.dairy\"}]}"}]}
              }]
            }
            """;

        var draft = GeminiReceiptParser.ParseResponse(
            response,
            [new FinanceCategory("1", "food.dairy", "Молочное", "expense", "food")]);

        Assert.Equal("Чек", draft.Title);
        Assert.Equal(120, draft.TotalMinor);
        Assert.Equal("food.dairy", draft.Items[0].CategorySlug);
    }
}
