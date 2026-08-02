package io.github.andivalsei.andivum

import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.net.HttpURLConnection
import java.net.URL
import java.nio.charset.StandardCharsets
import java.util.ArrayDeque
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class SupabaseProfileClientTest {
    @Test
    fun bootstraps_a_profile_with_the_auth0_id_token() {
        val connections = ConnectionQueue().apply {
            add(
                FakeHttpURLConnection(
                    URL("https://example.supabase.co/rest/v1/app_profiles"),
                    HttpURLConnection.HTTP_OK,
                    "[]",
                ),
            )
            add(
                FakeHttpURLConnection(
                    URL("https://example.supabase.co/rest/v1/app_profiles"),
                    HttpURLConnection.HTTP_CREATED,
                    "[{\"id\":\"profile-123\"}]",
                ),
            )
        }
        val client = SupabaseProfileClient(
            "https://example.supabase.co",
            "publishable-key",
        ) { connections.removeFirst() }

        val session = client.getCurrentSession("id-token")

        assertEquals("profile-123", session.userId)
        assertTrue(session.authenticated)
        val createRequest = connections.lastReturned!!
        assertEquals("publishable-key", createRequest.capturedProperties["apikey"])
        assertEquals("Bearer id-token", createRequest.capturedProperties["Authorization"])
        assertEquals("{}", createRequest.output.toString(StandardCharsets.UTF_8.name()))
    }

    private class ConnectionQueue : ArrayDeque<FakeHttpURLConnection>() {
        var lastReturned: FakeHttpURLConnection? = null

        override fun removeFirst(): FakeHttpURLConnection {
            return super.removeFirst().also { lastReturned = it }
        }
    }

    private class FakeHttpURLConnection(
        url: URL,
        private val statusCode: Int,
        private val responseBody: String,
    ) : HttpURLConnection(url) {
        val capturedProperties = mutableMapOf<String, String>()
        val output = ByteArrayOutputStream()

        override fun disconnect() = Unit

        override fun usingProxy(): Boolean = false

        override fun connect() = Unit

        override fun getResponseCode(): Int = statusCode

        override fun setRequestProperty(key: String, value: String) {
            capturedProperties[key] = value
        }

        override fun getInputStream() =
            ByteArrayInputStream(responseBody.toByteArray(StandardCharsets.UTF_8))

        override fun getOutputStream() = output
    }
}
