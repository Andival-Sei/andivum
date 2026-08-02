param(
    [string] $OutputDirectory = (Join-Path $env:TEMP "andivum-local-ca")
)

$ErrorActionPreference = "Stop"

$rootSubject = "CN=Andivum Local Development Root CA"
$serverSubject = "CN=localhost"
$notAfter = (Get-Date).AddYears(2)
$password = $env:ANDIVUM_LOCAL_HTTPS_CERT_PASSWORD
if ([string]::IsNullOrWhiteSpace($password)) {
    $password = "andivum-local-only"
}

$root = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $rootSubject -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt (Get-Date)
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $root) {
    $root = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $rootSubject `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -NotAfter $notAfter `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyExportPolicy Exportable `
        -KeyUsage CertSign, CRLSign, DigitalSignature `
        -TextExtension @("2.5.29.19={text}CA=true&pathlength=1")
}

$server = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $serverSubject -and
        $_.Issuer -eq $root.Subject -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt (Get-Date)
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $server) {
    $server = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $serverSubject `
        -DnsName "localhost" `
        -Signer $root `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -NotAfter $notAfter `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyExportPolicy Exportable `
        -KeyUsage DigitalSignature, KeyEncipherment `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1")
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$rootPath = Join-Path $OutputDirectory "andivum-local-ca.crt"
$serverPath = Join-Path $OutputDirectory "localhost-server.pfx"
$securePassword = ConvertTo-SecureString $password -AsPlainText -Force

Export-Certificate -Cert $root -FilePath $rootPath -Type CERT -Force | Out-Null
Export-PfxCertificate -Cert $server -FilePath $serverPath -Password $securePassword -Force | Out-Null

$trustedRoot = Get-ChildItem Cert:\CurrentUser\Root |
    Where-Object { $_.Thumbprint -eq $root.Thumbprint } |
    Select-Object -First 1
if (-not $trustedRoot) {
    Import-Certificate -FilePath $rootPath -CertStoreLocation Cert:\CurrentUser\Root |
        Out-Null
}

Write-Output "Локальный HTTPS CA подготовлен: $rootPath"
Write-Output "Сертификат API подготовлен: $serverPath"
