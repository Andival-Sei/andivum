package io.github.andivalsei.andivum

import android.content.Intent
import android.net.Uri
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.AccountBalanceWallet
import androidx.compose.material.icons.outlined.CheckCircleOutline
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.OutlinedCard
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp

class MainActivity : ComponentActivity() {
    private lateinit var authManager: AuthManager
    private var uiState by mutableStateOf(AuthShellState(isSignedIn = false))
    private val authorizationLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult(),
    ) { result ->
        val data = result.data
        if (data == null) {
            uiState = uiState.copy(
                isBusy = false,
                message = getString(R.string.auth_cancelled),
            )
            return@registerForActivityResult
        }

        authManager.handleCallback(data) { status ->
            if (!authManager.isSignedIn()) {
                runOnUiThread {
                    uiState = uiState.copy(isBusy = false, message = status)
                }
                return@handleCallback
            }

            authManager.validateSession { result ->
                runOnUiThread {
                    uiState = result.fold(
                        onSuccess = {
                            uiState.copy(
                                isSignedIn = true,
                                isBusy = false,
                                message = null,
                                sessionStatus = getString(R.string.auth_session_verified),
                            )
                        },
                        onFailure = {
                            uiState.copy(
                                isSignedIn = false,
                                isBusy = false,
                                message = getString(R.string.auth_session_unavailable),
                            )
                        },
                    )
                }
            }
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        authManager = AuthManager(this)
        val hasSavedSession = authManager.isSignedIn()
        uiState = AuthShellState(isSignedIn = false, isBusy = hasSavedSession)
        setContent {
            AndivumTheme {
                AndivumApp(
                    state = uiState,
                    onSignIn = ::beginSignIn,
                    onSignOut = ::signOut,
                    onOpenAccountSettings = ::openAccountSettings,
                )
            }
        }
        if (hasSavedSession) {
            authManager.validateSession { result ->
                runOnUiThread {
                    uiState = result.fold(
                        onSuccess = {
                            uiState.copy(
                                isSignedIn = true,
                                isBusy = false,
                                sessionStatus = getString(R.string.auth_session_verified),
                            )
                        },
                        onFailure = {
                            uiState.copy(
                                isBusy = false,
                                message = getString(R.string.auth_session_unavailable),
                            )
                        },
                    )
                }
            }
        }
    }

    override fun onDestroy() {
        authManager.close()
        super.onDestroy()
    }

    private fun beginSignIn() {
        if (uiState.isBusy) return
        uiState = uiState.copy(
            isBusy = true,
            message = null,
        )
        authManager.startSignIn(
            onReady = { intent -> authorizationLauncher.launch(intent) },
            onError = { message ->
                runOnUiThread {
                    uiState = uiState.copy(
                        isBusy = false,
                        message = message,
                    )
                }
            },
        )
    }

    private fun signOut() {
        authManager.signOut()
        uiState = AuthShellState(isSignedIn = false)
    }

    private fun openAccountSettings() {
        if (authManager.authConfiguration.usesSupabase) {
            uiState = uiState.copy(
                message = getString(R.string.auth_settings_unavailable),
            )
            return
        }

        startActivity(
            Intent(
                Intent.ACTION_VIEW,
                Uri.parse("${authManager.authConfiguration.issuer}/Account/Settings"),
            ),
        )
    }
}

@Composable
private fun AndivumApp(
    state: AuthShellState,
    onSignIn: () -> Unit,
    onSignOut: () -> Unit,
    onOpenAccountSettings: () -> Unit,
) {
    when (state.screen) {
        AuthShellScreen.SIGN_IN -> SignInScreen(state = state, onSignIn = onSignIn)
        AuthShellScreen.DASHBOARD -> DashboardScreen(
            sessionStatus = state.sessionStatus,
            onSignOut = onSignOut,
            onOpenAccountSettings = onOpenAccountSettings,
        )
    }
}

@Composable
private fun SignInScreen(
    state: AuthShellState,
    onSignIn: () -> Unit,
) {
    Surface(modifier = Modifier.fillMaxSize()) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp, vertical = 32.dp),
            verticalArrangement = Arrangement.spacedBy(20.dp),
        ) {
            Spacer(modifier = Modifier.height(12.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Surface(
                    modifier = Modifier.size(52.dp),
                    shape = RoundedCornerShape(18.dp),
                    color = MaterialTheme.colorScheme.primary,
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Text(
                            text = "A",
                            color = MaterialTheme.colorScheme.onPrimary,
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.Bold,
                        )
                    }
                }
                Spacer(modifier = Modifier.width(14.dp))
                Column {
                    Text(
                        text = stringResource(R.string.app_name),
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.SemiBold,
                    )
                    Text(
                        text = stringResource(R.string.auth_eyebrow),
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.primary,
                    )
                }
            }

            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text(
                    text = stringResource(R.string.auth_title),
                    style = MaterialTheme.typography.headlineLarge,
                    fontWeight = FontWeight.Bold,
                )
                Text(
                    text = stringResource(R.string.auth_subtitle),
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }

            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(28.dp),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.primaryContainer,
                ),
            ) {
                Column(
                    modifier = Modifier.padding(24.dp),
                    verticalArrangement = Arrangement.spacedBy(14.dp),
                ) {
                    Text(
                        text = stringResource(R.string.auth_card_title),
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                    )
                    Text(
                        text = stringResource(R.string.auth_card_body),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onPrimaryContainer,
                    )
                    Button(
                        onClick = onSignIn,
                        enabled = !state.isBusy,
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(56.dp),
                        shape = RoundedCornerShape(18.dp),
                    ) {
                        Text(
                            text = stringResource(
                                if (state.isBusy) R.string.auth_waiting else R.string.sign_in,
                            ),
                            fontWeight = FontWeight.SemiBold,
                        )
                    }
                }
            }

            state.message?.let { message ->
                Text(
                    text = message,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.error,
                )
            }

            Text(
                text = stringResource(R.string.auth_security_note),
                modifier = Modifier.fillMaxWidth(),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )
        }
    }
}

