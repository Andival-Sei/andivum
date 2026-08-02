param(
    [string] $ApiBaseUrl,
    [switch] $Cloud,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $GradleArguments
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$javaHomeCandidates = @(
    $env:JAVA_HOME,
    "C:\Program Files\Android\openjdk\jdk-21.0.8"
)
$javaHome = $javaHomeCandidates |
    Where-Object { $_ -and (Test-Path (Join-Path $_ "bin\java.exe")) } |
    Select-Object -First 1

if (-not $javaHome) {
    throw "Не найден JDK 17+ для Android Gradle. Установите JDK или задайте JAVA_HOME."
}

$env:JAVA_HOME = $javaHome
$gradleWrapper = Join-Path $repoRoot "apps/android/gradlew.bat"
Set-Location (Join-Path $repoRoot "apps/android")
$firstArgumentIsGradleTask = $ApiBaseUrl -and
    $ApiBaseUrl -notmatch '^[a-z][a-z0-9+.-]*://'
$effectiveGradleArguments = @($GradleArguments)
if ($firstArgumentIsGradleTask) {
    $effectiveGradleArguments = @($ApiBaseUrl) + $effectiveGradleArguments
    $ApiBaseUrl = $null
}

if ($Cloud) {
    $cloudEnvFile = Join-Path $repoRoot ".env.andivum.local"
    if (-not (Test-Path -LiteralPath $cloudEnvFile)) {
        throw "Не найден $cloudEnvFile. Создайте локальный файл с публичной cloud-конфигурацией Andivum."
    }

    $cloudValues = @{}
    foreach ($line in Get-Content -LiteralPath $cloudEnvFile) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$') {
            $key = $Matches[1]
            $value = $Matches[2].Trim()
            if ($value.Length -ge 2 -and
                (($value.StartsWith('"') -and $value.EndsWith('"')) -or
                 ($value.StartsWith("'") -and $value.EndsWith("'")))) {
                $value = $value.Substring(1, $value.Length - 2)
            }
            $cloudValues[$key] = $value
        }
    }

    $cloudProperties = [ordered]@{
        ANDIVUM_AUTH_PROVIDER = "andivumAuthProvider"
        ANDIVUM_AUTH0_DOMAIN = "andivumAuth0Domain"
        ANDIVUM_AUTH0_ANDROID_CLIENT_ID = "andivumAuthClientId"
        ANDIVUM_AUTH0_ANDROID_REDIRECT_URI = "andivumAuthRedirectUri"
        ANDIVUM_SUPABASE_URL = "andivumSupabaseUrl"
        ANDIVUM_SUPABASE_PUBLISHABLE_KEY = "andivumSupabasePublishableKey"
    }

    foreach ($entry in $cloudProperties.GetEnumerator()) {
        $value = $cloudValues[$entry.Key]
        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "В $cloudEnvFile не задано обязательное значение $($entry.Key)."
        }
        $effectiveGradleArguments += "-P$($entry.Value)=$value"
    }
}

if ($ApiBaseUrl) {
    $effectiveGradleArguments += "-PandivumApiBaseUrl=$ApiBaseUrl"
}
& $gradleWrapper @effectiveGradleArguments
exit $LASTEXITCODE
