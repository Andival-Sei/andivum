package io.github.andivalsei.andivum

import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class SessionResponseTest {
    @Test
    fun parses_the_authenticated_account_without_token_data() {
        val response = SessionResponse.parse(
            "{\"userId\":\"account-123\",\"authenticated\":true}",
        )

        assertEquals("account-123", response.userId)
        assertTrue(response.authenticated)
    }
}
