package io.github.andivalsei.andivum

enum class AuthProvider {
    LOCAL,
    AUTH0_SUPABASE,
}

data class AuthConfiguration(
    val provider: AuthProvider,
    val issuer: String,
    val clientId: String,
    val redirectUri: String,
    val supabaseUrl: String? = null,
    val supabasePublishableKey: String? = null,
) {
    val usesSupabase: Boolean
        get() = provider == AuthProvider.AUTH0_SUPABASE

    companion object {
        fun fromBuildConfig(): AuthConfiguration = fromValues(
            "android",
            mapOf(
                "ANDIVUM_AUTH_PROVIDER" to BuildConfig.AUTH_PROVIDER,
                "ANDIVUM_LOCAL_AUTH_ISSUER" to BuildConfig.AUTH_ISSUER,
                "ANDIVUM_LOCAL_AUTH_CLIENT_ID" to BuildConfig.AUTH_CLIENT_ID,
                "ANDIVUM_LOCAL_AUTH_REDIRECT_URI" to BuildConfig.AUTH_REDIRECT_URI,
                "ANDIVUM_AUTH0_DOMAIN" to BuildConfig.AUTH0_DOMAIN,
                "ANDIVUM_AUTH0_ANDROID_CLIENT_ID" to BuildConfig.AUTH_CLIENT_ID,
                "ANDIVUM_AUTH0_ANDROID_REDIRECT_URI" to BuildConfig.AUTH_REDIRECT_URI,
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

            return when (values["ANDIVUM_AUTH_PROVIDER"]
                ?.trim()
                ?.lowercase()
                .orEmpty()) {
                "", "local" -> AuthConfiguration(
                    provider = AuthProvider.LOCAL,
                    issuer = values["ANDIVUM_LOCAL_AUTH_ISSUER"]
                        ?.takeIf { it.isNotBlank() }
                        ?: "https://10.0.2.2:7240",
                    clientId = values["ANDIVUM_LOCAL_AUTH_CLIENT_ID"]
                        ?.takeIf { it.isNotBlank() }
                        ?: "andivum-$platform",
                    redirectUri = values["ANDIVUM_LOCAL_AUTH_REDIRECT_URI"]
                        ?.takeIf { it.isNotBlank() }
                        ?: "andivum://$platform/auth/callback",
                )

                "auth0-supabase" -> createAuth0Configuration(platform, values)
                else -> throw IllegalArgumentException(
                    "Unsupported ANDIVUM_AUTH_PROVIDER. Use 'local' or 'auth0-supabase'.",
                )
            }
        }

        private fun createAuth0Configuration(
            platform: String,
            values: Map<String, String>,
        ): AuthConfiguration {
            var domain = required(values, "ANDIVUM_AUTH0_DOMAIN")
                .trim()
                .trimEnd('/')
            if (domain.startsWith("https://", ignoreCase = true)) {
                domain = domain.removePrefix("https://").trimEnd('/')
            }
            require('/' !in domain && '?' !in domain && '#' !in domain) {
                "ANDIVUM_AUTH0_DOMAIN must contain only the Auth0 host name."
            }

            val platformName = platform.uppercase()
            return AuthConfiguration(
                provider = AuthProvider.AUTH0_SUPABASE,
                issuer = "https://$domain",
                clientId = required(values, "ANDIVUM_AUTH0_${platformName}_CLIENT_ID"),
                redirectUri = required(values, "ANDIVUM_AUTH0_${platformName}_REDIRECT_URI"),
                supabaseUrl = normalizeHttpsUrl(
                    required(values, "ANDIVUM_SUPABASE_URL"),
                    "ANDIVUM_SUPABASE_URL",
                ),
                supabasePublishableKey = required(
                    values,
                    "ANDIVUM_SUPABASE_PUBLISHABLE_KEY",
                ),
            )
        }

        private fun required(values: Map<String, String>, key: String): String =
            values[key]
                ?.trim()
                ?.takeIf { it.isNotBlank() }
                ?: throw IllegalArgumentException(
                    "Auth configuration is incomplete: $key is required.",
                )

        private fun normalizeHttpsUrl(value: String, key: String): String {
            val normalized = value.trim().trimEnd('/')
            val origin = normalized.removePrefix("https://")
            require(normalized.startsWith("https://") &&
                origin.isNotBlank() &&
                '/' !in origin &&
                '?' !in origin &&
                '#' !in origin) {
                "$key must be an HTTPS origin without a path."
            }
            return normalized
        }
    }
}
