package io.github.andivalsei.andivum

enum class AuthShellScreen {
    SIGN_IN,
    DASHBOARD,
}

data class AuthShellState(
    val isSignedIn: Boolean,
    val isBusy: Boolean = false,
    val message: String? = null,
) {
    val screen: AuthShellScreen
        get() = if (isSignedIn) AuthShellScreen.DASHBOARD else AuthShellScreen.SIGN_IN
}
