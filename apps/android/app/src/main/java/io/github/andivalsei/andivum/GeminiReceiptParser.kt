package io.github.andivalsei.andivum

import java.net.HttpURLConnection
import java.net.URL
import org.json.JSONArray
import org.json.JSONObject

data class GeminiRequest(
    val apiKeyHeader: String,
    val body: String,
)

object GeminiReceiptParser {
    fun buildRequest(
        bytes: ByteArray,
        mimeType: String,
        categories: Collection<FinanceCategory>,
        apiKey: String,
        model: String = "gemini-2.5-flash",
    ): GeminiRequest {
        require(bytes.isNotEmpty()) { "Document is empty." }
        require(mimeType.isNotBlank()) { "Document MIME type is required." }
        require(apiKey.isNotBlank()) { "Gemini API key is required." }

        val allowed = JSONArray()
        categories.filter { it.slug.isNotBlank() }.forEach { category ->
            allowed.put(
                JSONObject()
                    .put("slug", category.slug)
                    .put("name", category.name)
                    .put("type", category.type),
            )
        }
        val schema = JSONObject()
            .put("type", "OBJECT")
            .put(
                "properties",
                JSONObject()
                    .put("title", JSONObject().put("type", "STRING"))
                    .put("type", JSONObject().put("type", "STRING"))
                    .put("occurred_on", JSONObject().put("type", "STRING"))
                    .put("currency", JSONObject().put("type", "STRING"))
                    .put("total_minor", JSONObject().put("type", "INTEGER"))
                    .put(
                        "items",
                        JSONObject()
                            .put("type", "ARRAY")
                            .put(
                                "items",
                                JSONObject()
                                    .put("type", "OBJECT")
                                    .put(
                                        "properties",
                                        JSONObject()
                                            .put("name", JSONObject().put("type", "STRING"))
                                            .put("quantity", JSONObject().put("type", "NUMBER"))
                                            .put("unit_price_minor", JSONObject().put("type", "INTEGER"))
                                            .put("line_total_minor", JSONObject().put("type", "INTEGER"))
                                            .put("category_slug", JSONObject().put("type", "STRING")),
                                    )
                                    .put(
                                        "required",
                                        JSONArray(listOf("name", "quantity", "unit_price_minor", "line_total_minor", "category_slug")),
                                    ),
                            ),
                    ),
            )
            .put("required", JSONArray(listOf("title", "type", "occurred_on", "currency", "total_minor", "items")))
        val prompt = """
            Extract one personal-finance receipt or invoice into the JSON schema.
            Return only JSON. Never invent an amount or date. Missing values must be empty.
            Money fields are integer minor units. type is income or expense. category_slug
            must be one of the allowed slugs below. This is only an editable draft.
            Allowed categories: $allowed
        """.trimIndent()
        val body = JSONObject()
            .put(
                "contents",
                JSONArray().put(
                    JSONObject().put(
                        "parts",
                        JSONArray()
                            .put(JSONObject().put("text", prompt))
                            .put(
                                JSONObject().put(
                                    "inline_data",
                                    JSONObject()
                                        .put("mime_type", mimeType)
                                        .put("data", android.util.Base64.encodeToString(bytes, android.util.Base64.NO_WRAP)),
                                ),
                            ),
                    ),
                ),
            )
            .put(
                "generationConfig",
                JSONObject()
                    .put("responseMimeType", "application/json")
                    .put("responseSchema", schema)
                    .put("temperature", 0.1),
            )
        return GeminiRequest(apiKey, body.toString())
    }

    fun parse(
        bytes: ByteArray,
        mimeType: String,
        categories: Collection<FinanceCategory>,
        apiKey: String,
        model: String = "gemini-2.5-flash",
        openConnection: (URL) -> HttpURLConnection = { url -> url.openConnection() as HttpURLConnection },
    ): FinanceDraft {
        val request = buildRequest(bytes, mimeType, categories, apiKey, model)
        val connection = openConnection(
            URL("https://generativelanguage.googleapis.com/v1beta/models/$model:generateContent"),
        ).apply {
            requestMethod = "POST"
            setRequestProperty("Content-Type", "application/json")
            setRequestProperty("x-goog-api-key", request.apiKeyHeader)
            connectTimeout = 20_000
            readTimeout = 60_000
            doOutput = true
        }
        try {
            connection.outputStream.use { it.write(request.body.toByteArray(Charsets.UTF_8)) }
            val statusCode = connection.responseCode
            val stream = if (statusCode in 200..299) connection.inputStream else connection.errorStream
            val response = stream?.bufferedReader()?.use { it.readText() }.orEmpty()
            if (statusCode !in 200..299) {
                throw IllegalStateException("Gemini could not analyze the document.")
            }
            return parseResponse(response, categories)
        } finally {
            connection.disconnect()
        }
    }

    fun parseResponse(response: String, categories: Collection<FinanceCategory>): FinanceDraft {
        val candidate = JSONObject(response)
            .getJSONArray("candidates")
            .getJSONObject(0)
            .getJSONObject("content")
            .getJSONArray("parts")
            .getJSONObject(0)
            .getString("text")
            .trim()
        val jsonText = candidate.removeCodeFence()
        val json = JSONObject(jsonText)
        val type = json.optString("type").lowercase()
        require(type == "income" || type == "expense") { "Gemini returned an unsupported transaction type." }
        val allowed = categories
            .filter { it.type.equals(type, ignoreCase = true) }
            .map { it.slug }
            .toSet()
        val itemRows = json.optJSONArray("items")
            ?: throw IllegalStateException("Gemini returned no finance items.")
        val items = buildList(itemRows.length()) {
            repeat(itemRows.length()) { index ->
                val item = itemRows.getJSONObject(index)
                val categorySlug = item.optString("category_slug")
                require(
                    item.optString("name").isNotBlank() &&
                        item.optDouble("quantity", 0.0) > 0.0 &&
                        item.optLong("line_total_minor", -1L) >= 0L &&
                        categorySlug in allowed,
                ) { "Gemini returned an invalid finance item." }
                add(
                    FinanceDraftItem(
                        name = item.getString("name"),
                        quantity = item.getDouble("quantity"),
                        lineTotalMinor = item.getLong("line_total_minor"),
                        categorySlug = categorySlug,
                    ),
                )
            }
        }
        val totalMinor = json.getLong("total_minor")
        require(items.sumOf { it.lineTotalMinor } == totalMinor) {
            "Gemini item totals do not equal the transaction total."
        }
        return FinanceDraft(
            type = if (type == "income") FinanceTransactionType.INCOME else FinanceTransactionType.EXPENSE,
            title = json.optString("title").ifBlank { "Без названия" },
            occurredOn = json.optString("occurred_on"),
            currency = json.optString("currency").uppercase(),
            totalMinor = totalMinor,
            items = items,
        )
    }

    private fun String.removeCodeFence(): String =
        if (startsWith("```")) trim('`', ' ', 'j', 's', 'o', 'n', '\r', '\n') else this
}
