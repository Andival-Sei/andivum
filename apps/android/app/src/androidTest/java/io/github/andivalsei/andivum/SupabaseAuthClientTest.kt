package io.github.andivalsei.andivum

import java.io.ByteArrayInputStream
import java.net.HttpURLConnection
import java.net.URL
import java.nio.charset.StandardCharsets
import java.util.ArrayDeque
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SupabaseAuthClientTest {
    @Test
    fun sign_in_posts_credentials_and_saves_the_session() {
        val connections = ConnectionQueue().apply {
            add(
                FakeHttpURLConnection(
                    URL("https://example.supabase.co/auth/v1/token?grant_type=password"),
                    HttpURLConnection.HTTP_OK,
                    "{\"access_token\":\"access\",\"refresh_token\":\"refresh\",\"token_type\":\"bearer\",\"expires_in\":3600}",
                ),
            )
        }
        val store = InMemoryTokenStore()
        val client = SupabaseAuthClient(
            "https://example.supabase.co",
            "publishable-key",
            store,
            openConnection = { connections.removeFirst() },
        )

        val result = client.signIn("alice@example.com", "correct horse battery staple")

        assertTrue(result.sessionCreated)
        assertFalse(result.emailConfirmationRequired)
        assertEquals("access", store.read()!!.accessToken)
        val connection = connections.lastReturned!!
        assertEquals("publishable-key", connection.capturedProperties["apikey"])
        val body = connection.output.toString(StandardCharsets.UTF_8.name())
        assertTrue(body.contains("alice@example.com"))
        assertTrue(body.contains("correct horse battery staple"))
        assertFalse(body.contains("auth0", ignoreCase = true))
    }

    @Test
    fun sign_up_without_tokens_requires_email_confirmation() {
        val connections = ConnectionQueue().apply {
            add(
                FakeHttpURLConnection(
                    URL("https://example.supabase.co/auth/v1/signup"),
                    HttpURLConnection.HTTP_OK,
                    "{\"access_token\":\"\",\"refresh_token\":\"\",\"user\":{\"id\":\"user-123\"}}",
                ),
            )
        }
        val store = InMemoryTokenStore()
        val client = SupabaseAuthClient(
            "https://example.supabase.co",
            "publishable-key",
            store,
            openConnection = { connections.removeFirst() },
        )

        val result = client.signUp("alice@example.com", "correct horse battery staple")

        assertFalse(result.sessionCreated)
        assertTrue(result.emailConfirmationRequired)
        assertNull(store.read())
    }

    private class InMemoryTokenStore : AuthTokenStore {
        private var value: SupabaseTokenSet? = null

        override fun read(): SupabaseTokenSet? = value

        override fun write(tokenSet: SupabaseTokenSet) {
            value = tokenSet
        }

        override fun clear() {
            value = null
        }
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
        val output = java.io.ByteArrayOutputStream()

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
