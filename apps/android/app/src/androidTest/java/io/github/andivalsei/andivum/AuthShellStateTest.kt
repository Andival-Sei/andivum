package io.github.andivalsei.andivum

import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
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
}
