using System.Runtime.InteropServices;
using Windows.Security.Credentials;

namespace Andivum_Windows.Finance;

public sealed class GeminiKeyStore
{
    private const string Resource = "Andivum.Finance.Gemini";
    private const string UserName = "api-key";

    public void Save(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Gemini API key is required.", nameof(apiKey));
        }

        var vault = new PasswordVault();
        RemoveExisting(vault);
        vault.Add(new PasswordCredential(Resource, UserName, apiKey.Trim()));
    }

    public string? Read()
    {
        try
        {
            var credential = new PasswordVault().Retrieve(Resource, UserName);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public void Clear() => RemoveExisting(new PasswordVault());

    private static void RemoveExisting(PasswordVault vault)
    {
        try
        {
            vault.Remove(vault.Retrieve(Resource, UserName));
        }
        catch (COMException)
        {
            // A missing key is the normal first-run state.
        }
    }
}
