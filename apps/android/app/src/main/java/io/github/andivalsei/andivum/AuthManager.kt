package io.github.andivalsei.andivum

import android.content.Context

class AuthManager(context: Context) {
    private val configuration = AuthConfiguration.fromBuildConfig()
    private val tokenStore = SecureAuthStateStore(context.applicationContext)
    private val client = SupabaseAuthClient(
        configuration.supabaseUrl,
        configuration.supabasePublishableKey,
        tokenStore,
    )

    val authConfiguration: AuthConfiguration
        get() = configuration

    fun isSignedIn(): Boolean = client.currentSession() != null

    fun signIn(
        email: String,
        password: String,
        onComplete: (Result<AuthOperationResult>) -> Unit,
    ) {
        Thread {
            runCatching { client.signIn(email, password) }
                .let(onComplete)
        }.start()
    }

    fun signUp(
        email: String,
        password: String,
        onComplete: (Result<AuthOperationResult>) -> Unit,
    ) {
        Thread {
            runCatching { client.signUp(email, password) }
                .let(onComplete)
        }.start()
    }

    fun validateSession(onComplete: (Result<SessionResponse>) -> Unit) {
        Thread {
            runCatching { client.getCurrentSession() }
                .let(onComplete)
        }.start()
    }

    fun clearSession() {
        client.clearSession()
    }

    fun signOut() {
        Thread { client.signOut() }.start()
    }

    fun close() = Unit
}
