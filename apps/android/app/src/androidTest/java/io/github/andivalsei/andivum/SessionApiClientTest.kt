package io.github.andivalsei.andivum

import java.net.HttpURLConnection
import java.net.URL
import org.junit.Assert.assertThrows
import org.junit.Test

class SessionApiClientTest {
    @Test
    fun unauthorized_response_is_distinguished_for_one_refresh_retry() {
        val client = SessionApiClient("https://localhost:7240") { url ->
            FakeHttpURLConnection(url, HttpURLConnection.HTTP_UNAUTHORIZED)
        }

        assertThrows(SessionUnauthorizedException::class.java) {
            client.getCurrentSession("access-token")
        }
    }

    private class FakeHttpURLConnection(
        url: URL,
        private val statusCode: Int,
    ) : HttpURLConnection(url) {
        override fun disconnect() = Unit

        override fun usingProxy(): Boolean = false

        override fun connect() = Unit

        override fun getResponseCode(): Int = statusCode
    }
}
