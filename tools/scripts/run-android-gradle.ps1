param(
    [string] $ApiBaseUrl,
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
if ($ApiBaseUrl) {
    $effectiveGradleArguments += "-PandivumApiBaseUrl=$ApiBaseUrl"
}
& $gradleWrapper @effectiveGradleArguments
exit $LASTEXITCODE
