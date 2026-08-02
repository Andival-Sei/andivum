package io.github.andivalsei.andivum

import java.net.HttpURLConnection
import java.net.URL
import org.json.JSONObject

data class AuthOperationResult(
    val sessionCreated: Boolean,
    val emailConfirmationRequired: Boolean,
)

class SupabaseAuthClient(
    private val supabaseUrl: String,
    private val publishableKey: String,
    private val tokenStore: AuthTokenStore,
    private val openConnection: (URL) -> HttpURLConnection = { url ->
        url.openConnection() as HttpURLConnection
    },
    private val clock: () -> Long = { System.currentTimeMillis() / 1000 },
) {
    private val authBaseUrl = "${supabaseUrl.trimEnd('/')}/auth/v1"
    private val profileClient = SupabaseProfileClient(
        supabaseUrl,
        publishableKey,
        openConnection,
    )

    fun currentSession(): SupabaseTokenSet? = tokenStore.read()

    fun signIn(email: String, password: String): AuthOperationResult =
        authenticate("token?grant_type=password", email, password)

    fun signUp(email: String, password: String): AuthOperationResult =
        authenticate("signup", email, password)

    fun getCurrentSession(): SessionResponse {
        var token = tokenStore.read()
            ?: throw IllegalStateException("No saved session is available.")
        var refreshed = false

        if (needsRefresh(token)) {
            token = refresh(token)
            refreshed = true
        }

        repeat(2) {
            try {
                return profileClient.getCurrentSession(token.accessToken)
            } catch (exception: SessionUnauthorizedException) {
                if (refreshed || token.refreshToken.isNullOrBlank()) {
                    throw exception
                }
                token = refresh(token)
                refreshed = true
            }
        }

        throw IllegalStateException("Supabase session validation could not be completed.")
    }

    fun clearSession() {
        tokenStore.clear()
    }

    fun signOut() {
        val token = tokenStore.read()
        try {
            if (token != null && token.accessToken.isNotBlank()) {
                request(
                    endpoint = "$authBaseUrl/logout",
                    method = "POST",
                    accessToken = token.accessToken,
                )
            }
        } finally {
            tokenStore.clear()
        }
    }

    private fun authenticate(
        path: String,
        email: String,
        password: String,
    ): AuthOperationResult {
        require(email.isNotBlank()) { "Email is required." }
        require(password.isNotBlank()) { "Password is required." }

        val body = JSONObject()
            .put("email", email)
            .put("password", password)
        val response = request(
            endpoint = "$authBaseUrl/$path",
            method = "POST",
            body = body.toString(),
        )
        ensureSuccess(response)
        val json = JSONObject(response.body)
        val accessToken = json.optString("access_token")
        if (accessToken.isBlank()) {
            return AuthOperationResult(
                sessionCreated = false,
                emailConfirmationRequired = path == "signup",
            )
        }

        tokenStore.write(
            SupabaseTokenSet(
                accessToken = accessToken,
                refreshToken = json.optString("refresh_token").takeIf { it.isNotBlank() },
                tokenType = json.optString("token_type", "Bearer"),
                expiresIn = json.optInt("expires_in", 3600),
                issuedAtEpochSeconds = clock(),
            ),
        )
        return AuthOperationResult(
            sessionCreated = true,
            emailConfirmationRequired = false,
        )
    }

    private fun refresh(current: SupabaseTokenSet): SupabaseTokenSet {
        val refreshToken = current.refreshToken
            ?: throw IllegalStateException("No Supabase refresh token is available.")
        val response = request(
            endpoint = "$authBaseUrl/token?grant_type=refresh_token",
            method = "POST",
            body = JSONObject().put("refresh_token", refreshToken).toString(),
        )
        ensureSuccess(response)
        val json = JSONObject(response.body)
        val accessToken = json.optString("access_token")
        require(accessToken.isNotBlank()) { "Supabase refresh returned no access token." }
        val refreshed = SupabaseTokenSet(
            accessToken = accessToken,
            refreshToken = json.optString("refresh_token")
                .takeIf { it.isNotBlank() }
                ?: current.refreshToken,
            tokenType = json.optString("token_type", "Bearer"),
            expiresIn = json.optInt("expires_in", 3600),
            issuedAtEpochSeconds = clock(),
        )
        tokenStore.write(refreshed)
        return refreshed
    }

    private fun needsRefresh(token: SupabaseTokenSet): Boolean {
        if (token.refreshToken.isNullOrBlank()) return false
        if (token.issuedAtEpochSeconds == 0L) return true
        return clock() >= token.issuedAtEpochSeconds +
            (token.expiresIn - 30).coerceAtLeast(0)
    }

    private fun request(
        endpoint: String,
        method: String,
        accessToken: String? = null,
        body: String? = null,
    ): Response {
        val connection = openConnection(URL(endpoint)).apply {
            requestMethod = method
            setRequestProperty("apikey", publishableKey)
            accessToken?.let { setRequestProperty("Authorization", "Bearer $it") }
            connectTimeout = 10_000
            readTimeout = 10_000
            if (body != null) {
                doOutput = true
                setRequestProperty("Content-Type", "application/json")
            }
        }

        try {
            if (body != null) {
                connection.outputStream.use { output ->
                    output.write(body.toByteArray(Charsets.UTF_8))
                }
            }
            val statusCode = connection.responseCode
            val stream = if (statusCode in 200..299) {
                connection.inputStream
            } else {
                connection.errorStream
            }
            val responseBody = stream?.bufferedReader()?.use { it.readText() }.orEmpty()
            return Response(statusCode, responseBody)
        } finally {
            connection.disconnect()
        }
    }

    private fun ensureSuccess(response: Response) {
        if (response.statusCode !in 200..299) {
            val detail = runCatching {
                val json = JSONObject(response.body)
                listOf("message", "msg", "error_description")
                    .asSequence()
                    .map { json.optString(it) }
                    .firstOrNull { it.isNotBlank() }
            }.getOrNull().orEmpty()
            throw IllegalStateException(
                if (detail.isBlank()) {
                    "Supabase Auth request failed with HTTP ${response.statusCode}."
                } else {
                    "Supabase Auth: $detail"
                },
            )
        }
    }

    private data class Response(
        val statusCode: Int,
        val body: String,
    )
}
