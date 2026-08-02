package io.github.andivalsei.andivum

import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class AuthShellStateTest {
    @Test
    fun an_existing_session_opens_the_dashboard_screen() {
        assertEquals(
            AuthShellScreen.DASHBOARD,
            AuthShellState(isSignedIn = true).screen,
        )
    }

    @Test
    fun a_signed_out_session_opens_the_sign_in_screen() {
        assertEquals(
            AuthShellScreen.SIGN_IN,
            AuthShellState(isSignedIn = false).screen,
        )
    }

    @Test
    fun a_failed_auth_check_returns_to_sign_in_and_allows_retry() {
        val recovered = AuthShellState(
            isSignedIn = false,
            isBusy = true,
            sessionStatus = "old session",
        ).recoverAfterAuthFailure("Please sign in again.")

        assertFalse(recovered.isBusy)
        assertEquals(AuthShellScreen.SIGN_IN, recovered.screen)
        assertEquals("Please sign in again.", recovered.message)
        assertEquals(null, recovered.sessionStatus)
    }
}
