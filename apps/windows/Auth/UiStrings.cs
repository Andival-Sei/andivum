using Microsoft.Windows.ApplicationModel.Resources;

namespace Andivum_Windows.Auth;

public static class UiStrings
{
    private static readonly ResourceLoader Loader = new();

    public static string Get(string key) => Loader.GetString(key);
}
