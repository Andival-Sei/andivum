package io.github.andivalsei.andivum

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp

class MainActivity : ComponentActivity() {
    private lateinit var authManager: AuthManager
    private var sessionStatus by mutableStateOf("")
    private val authorizationLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult(),
    ) { result ->
        result.data?.let { data ->
            authManager.handleCallback(data) { status ->
                runOnUiThread { sessionStatus = status }
            }
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        authManager = AuthManager(this)
        setContent {
            MaterialTheme {
                Surface(modifier = Modifier.fillMaxSize()) {
                    Column(
                        modifier = Modifier.padding(32.dp),
                        verticalArrangement = Arrangement.Center,
                        horizontalAlignment = Alignment.Start,
                    ) {
                        Text(stringResource(R.string.app_name), style = MaterialTheme.typography.headlineLarge)
                        Text(
                            text = stringResource(R.string.welcome),
                            modifier = Modifier.padding(top = 12.dp, bottom = 24.dp),
                        )
                        Button(onClick = { beginSignIn() }) {
                            Text(stringResource(R.string.sign_in))
                        }
                        Button(
                            onClick = {
                                authManager.signOut()
                                sessionStatus = getString(R.string.not_signed_in)
                            },
                            modifier = Modifier.padding(top = 8.dp),
                        ) {
                            Text(stringResource(R.string.sign_out))
                        }
                        Text(
                            text = sessionStatus.ifBlank { stringResource(R.string.not_signed_in) },
                            modifier = Modifier.padding(top = 16.dp),
                        )
                    }
                }
            }
        }
    }

    override fun onDestroy() {
        authManager.close()
        super.onDestroy()
    }

    private fun beginSignIn() {
        sessionStatus = "Opening secure sign-in…"
        authManager.startSignIn(
            onReady = { authorizationLauncher.launch(it) },
            onError = { sessionStatus = it },
        )
    }
}
