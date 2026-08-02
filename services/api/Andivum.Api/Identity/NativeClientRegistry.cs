namespace Andivum.Api.Identity;

public sealed record NativeClientDefinition(
    string ClientId,
    IReadOnlySet<string> RedirectUris);

public sealed class NativeClientRegistry
{
    private readonly IReadOnlyDictionary<string, NativeClientDefinition> clients;

    public NativeClientRegistry(IEnumerable<NativeClientDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var items = definitions.ToArray();
        if (items.Any(item => string.IsNullOrWhiteSpace(item.ClientId)))
        {
            throw new ArgumentException(
                "Native client IDs must not be empty.",
                nameof(definitions));
        }

        clients = items.ToDictionary(
            item => item.ClientId,
            StringComparer.Ordinal);
    }

    public static NativeClientRegistry CreateDevelopment()
    {
        return new NativeClientRegistry(
        [
            new NativeClientDefinition(
                "andivum-windows",
                new HashSet<string>(
                    ["andivum://windows/auth/callback"],
                    StringComparer.Ordinal)),
            new NativeClientDefinition(
                "andivum-android",
                new HashSet<string>(
                    ["andivum://android/auth/callback"],
                    StringComparer.Ordinal)),
        ]);
    }

    public bool IsAllowedRedirect(string clientId, string? redirectUri)
    {
        return redirectUri is not null &&
            clients.TryGetValue(clientId, out var client) &&
            client.RedirectUris.Contains(redirectUri);
    }

    public bool IsRegistered(string clientId)
    {
        return clients.ContainsKey(clientId);
    }

    public IReadOnlyCollection<NativeClientDefinition> Clients =>
        clients.Values.ToArray();
}
