plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
}

val debugApiBaseUrl = providers.gradleProperty("andivumApiBaseUrl")
    .orElse("https://10.0.2.2:7240")
    .get()
    .replace("\\", "\\\\")
    .replace("\"", "\\\"")

fun buildConfigLiteral(value: String): String = value
    .replace("\\", "\\\\")
    .replace("\"", "\\\"")

val debugAuthProvider = providers.gradleProperty("andivumAuthProvider")
    .orElse("local")
    .get()
val releaseAuthProvider = providers.gradleProperty("andivumAuthProvider")
    .orElse("auth0-supabase")
    .get()
val debugAuthClientId = providers.gradleProperty("andivumAuthClientId")
    .orElse("andivum-android")
    .get()
val debugAuthRedirectUri = providers.gradleProperty("andivumAuthRedirectUri")
    .orElse("andivum://android/auth/callback")
    .get()
val configuredAuthClientId = providers.gradleProperty("andivumAuthClientId")
    .orElse("")
    .get()
val configuredAuthRedirectUri = providers.gradleProperty("andivumAuthRedirectUri")
    .orElse("")
    .get()
val auth0Domain = providers.gradleProperty("andivumAuth0Domain").orElse("").get()
val supabaseUrl = providers.gradleProperty("andivumSupabaseUrl").orElse("").get()
val supabasePublishableKey = providers.gradleProperty("andivumSupabasePublishableKey")
    .orElse("")
    .get()

android {
    namespace = "io.github.andivalsei.andivum"
    compileSdk = 36

    defaultConfig {
        applicationId = "io.github.andivalsei.andivum"
        minSdk = 29
        targetSdk = 36
        versionCode = 1
        versionName = "0.1.0"
        manifestPlaceholders["appAuthRedirectScheme"] = "andivum"
    }

    buildTypes {
        debug {
            buildConfigField("String", "API_BASE_URL", "\"$debugApiBaseUrl\"")
            buildConfigField("String", "AUTH_PROVIDER", "\"${buildConfigLiteral(debugAuthProvider)}\"")
            buildConfigField("String", "AUTH_ISSUER", "\"$debugApiBaseUrl\"")
            buildConfigField("String", "AUTH_CLIENT_ID", "\"${buildConfigLiteral(debugAuthClientId)}\"")
            buildConfigField("String", "AUTH_REDIRECT_URI", "\"${buildConfigLiteral(debugAuthRedirectUri)}\"")
            buildConfigField("String", "AUTH0_DOMAIN", "\"${buildConfigLiteral(auth0Domain)}\"")
            buildConfigField("String", "SUPABASE_URL", "\"${buildConfigLiteral(supabaseUrl)}\"")
            buildConfigField("String", "SUPABASE_PUBLISHABLE_KEY", "\"${buildConfigLiteral(supabasePublishableKey)}\"")
        }
        release {
            isMinifyEnabled = false
            buildConfigField("String", "API_BASE_URL", "\"https://api.andivum.example\"")
            buildConfigField("String", "AUTH_PROVIDER", "\"${buildConfigLiteral(releaseAuthProvider)}\"")
            buildConfigField("String", "AUTH_ISSUER", "\"\"")
            buildConfigField("String", "AUTH_CLIENT_ID", "\"${buildConfigLiteral(configuredAuthClientId)}\"")
            buildConfigField("String", "AUTH_REDIRECT_URI", "\"${buildConfigLiteral(configuredAuthRedirectUri)}\"")
            buildConfigField("String", "AUTH0_DOMAIN", "\"${buildConfigLiteral(auth0Domain)}\"")
            buildConfigField("String", "SUPABASE_URL", "\"${buildConfigLiteral(supabaseUrl)}\"")
            buildConfigField("String", "SUPABASE_PUBLISHABLE_KEY", "\"${buildConfigLiteral(supabasePublishableKey)}\"")
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro",
            )
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_21
        targetCompatibility = JavaVersion.VERSION_21
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }
}

dependencies {
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.appcompat)
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.net.openid.appauth)

    androidTestImplementation(libs.androidx.test.ext.junit)
    androidTestImplementation(libs.androidx.test.runner)

    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.compose.material.icons.extended)
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.tooling.preview)
}
