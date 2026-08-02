package io.github.andivalsei.andivum

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.assertThrows
import org.junit.Test

class AuthConfigurationTest {
    @Test
    fun defaults_use_the_local_supabase_endpoint_for_development() {
        val configuration = AuthConfiguration.fromValues("android", emptyMap())

        assertEquals(AuthProvider.SUPABASE, configuration.provider)
        assertEquals("http://10.0.2.2:54321", configuration.supabaseUrl)
        assertEquals("local-publishable-key", configuration.supabasePublishableKey)
    }

    @Test
    fun supabase_configuration_uses_the_cloud_url_and_publishable_key() {
        val configuration = AuthConfiguration.fromValues(
            "android",
            mapOf(
                "ANDIVUM_AUTH_PROVIDER" to "supabase",
                "ANDIVUM_SUPABASE_URL" to "https://example.supabase.co/",
                "ANDIVUM_SUPABASE_PUBLISHABLE_KEY" to "publishable-key",
            ),
        )

        assertEquals(AuthProvider.SUPABASE, configuration.provider)
        assertEquals("https://example.supabase.co", configuration.supabaseUrl)
        assertEquals("publishable-key", configuration.supabasePublishableKey)
    }

    @Test
    fun auth0_configuration_is_rejected_after_the_migration() {
        val exception = assertThrows(IllegalArgumentException::class.java) {
            AuthConfiguration.fromValues(
                "android",
                mapOf("ANDIVUM_AUTH_PROVIDER" to "auth0-supabase"),
            )
        }

        assertTrue(exception.message!!.contains("supabase"))
        assertTrue(!exception.message!!.contains("Auth0"))
    }

    @Test
    fun supabase_configuration_rejects_a_url_that_is_not_an_origin() {
        val exception = assertThrows(IllegalArgumentException::class.java) {
            AuthConfiguration.fromValues(
                "android",
                mapOf(
                    "ANDIVUM_AUTH_PROVIDER" to "supabase",
                    "ANDIVUM_SUPABASE_URL" to
                        "https://example.supabase.co?unexpected=query",
                    "ANDIVUM_SUPABASE_PUBLISHABLE_KEY" to "publishable-key",
                ),
            )
        }

        assertTrue(exception.message!!.contains("ANDIVUM_SUPABASE_URL"))
    }
}
