param(
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
& $gradleWrapper @GradleArguments
exit $LASTEXITCODE
