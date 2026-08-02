package io.github.andivalsei.andivum

import android.content.Context
import android.util.Base64
import net.openid.appauth.AuthState
import java.nio.ByteBuffer
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

class SecureAuthStateStore(context: Context) {
    companion object {
        private const val keyAlias = "andivum.auth.state"
        private const val preferenceName = "andivum_secure_auth"
        private const val stateKey = "auth_state"
    }

    private val preferences = context.getSharedPreferences(preferenceName, Context.MODE_PRIVATE)

    fun read(): AuthState? {
        val encoded = preferences.getString(stateKey, null) ?: return null
        return runCatching {
            val packed = Base64.decode(encoded, Base64.DEFAULT)
            val buffer = ByteBuffer.wrap(packed)
            val ivLength = buffer.int
            val iv = ByteArray(ivLength)
            buffer.get(iv)
            val ciphertext = ByteArray(buffer.remaining())
            buffer.get(ciphertext)
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, iv))
            AuthState.jsonDeserialize(String(cipher.doFinal(ciphertext), Charsets.UTF_8))
        }.getOrNull()
    }

    fun write(state: AuthState) {
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, key())
        val iv = cipher.iv
        val ciphertext = cipher.doFinal(state.jsonSerializeString().toByteArray(Charsets.UTF_8))
        val packed = ByteBuffer.allocate(Int.SIZE_BYTES + iv.size + ciphertext.size)
            .putInt(iv.size)
            .put(iv)
            .put(ciphertext)
            .array()
        preferences.edit()
            .putString(stateKey, Base64.encodeToString(packed, Base64.NO_WRAP))
            .apply()
    }

    fun clear() {
        preferences.edit().remove(stateKey).apply()
    }

    private fun key(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        val existing = keyStore.getKey(keyAlias, null) as? SecretKey
        if (existing != null) return existing

        val generator = KeyGenerator.getInstance("AES", "AndroidKeyStore")
        generator.init(
            android.security.keystore.KeyGenParameterSpec.Builder(
                keyAlias,
                android.security.keystore.KeyProperties.PURPOSE_ENCRYPT or
                    android.security.keystore.KeyProperties.PURPOSE_DECRYPT,
            )
                .setBlockModes(android.security.keystore.KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(android.security.keystore.KeyProperties.ENCRYPTION_PADDING_NONE)
                .build(),
        )
        return generator.generateKey()
    }
}
