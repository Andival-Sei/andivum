using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Andivum.Api.Identity;

public sealed class IdentityOptionsSetup(
    IHostEnvironment environment,
    IConfiguration configuration) : IConfigureOptions<IdentityPasskeyOptions>
{
    public void Configure(IdentityPasskeyOptions options)
    {
        var serverDomain = configuration["Authentication:Passkey:ServerDomain"];
        var allowedOrigins = configuration
            .GetSection("Authentication:Passkey:AllowedOrigins")
            .GetChildren()
            .Select(section => section.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

        if (allowedOrigins.Length == 0 && environment.IsDevelopment())
        {
            allowedOrigins = ["https://localhost:7240"];
        }

        Configure(options, environment.EnvironmentName, serverDomain, allowedOrigins);
    }

    public static void Configure(
        IdentityPasskeyOptions options,
        string environmentName,
        string? serverDomain,
        IReadOnlyCollection<string> allowedOrigins)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(allowedOrigins);

        if (!string.Equals(environmentName, Environments.Development,
                StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(serverDomain))
        {
            throw new InvalidOperationException(
                "Authentication:Passkey:ServerDomain must be configured outside Development.");
        }

        if (allowedOrigins.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one passkey origin must be configured.");
        }

        options.ServerDomain = string.Equals(
            environmentName,
            Environments.Development,
            StringComparison.OrdinalIgnoreCase)
            ? "localhost"
            : serverDomain;
        options.UserVerificationRequirement = "required";
        options.ResidentKeyRequirement = "preferred";
        options.ValidateOrigin = context => new ValueTask<bool>(
            !context.CrossOrigin &&
            AuthPolicy.IsAllowedOrigin(context.Origin, allowedOrigins));
    }
}
