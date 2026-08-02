package io.github.andivalsei.andivum

import android.content.Context
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import net.openid.appauth.AuthState
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Test
import org.junit.runner.RunWith
import java.security.KeyStore

@RunWith(AndroidJUnit4::class)
class SecureAuthStateStoreTest {
    @Test
    fun stores_and_reads_auth_state_with_android_keystore() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        deleteStoredStateAndKey(context)
        val store = SecureAuthStateStore(context)
        val expected = AuthState().jsonSerializeString()

        try {
            store.write(AuthState())

            val restored = store.read()
            assertNotNull(restored)
            assertEquals(expected, restored!!.jsonSerializeString())
        } finally {
            deleteStoredStateAndKey(context)
        }
    }

    private fun deleteStoredStateAndKey(context: Context) {
        context.getSharedPreferences("andivum_secure_auth", Context.MODE_PRIVATE)
            .edit()
            .clear()
            .commit()

        KeyStore.getInstance("AndroidKeyStore").apply {
            load(null)
            if (containsAlias("andivum.auth.state")) {
                deleteEntry("andivum.auth.state")
            }
        }
    }
}
