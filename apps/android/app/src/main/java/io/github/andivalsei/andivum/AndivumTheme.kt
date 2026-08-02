package io.github.andivalsei.andivum

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val LightColors = lightColorScheme(
    primary = Color(0xFF4F46A5),
    onPrimary = Color.White,
    primaryContainer = Color(0xFFE6E4FF),
    onPrimaryContainer = Color(0xFF15134C),
    secondary = Color(0xFF5F5D72),
    background = Color(0xFFFBF8FF),
    surface = Color(0xFFFBF8FF),
)

private val DarkColors = darkColorScheme(
    primary = Color(0xFFC5C2FF),
    onPrimary = Color(0xFF29256B),
    primaryContainer = Color(0xFF3D397F),
    onPrimaryContainer = Color(0xFFE6E4FF),
    secondary = Color(0xFFC6C3DB),
    background = Color(0xFF13131B),
    surface = Color(0xFF13131B),
)

@Composable
fun AndivumTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = if (isSystemInDarkTheme()) DarkColors else LightColors,
        content = content,
    )
}
