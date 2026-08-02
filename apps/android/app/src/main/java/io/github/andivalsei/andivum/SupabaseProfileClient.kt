package io.github.andivalsei.andivum

import java.net.HttpURLConnection
import java.net.URL
import org.json.JSONArray

class SupabaseProfileClient(
    private val supabaseUrl: String,
    private val publishableKey: String,
    private val openConnection: (URL) -> HttpURLConnection = { url ->
        url.openConnection() as HttpURLConnection
    },
) {
    fun getCurrentSession(idToken: String): SessionResponse {
        require(idToken.isNotBlank()) { "Auth0 ID token is required." }

        val profileEndpoint =
            "${supabaseUrl.trimEnd('/')}/rest/v1/app_profiles?select=id&limit=1"
        val existing = request(profileEndpoint, "GET", idToken)
        if (existing.statusCode == HttpURLConnection.HTTP_UNAUTHORIZED) {
            throw SessionUnauthorizedException()
        }
        if (existing.statusCode !in 200..299) {
            throw IllegalStateException(
                "Supabase profile request failed with HTTP ${existing.statusCode}.",
            )
        }

        parseFirstProfile(existing.body)?.let { return it }

        val created = request(profileEndpoint, "POST", idToken, "{}")
        if (created.statusCode == HttpURLConnection.HTTP_CONFLICT) {
            return getCurrentSession(idToken)
        }
        if (created.statusCode !in 200..299) {
            throw IllegalStateException(
                "Supabase profile bootstrap failed with HTTP ${created.statusCode}.",
            )
        }

        return parseFirstProfile(created.body)
            ?: throw IllegalStateException(
                "Supabase profile bootstrap returned no profile.",
            )
    }

    private fun request(
        endpoint: String,
        method: String,
        idToken: String,
        body: String? = null,
    ): Response {
        val connection = openConnection(URL(endpoint)).apply {
            requestMethod = method
            setRequestProperty("apikey", publishableKey)
            setRequestProperty("Authorization", "Bearer $idToken")
            connectTimeout = 10_000
            readTimeout = 10_000
            if (method == "POST") {
                doOutput = true
                setRequestProperty("Content-Type", "application/json")
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
            val responseStream = if (statusCode in 200..299) {
                connection.inputStream
            } else {
                connection.errorStream
            }
            val responseBody = responseStream?.bufferedReader()?.use { reader ->
                reader.readText()
            }.orEmpty()
            return Response(statusCode, responseBody)
        } finally {
            connection.disconnect()
        }
    }

    private fun parseFirstProfile(body: String): SessionResponse? {
        val profiles = JSONArray(body)
        if (profiles.length() == 0) {
            return null
        }

        val id = profiles.getJSONObject(0).getString("id")
        require(id.isNotBlank()) { "Supabase profile has no stable id." }
        return SessionResponse(id, authenticated = true)
    }

    private data class Response(
        val statusCode: Int,
        val body: String,
    )
}
