package io.github.andivalsei.andivum

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Assert.assertThrows
import org.junit.Test

class AuthConfigurationTest {
    @Test
    fun local_defaults_keep_the_development_fallback() {
        val configuration = AuthConfiguration.fromValues("android", emptyMap())

        assertEquals(AuthProvider.LOCAL, configuration.provider)
        assertEquals("https://10.0.2.2:7240", configuration.issuer)
        assertEquals("andivum-android", configuration.clientId)
        assertEquals("andivum://android/auth/callback", configuration.redirectUri)
        assertFalse(configuration.usesSupabase)
    }

    @Test
    fun auth0_configuration_uses_supabase_data_settings() {
        val configuration = AuthConfiguration.fromValues(
            "android",
            mapOf(
                "ANDIVUM_AUTH_PROVIDER" to "auth0-supabase",
                "ANDIVUM_AUTH0_DOMAIN" to "dev-example.eu.auth0.com",
                "ANDIVUM_AUTH0_ANDROID_CLIENT_ID" to "android-client",
                "ANDIVUM_AUTH0_ANDROID_REDIRECT_URI" to
                    "andivum://android/auth/callback",
                "ANDIVUM_SUPABASE_URL" to "https://example.supabase.co",
                "ANDIVUM_SUPABASE_PUBLISHABLE_KEY" to "publishable-key",
            ),
        )

        assertEquals(AuthProvider.AUTH0_SUPABASE, configuration.provider)
        assertEquals("https://dev-example.eu.auth0.com", configuration.issuer)
        assertEquals("android-client", configuration.clientId)
        assertEquals("https://example.supabase.co", configuration.supabaseUrl)
        assertTrue(configuration.usesSupabase)
    }

    @Test
    fun auth0_configuration_fails_when_a_required_value_is_missing() {
        val exception = assertThrows(IllegalArgumentException::class.java) {
            AuthConfiguration.fromValues(
                "android",
                mapOf(
                    "ANDIVUM_AUTH_PROVIDER" to "auth0-supabase",
                    "ANDIVUM_AUTH0_DOMAIN" to "dev-example.eu.auth0.com",
                ),
            )
        }

        assertTrue(exception.message!!.contains("ANDIVUM_AUTH0_ANDROID_CLIENT_ID"))
    }

    @Test
    fun auth0_configuration_rejects_a_supabase_url_that_is_not_an_origin() {
        val exception = assertThrows(IllegalArgumentException::class.java) {
            AuthConfiguration.fromValues(
                "android",
                mapOf(
                    "ANDIVUM_AUTH_PROVIDER" to "auth0-supabase",
                    "ANDIVUM_AUTH0_DOMAIN" to "dev-example.eu.auth0.com",
                    "ANDIVUM_AUTH0_ANDROID_CLIENT_ID" to "android-client",
                    "ANDIVUM_AUTH0_ANDROID_REDIRECT_URI" to
                        "andivum://android/auth/callback",
                    "ANDIVUM_SUPABASE_URL" to
                        "https://example.supabase.co?unexpected=query",
                    "ANDIVUM_SUPABASE_PUBLISHABLE_KEY" to "publishable-key",
                ),
            )
        }

        assertTrue(exception.message!!.contains("ANDIVUM_SUPABASE_URL"))
    }
}
