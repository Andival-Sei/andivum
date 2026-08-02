using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

namespace Andivum.Api.Identity;

public static class OpenIddictCredentialLoader
{
    public static X509Certificate2 Load(
        IConfiguration configuration,
        string settingName)
    {
        var path = configuration[$"Authentication:OpenIddict:{settingName}"];
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                $"Authentication:OpenIddict:{settingName} must be configured outside Development.");
        }

        var password = Environment.GetEnvironmentVariable(
            "ANDIVUM_OPENIDDICT_CERT_PASSWORD");
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException(
                "ANDIVUM_OPENIDDICT_CERT_PASSWORD must be provided outside Development.");
        }

        try
        {
            return X509CertificateLoader.LoadPkcs12FromFile(
                path,
                password,
                X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException(
                $"The OpenIddict certificate configured by {settingName} could not be loaded.",
                exception);
        }
    }
}
