package io.github.andivalsei.andivum

enum class AuthProvider {
    SUPABASE,
}

data class AuthConfiguration(
    val provider: AuthProvider,
    val supabaseUrl: String,
    val supabasePublishableKey: String,
) {
    companion object {
        fun fromBuildConfig(): AuthConfiguration = fromValues(
            "android",
            mapOf(
                "ANDIVUM_AUTH_PROVIDER" to BuildConfig.AUTH_PROVIDER,
                "ANDIVUM_SUPABASE_URL" to BuildConfig.SUPABASE_URL,
                "ANDIVUM_SUPABASE_PUBLISHABLE_KEY" to BuildConfig.SUPABASE_PUBLISHABLE_KEY,
            ),
        )

        fun fromValues(
            platform: String,
            values: Map<String, String>,
        ): AuthConfiguration {
            require(platform == "android" || platform == "windows") {
                "Unsupported native auth platform '$platform'."
            }

            val provider = values["ANDIVUM_AUTH_PROVIDER"]
                ?.trim()
                ?.lowercase()
                .orEmpty()
            require(provider.isEmpty() || provider == "supabase") {
                "Unsupported ANDIVUM_AUTH_PROVIDER. Only 'supabase' is available."
            }

            val defaultUrl = if (platform == "android") {
                "http://10.0.2.2:54321"
            } else {
                "http://localhost:54321"
            }
            return AuthConfiguration(
                provider = AuthProvider.SUPABASE,
                supabaseUrl = normalizeSupabaseUrl(
                    values["ANDIVUM_SUPABASE_URL"]
                        ?.trim()
                        ?.takeIf { it.isNotBlank() }
                        ?: defaultUrl,
                    "ANDIVUM_SUPABASE_URL",
                ),
                supabasePublishableKey = values["ANDIVUM_SUPABASE_PUBLISHABLE_KEY"]
                    ?.trim()
                    ?.takeIf { it.isNotBlank() }
                    ?: "local-publishable-key",
            )
        }

        private fun normalizeSupabaseUrl(value: String, key: String): String {
            val normalized = value.trim().trimEnd('/')
            val uri = runCatching { java.net.URI(normalized) }.getOrNull()
            val isLocalHttp = uri?.scheme == "http" &&
                uri.host in setOf("localhost", "127.0.0.1", "10.0.2.2")
            require(
                uri != null &&
                    (uri.scheme == "https" || isLocalHttp) &&
                    !uri.host.isNullOrBlank() &&
                    uri.rawPath.isNullOrEmpty() &&
                    uri.rawQuery.isNullOrEmpty() &&
                    uri.rawFragment.isNullOrEmpty() &&
                    uri.rawUserInfo.isNullOrEmpty(),
            ) {
                "$key must be an HTTPS origin without a path; HTTP is allowed only for local development."
            }
            return normalized
        }
    }
}
