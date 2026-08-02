package io.github.andivalsei.andivum

import android.content.Context
import android.content.Intent
import net.openid.appauth.AuthorizationException
import net.openid.appauth.AuthorizationRequest
import net.openid.appauth.AuthorizationResponse
import net.openid.appauth.AuthorizationService
import net.openid.appauth.AuthorizationServiceConfiguration
import net.openid.appauth.ResponseTypeValues
import net.openid.appauth.AuthState
import android.net.Uri

class AuthManager(context: Context) {
    companion object {
        const val clientId = "andivum-android"
        const val redirectUri = "andivum://android/auth/callback"
    }

    private val appContext = context.applicationContext
    private val configuration = AuthConfiguration.fromBuildConfig()
    private val issuer = configuration.issuer
    private val authorizationService = AuthorizationService(appContext)
    private val stateStore = SecureAuthStateStore(appContext)
    private val sessionApiClient = SessionApiClient(issuer)
    private val supabaseProfileClient = if (configuration.usesSupabase) {
        SupabaseProfileClient(
            configuration.supabaseUrl!!,
            configuration.supabasePublishableKey!!,
        )
    } else {
        null
    }
    private var state = stateStore.read() ?: AuthState()

    val authConfiguration: AuthConfiguration
        get() = configuration

    fun startSignIn(onReady: (Intent) -> Unit, onError: (String) -> Unit) {
        AuthorizationServiceConfiguration.fetchFromIssuer(Uri.parse(issuer)) { configuration, exception ->
            if (configuration == null) {
                onError(exception?.errorDescription ?: appContext.getString(R.string.auth_discovery_failed))
                return@fetchFromIssuer
            }

            val request = AuthorizationRequest.Builder(
                configuration,
                this@AuthManager.configuration.clientId,
                ResponseTypeValues.CODE,
                Uri.parse(this@AuthManager.configuration.redirectUri),
            )
                .setScope("openid profile offline_access")
                .setCodeVerifier(Pkce.createVerifier())
                .build()
            onReady(authorizationService.getAuthorizationRequestIntent(request))
        }
    }

    fun handleCallback(intent: Intent, onComplete: (String) -> Unit) {
        val response = AuthorizationResponse.fromIntent(intent)
        val exception = AuthorizationException.fromIntent(intent)
        state = AuthState()
        state.update(response, exception)
        if (response is AuthorizationResponse) {
            authorizationService.performTokenRequest(response.createTokenExchangeRequest()) { tokenResponse, tokenException ->
                if (tokenException != null || tokenResponse == null) {
                    state = AuthState()
                    stateStore.clear()
                } else {
                    state.update(tokenResponse, null)
                    stateStore.write(state)
                }
                onComplete(
                    if (tokenException == null && tokenResponse != null) {
                        appContext.getString(R.string.auth_signed_in)
                    } else {
                        tokenException?.errorDescription
                            ?: appContext.getString(R.string.auth_token_exchange_failed)
                    },
                )
            }
        } else {
            state = AuthState()
            stateStore.clear()
            onComplete(exception?.errorDescription ?: appContext.getString(R.string.auth_failed))
        }
    }

    fun isSignedIn(): Boolean = state.isAuthorized

    fun validateSession(onComplete: (Result<SessionResponse>) -> Unit) {
        state.performActionWithFreshTokens(authorizationService) { accessToken, _, exception ->
            if (exception != null || accessToken.isNullOrBlank()) {
                clearSession()
                onComplete(
                    Result.failure(
                        exception ?: IllegalStateException("No access token is available."),
                    ),
                )
                return@performActionWithFreshTokens
            }

            stateStore.write(state)
            validateWithAccessToken(accessToken, onComplete)
        }
    }

    private fun validateWithAccessToken(
        accessToken: String,
        onComplete: (Result<SessionResponse>) -> Unit,
    ) {
        Thread {
            try {
                val session = if (configuration.usesSupabase) {
                    supabaseProfileClient!!.getCurrentSession(state.idToken ?: "")
                } else {
                    sessionApiClient.getCurrentSession(accessToken)
                }
                onComplete(Result.success(session))
            } catch (exception: SessionUnauthorizedException) {
                refreshAfterUnauthorized(onComplete)
            } catch (exception: Exception) {
                onComplete(Result.failure(exception))
            }
        }.start()
    }

    private fun refreshAfterUnauthorized(onComplete: (Result<SessionResponse>) -> Unit) {
        val refreshRequest = try {
            state.createTokenRefreshRequest()
        } catch (exception: Exception) {
            clearSession()
            onComplete(Result.failure(exception))
            return
        }

        authorizationService.performTokenRequest(refreshRequest) { tokenResponse, tokenException ->
            if (tokenException != null || tokenResponse == null || state.accessToken.isNullOrBlank()) {
                clearSession()
                onComplete(
                    Result.failure(
                        tokenException ?: IllegalStateException("Refresh did not return an access token."),
                    ),
                )
                return@performTokenRequest
            }

            state.update(tokenResponse, null)
            stateStore.write(state)
            validateWithAccessToken(state.accessToken!!, onComplete)
        }
    }

    fun clearSession() {
        state = AuthState()
        stateStore.clear()
    }

    fun signOut() {
        clearSession()
    }

    fun close() {
        authorizationService.dispose()
    }
}
