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
    .orElse("supabase")
    .get()
val releaseAuthProvider = providers.gradleProperty("andivumAuthProvider")
    .orElse("supabase")
    .get()
val debugSupabaseUrl = providers.gradleProperty("andivumSupabaseUrl")
    .orElse("http://10.0.2.2:54321")
    .get()
val releaseSupabaseUrl = providers.gradleProperty("andivumSupabaseUrl")
    .orElse("")
    .get()
val supabasePublishableKey = providers.gradleProperty("andivumSupabasePublishableKey")
    .orElse("local-publishable-key")
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
    }

    buildTypes {
        debug {
            buildConfigField("String", "API_BASE_URL", "\"$debugApiBaseUrl\"")
            buildConfigField("String", "AUTH_PROVIDER", "\"${buildConfigLiteral(debugAuthProvider)}\"")
            buildConfigField("String", "SUPABASE_URL", "\"${buildConfigLiteral(debugSupabaseUrl)}\"")
            buildConfigField("String", "SUPABASE_PUBLISHABLE_KEY", "\"${buildConfigLiteral(supabasePublishableKey)}\"")
        }
        release {
            isMinifyEnabled = false
            buildConfigField("String", "API_BASE_URL", "\"https://api.andivum.example\"")
            buildConfigField("String", "AUTH_PROVIDER", "\"${buildConfigLiteral(releaseAuthProvider)}\"")
            buildConfigField("String", "SUPABASE_URL", "\"${buildConfigLiteral(releaseSupabaseUrl)}\"")
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
    androidTestImplementation(libs.androidx.test.ext.junit)
    androidTestImplementation(libs.androidx.test.runner)

    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.compose.material.icons.extended)
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.tooling.preview)
}
