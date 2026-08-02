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
    private val issuer = BuildConfig.API_BASE_URL
    private val authorizationService = AuthorizationService(appContext)
    private val stateStore = SecureAuthStateStore(appContext)
    private var state = stateStore.read() ?: AuthState()

    fun startSignIn(onReady: (Intent) -> Unit, onError: (String) -> Unit) {
        AuthorizationServiceConfiguration.fetchFromIssuer(Uri.parse(issuer)) { configuration, exception ->
            if (configuration == null) {
                onError(exception?.errorDescription ?: appContext.getString(R.string.auth_discovery_failed))
                return@fetchFromIssuer
            }

            val request = AuthorizationRequest.Builder(
                configuration,
                clientId,
                ResponseTypeValues.CODE,
                Uri.parse(redirectUri),
            )
                .setScope("openid profile")
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
        stateStore.write(state)
        if (response is AuthorizationResponse) {
            authorizationService.performTokenRequest(response.createTokenExchangeRequest()) { tokenResponse, tokenException ->
                state.update(tokenResponse, tokenException)
                stateStore.write(state)
                onComplete(
                    if (tokenException == null) appContext.getString(R.string.auth_signed_in) else
                        tokenException.errorDescription ?: appContext.getString(R.string.auth_token_exchange_failed),
                )
            }
        } else {
            onComplete(exception?.errorDescription ?: appContext.getString(R.string.auth_failed))
        }
    }

    fun isSignedIn(): Boolean = state.isAuthorized

    fun signOut() {
        state = AuthState()
        stateStore.clear()
    }

    fun close() {
        authorizationService.dispose()
    }
}
