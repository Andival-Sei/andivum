package io.github.andivalsei.andivum

import android.content.Context
import android.util.Base64
import java.nio.ByteBuffer
import java.security.KeyStore
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties

class SecureFinanceSettingsStore(context: Context) {
    private val preferences = context.applicationContext.getSharedPreferences(
        "andivum_finance_settings",
        Context.MODE_PRIVATE,
    )

    fun readGeminiApiKey(): String? = runCatching {
        val encoded = preferences.getString(KEY_GEMINI, null) ?: return null
        val packed = Base64.decode(encoded, Base64.NO_WRAP)
        val buffer = ByteBuffer.wrap(packed)
        val iv = ByteArray(buffer.getInt()).also(buffer::get)
        val ciphertext = ByteArray(buffer.remaining()).also(buffer::get)
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, iv))
        cipher.doFinal(ciphertext).toString(Charsets.UTF_8).ifBlank { null }
    }.getOrNull()

    fun writeGeminiApiKey(value: String) {
        val normalized = value.trim()
        if (normalized.isEmpty()) {
            clearGeminiApiKey()
            return
        }
        val iv = ByteArray(12).also(SecureRandom()::nextBytes)
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, key(), GCMParameterSpec(128, iv))
        val ciphertext = cipher.doFinal(normalized.toByteArray(Charsets.UTF_8))
        val packed = ByteBuffer.allocate(Int.SIZE_BYTES + iv.size + ciphertext.size)
            .putInt(iv.size)
            .put(iv)
            .put(ciphertext)
            .array()
        preferences.edit()
            .putString(KEY_GEMINI, Base64.encodeToString(packed, Base64.NO_WRAP))
            .apply()
    }

    fun clearGeminiApiKey() {
        preferences.edit().remove(KEY_GEMINI).apply()
    }

    private fun key(): SecretKey {
        val store = KeyStore.getInstance(ANDROID_KEYSTORE).apply { load(null) }
        if (!store.containsAlias(KEY_ALIAS)) {
            KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, ANDROID_KEYSTORE).apply {
                init(
                    KeyGenParameterSpec.Builder(
                        KEY_ALIAS,
                        KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT,
                    )
                        .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                        .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                        .setRandomizedEncryptionRequired(true)
                        .build(),
                )
            }.generateKey()
        }
        return (store.getKey(KEY_ALIAS, null) as SecretKey)
    }

    private companion object {
        const val ANDROID_KEYSTORE = "AndroidKeyStore"
        const val KEY_ALIAS = "andivum.finance.gemini"
        const val KEY_GEMINI = "gemini_api_key"
        const val TRANSFORMATION = "AES/GCM/NoPadding"
    }
}
