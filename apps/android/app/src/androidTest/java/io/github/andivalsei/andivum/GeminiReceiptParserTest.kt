package io.github.andivalsei.andivum

import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class GeminiReceiptParserTest {
    @Test
    fun buildRequest_uses_structured_json_and_allowed_categories() {
        val request = GeminiReceiptParser.buildRequest(
            bytes = "receipt".toByteArray(),
            mimeType = "image/jpeg",
            categories = listOf(FinanceCategory("1", "food.dairy", "Молочное", "expense", "food")),
            apiKey = "test-key",
        )

        assertEquals("test-key", request.apiKeyHeader)
        assertTrue(request.body.contains("food.dairy"))
        assertTrue(request.body.contains("application/json"))
        assertFalse(request.body.contains("test-key"))
    }

    @Test
    fun parseResponse_returns_a_draft_and_does_not_save_it() {
        val extracted = JSONObject()
            .put("title", "Чек")
            .put("type", "expense")
            .put("occurred_on", "2026-08-03")
            .put("currency", "RUB")
            .put("total_minor", 120)
            .put(
                "items",
                JSONArray().put(
                    JSONObject()
                        .put("name", "Молоко")
                        .put("quantity", 1)
                        .put("line_total_minor", 120)
                        .put("category_slug", "food.dairy"),
                ),
            )
        val response = JSONObject()
            .put(
                "candidates",
                JSONArray().put(
                    JSONObject().put(
                        "content",
                        JSONObject().put(
                            "parts",
                            JSONArray().put(JSONObject().put("text", extracted.toString())),
                        ),
                    ),
                ),
            )

        val draft = GeminiReceiptParser.parseResponse(
            response.toString(),
            listOf(FinanceCategory("1", "food.dairy", "Молочное", "expense", "food")),
        )

        assertEquals("Чек", draft.title)
        assertEquals(120L, draft.totalMinor)
        assertEquals("food.dairy", draft.items.first().categorySlug)
    }
}
