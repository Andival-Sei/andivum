package io.github.andivalsei.andivum

import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL

data class SessionResponse(
    val userId: String,
    val authenticated: Boolean,
) {
    companion object {
        fun parse(json: String): SessionResponse {
            val body = JSONObject(json)
            val userId = body.getString("userId")
            require(userId.isNotBlank()) { "Session response has no account id." }
            return SessionResponse(
                userId = userId,
                authenticated = body.getBoolean("authenticated"),
            )
        }
    }
}

class SessionUnauthorizedException : IllegalStateException(
    "Session request failed with HTTP 401.",
)

class SessionApiClient(
    private val apiBaseUrl: String,
    private val openConnection: (URL) -> HttpURLConnection = { url ->
        url.openConnection() as HttpURLConnection
    },
) {
    fun getCurrentSession(accessToken: String): SessionResponse {
        require(accessToken.isNotBlank()) { "Access token is required." }
        val connection = openConnection(URL("$apiBaseUrl/api/v1/session")).apply {
            requestMethod = "GET"
            setRequestProperty("Authorization", "Bearer $accessToken")
            connectTimeout = 10_000
            readTimeout = 10_000
        }

        try {
            val statusCode = connection.responseCode
            if (statusCode == HttpURLConnection.HTTP_UNAUTHORIZED) {
                throw SessionUnauthorizedException()
            }
            if (statusCode !in 200..299) {
                throw IllegalStateException(
                    "Session request failed with HTTP $statusCode.",
                )
            }
            val response = SessionResponse.parse(
                connection.inputStream.bufferedReader().use { reader -> reader.readText() },
            )
            require(response.authenticated) { "Session response is not authenticated." }
            return response
        } finally {
            connection.disconnect()
        }
    }
}
