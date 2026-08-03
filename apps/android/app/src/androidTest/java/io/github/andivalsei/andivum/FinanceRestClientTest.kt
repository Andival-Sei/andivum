package io.github.andivalsei.andivum

import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.net.HttpURLConnection
import java.net.URL
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class FinanceRestClientTest {
    @Test
    fun createTransaction_uses_authenticated_rpc_and_never_puts_publishable_key_in_body() {
        val connection = FakeHttpURLConnection(
            URL("https://example.supabase.co/rest/v1/rpc/finance_create_transaction"),
            responseBody = "{\"duplicate\":false,\"transaction_id\":\"txn-1\"}",
        )
        val client = FinanceClient(
            supabaseUrl = "https://example.supabase.co",
            publishableKey = "publishable-key",
            accessTokenProvider = { "test-access-token" },
            openConnection = { connection },
        )
        val draft = FinanceDraft(
            FinanceTransactionType.EXPENSE,
            "Покупки",
            "2026-08-03",
            "RUB",
            100,
            listOf(FinanceDraftItem("Молоко", 1.0, 100, "food.dairy")),
        )

        val result = client.createTransaction(draft, "account-1")

        assertFalse(result.isDuplicate)
        assertEquals("txn-1", result.transactionId)
        assertEquals("Bearer test-access-token", connection.getRequestProperty("Authorization"))
        assertEquals("POST", connection.requestMethod)
        assertTrue(connection.requestedBody.contains("\"total_minor\":100"))
        assertFalse(connection.requestedBody.contains("publishable-key"))
    }

    private class FakeHttpURLConnection(url: URL, private val responseBody: String) : HttpURLConnection(url) {
        private val requestBytes = ByteArrayOutputStream()

        val requestedBody: String
            get() = requestBytes.toString(Charsets.UTF_8.name())

        override fun connect() = Unit
        override fun disconnect() = Unit
        override fun usingProxy(): Boolean = false
        override fun getResponseCode(): Int = HTTP_OK
        override fun getInputStream() = ByteArrayInputStream(responseBody.toByteArray())
        override fun getOutputStream() = requestBytes
    }
}
