using OpenIddict.Abstractions;

namespace Andivum.Api.Identity;

public static class NativeClientSeeder
{
    public static async Task SeedAsync(
        IOpenIddictApplicationManager manager,
        NativeClientRegistry registry,
        CancellationToken cancellationToken = default)
    {
        foreach (var client in registry.Clients)
        {
            if (await manager.FindByClientIdAsync(
                    client.ClientId,
                    cancellationToken) is not null)
            {
                continue;
            }

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = client.ClientId,
                ClientType = OpenIddictConstants.ClientTypes.Public,
                ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
                DisplayName = client.ClientId,
            };

            foreach (var redirectUri in client.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(redirectUri));
            }

            descriptor.Permissions.UnionWith(
            [
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope +
                    OpenIddictConstants.Scopes.OfflineAccess,
            ]);

            await manager.CreateAsync(descriptor, cancellationToken);
        }
    }
}
