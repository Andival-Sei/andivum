package io.github.andivalsei.andivum

import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Test

class SecureFinanceSettingsStoreTest {
    @Test
    fun geminiKeyRoundTripsThroughKeystore() {
        val store = SecureFinanceSettingsStore(
            InstrumentationRegistry.getInstrumentation().targetContext,
        )
        store.writeGeminiApiKey("test-gemini-key")
        assertEquals("test-gemini-key", store.readGeminiApiKey())
        store.clearGeminiApiKey()
    }
}
