package io.github.andivalsei.andivum

import org.json.JSONObject

data class SupabaseTokenSet(
    val accessToken: String,
    val refreshToken: String?,
    val tokenType: String,
    val expiresIn: Int,
    val issuedAtEpochSeconds: Long,
) {
    fun toJson(): JSONObject = JSONObject()
        .put("access_token", accessToken)
        .put("refresh_token", refreshToken)
        .put("token_type", tokenType)
        .put("expires_in", expiresIn)
        .put("issued_at", issuedAtEpochSeconds)

    companion object {
        fun fromJson(json: JSONObject): SupabaseTokenSet = SupabaseTokenSet(
            accessToken = json.getString("access_token"),
            refreshToken = json.optString("refresh_token").takeIf { it.isNotBlank() },
            tokenType = json.optString("token_type", "Bearer"),
            expiresIn = json.optInt("expires_in", 3600),
            issuedAtEpochSeconds = json.optLong("issued_at", 0),
        )
    }
}