private data class ModulePlaceholder(
    val title: Int,
    val body: Int,
    val status: Int,
    val icon: @Composable () -> Unit,
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DashboardScreen(
    sessionStatus: String?,
    onSignOut: () -> Unit,
    onOpenAccountSettings: () -> Unit,
) {
    val modules = listOf(
        ModulePlaceholder(
            title = R.string.module_tasks_title,
            body = R.string.module_tasks_body,
            status = R.string.module_coming_soon,
            icon = {
                Icon(
                    imageVector = Icons.Outlined.CheckCircleOutline,
                    contentDescription = null,
                )
            },
        ),
        ModulePlaceholder(
            title = R.string.module_finance_title,
            body = R.string.module_finance_body,
            status = R.string.module_coming_soon,
            icon = {
                Icon(
                    imageVector = Icons.Outlined.AccountBalanceWallet,
                    contentDescription = null,
                )
            },
        ),
    )

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text(
                            text = stringResource(R.string.app_name),
                            style = MaterialTheme.typography.titleLarge,
                        )
                        Text(
                            text = stringResource(R.string.dashboard_overview),
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                },
                actions = {
                    TextButton(onClick = onSignOut) {
                        Text(stringResource(R.string.sign_out))
                    }
                },
            )
        },
        bottomBar = {
            NavigationBar {
                NavigationBarItem(
                    selected = true,
                    onClick = {},
                    icon = { Icon(Icons.Outlined.Home, contentDescription = null) },
                    label = { Text(stringResource(R.string.navigation_overview)) },
                )
                NavigationBarItem(
                    selected = false,
                    enabled = false,
                    onClick = {},
                    icon = { Icon(Icons.Outlined.CheckCircleOutline, contentDescription = null) },
                    label = { Text(stringResource(R.string.navigation_tasks)) },
                )
                NavigationBarItem(
                    selected = false,
                    onClick = onOpenAccountSettings,
                    icon = { Icon(Icons.Outlined.Settings, contentDescription = null) },
                    label = { Text(stringResource(R.string.navigation_more)) },
                )
            }
        },
    ) { innerPadding ->
        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(
                start = 20.dp,
                end = 20.dp,
                top = innerPadding.calculateTopPadding() + 16.dp,
                bottom = innerPadding.calculateBottomPadding() + 20.dp,
            ),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(28.dp),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.primaryContainer,
                    ),
                ) {
                    Column(
                        modifier = Modifier.padding(24.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        Text(
                            text = stringResource(R.string.dashboard_eyebrow),
                            style = MaterialTheme.typography.labelLarge,
                            color = MaterialTheme.colorScheme.primary,
                        )
                        Text(
                            text = stringResource(R.string.dashboard_title),
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.Bold,
                        )
                        Text(
                            text = stringResource(R.string.dashboard_body),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onPrimaryContainer,
                        )
                        sessionStatus?.let { status ->
                            Text(
                                text = status,
                                style = MaterialTheme.typography.labelLarge,
                                color = MaterialTheme.colorScheme.primary,
                            )
                        }
                    }
                }
            }
            item {
                Text(
                    text = stringResource(R.string.dashboard_modules),
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.SemiBold,
                )
            }
            items(modules) { module ->
                OutlinedCard(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(22.dp),
                ) {
                    Row(
                        modifier = Modifier.padding(20.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Surface(
                            modifier = Modifier.size(48.dp),
                            shape = RoundedCornerShape(16.dp),
                            color = MaterialTheme.colorScheme.secondaryContainer,
                        ) {
                            Box(
                                contentAlignment = Alignment.Center,
                            ) {
                                module.icon()
                            }
                        }
                        Spacer(modifier = Modifier.width(16.dp))
                        Column(
                            modifier = Modifier.weight(1f),
                            verticalArrangement = Arrangement.spacedBy(4.dp),
                        ) {
                            Text(
                                text = stringResource(module.title),
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.SemiBold,
                            )
                            Text(
                                text = stringResource(module.body),
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                        Text(
                            text = stringResource(module.status),
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.primary,
                        )
                    }
                }
            }
            item {
                Text(
                    text = stringResource(R.string.dashboard_footer),
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 4.dp),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center,
                )
            }
        }
    }
}
