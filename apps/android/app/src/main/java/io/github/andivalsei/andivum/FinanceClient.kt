package io.github.andivalsei.andivum

import java.net.HttpURLConnection
import java.net.URL
import org.json.JSONArray
import org.json.JSONObject

data class FinanceCreateResult(
    val isDuplicate: Boolean,
    val transactionId: String?,
)

class FinanceApiException(
    val statusCode: Int,
    message: String,
) : IllegalStateException(message)

class FinanceClient(
    private val supabaseUrl: String,
    private val publishableKey: String,
    private val accessTokenProvider: () -> String?,
    private val openConnection: (URL) -> HttpURLConnection = { url ->
        url.openConnection() as HttpURLConnection
    },
) {
    fun getCategories(): List<FinanceCategory> {
        val response = request(
            endpoint = "${restBaseUrl()}/finance_categories?select=id,slug,name_ru,name_en,category_type,parent_id&order=slug.asc",
            method = "GET",
        )
        ensureSuccess(response)
        val rows = JSONArray(response.body)
        return buildList(rows.length()) {
            repeat(rows.length()) { index ->
                val row = rows.getJSONObject(index)
                add(
                    FinanceCategory(
                        id = row.optString("id"),
                        slug = row.optString("slug"),
                        name = row.optString("name_ru").ifBlank { row.optString("name_en") },
                        type = row.optString("category_type"),
                        parentId = row.optString("parent_id").takeIf { it.isNotBlank() },
                    ),
                )
            }
        }
    }

    fun getAccounts(): List<FinanceAccount> {
        val response = request(
            endpoint = "${restBaseUrl()}/finance_accounts?select=id,name,account_type,currency&archived_at=is.null&order=created_at.asc",
            method = "GET",
        )
        ensureSuccess(response)
        val rows = JSONArray(response.body)
        return buildList(rows.length()) {
            repeat(rows.length()) { index ->
                val row = rows.getJSONObject(index)
                add(
                    FinanceAccount(
                        id = row.optString("id"),
                        name = row.optString("name"),
                        accountType = row.optString("account_type"),
                        currency = row.optString("currency"),
                    ),
                )
            }
        }
    }

    fun createAccount(name: String, currency: String = "RUB"): FinanceAccount {
        val body = JSONObject()
            .put("name", name.trim())
            .put("account_type", "cash")
            .put("currency", currency.uppercase())
        val response = request(
            endpoint = "${restBaseUrl()}/finance_accounts?select=id,name,account_type,currency",
            method = "POST",
            body = body.toString(),
            preferRepresentation = true,
        )
        ensureSuccess(response)
        val row = JSONArray(response.body).optJSONObject(0)
            ?: throw IllegalStateException("Supabase did not return the created finance account.")
        return FinanceAccount(
            id = row.optString("id"),
            name = row.optString("name"),
            accountType = row.optString("account_type"),
            currency = row.optString("currency"),
        )
    }

    fun getTransactions(): List<FinanceTransaction> {
        val response = request(
            endpoint = "${restBaseUrl()}/finance_transactions?select=id,title,transaction_type,occurred_on,currency,total_minor,source,finance_transaction_items(name,quantity,unit_price_minor,line_total_minor,category_id,sort_order,finance_categories(slug))&order=occurred_on.desc,created_at.desc&limit=50",
            method = "GET",
        )
        ensureSuccess(response)
        val rows = JSONArray(response.body)
        return buildList(rows.length()) {
            repeat(rows.length()) { index ->
                val row = rows.getJSONObject(index)
                val itemRows = row.optJSONArray("finance_transaction_items")
                val items = buildList(itemRows?.length() ?: 0) {
                    repeat(itemRows?.length() ?: 0) { itemIndex ->
                        val item = itemRows!!.getJSONObject(itemIndex)
                        add(
                            FinanceDraftItem(
                                name = item.optString("name"),
                                quantity = item.optDouble("quantity", 1.0),
                                lineTotalMinor = item.optLong("line_total_minor"),
                                categorySlug = item.optJSONObject("finance_categories")?.optString("slug")
                                    ?.takeIf { it.isNotBlank() }
                                    ?: item.optString("category_id"),
                            ),
                        )
                    }
                }
                add(
                    FinanceTransaction(
                        id = row.optString("id"),
                        title = row.optString("title"),
                        type = row.optString("transaction_type"),
                        occurredOn = row.optString("occurred_on"),
                        currency = row.optString("currency"),
                        totalMinor = row.optLong("total_minor"),
                        source = row.optString("source", "manual"),
                        items = items,
                    ),
                )
            }
        }
    }

    fun createTransaction(
        draft: FinanceDraft,
        accountId: String,
        source: String = "manual",
        importFingerprint: String? = null,
    ): FinanceCreateResult {
        val response = request(
            endpoint = "${restBaseUrl()}/rpc/finance_create_transaction",
            method = "POST",
            body = FinanceJson.createTransactionPayload(
                draft,
                accountId,
                source,
                importFingerprint,
            ).toString(),
        )
        ensureSuccess(response)
        val json = if (response.body.trimStart().startsWith("[")) {
            JSONArray(response.body).optJSONObject(0) ?: JSONObject()
        } else {
            JSONObject(response.body)
        }
        return FinanceCreateResult(
            isDuplicate = json.optBoolean("duplicate", false),
            transactionId = json.optString("transaction_id").takeIf { it.isNotBlank() },
        )
    }

    private fun restBaseUrl(): String = "${supabaseUrl.trimEnd('/')}/rest/v1"

    private fun request(
        endpoint: String,
        method: String,
        body: String? = null,
        preferRepresentation: Boolean = false,
    ): Response {
        val accessToken = accessTokenProvider()?.takeIf { it.isNotBlank() }
            ?: throw IllegalStateException("No saved Supabase session is available.")
        val connection = openConnection(URL(endpoint)).apply {
            requestMethod = method
            setRequestProperty("apikey", publishableKey)
            setRequestProperty("Authorization", "Bearer $accessToken")
            connectTimeout = 10_000
            readTimeout = 10_000
            if (body != null) {
                doOutput = true
                setRequestProperty("Content-Type", "application/json")
            }
            if (preferRepresentation) {
                setRequestProperty("Prefer", "return=representation")
            }
        }
        try {
            if (body != null) {
                connection.outputStream.use { output ->
                    output.write(body.toByteArray(Charsets.UTF_8))
                }
            }
            val statusCode = connection.responseCode
            val stream = if (statusCode in 200..299) connection.inputStream else connection.errorStream
            val responseBody = stream?.bufferedReader()?.use { it.readText() }.orEmpty()
            return Response(statusCode, responseBody)
        } finally {
            connection.disconnect()
        }
    }

    private fun ensureSuccess(response: Response) {
        if (response.statusCode !in 200..299) {
            throw FinanceApiException(
                response.statusCode,
                "Finance request failed with HTTP ${response.statusCode}.",
            )
        }
    }

    private data class Response(
        val statusCode: Int,
        val body: String,
    )
}
