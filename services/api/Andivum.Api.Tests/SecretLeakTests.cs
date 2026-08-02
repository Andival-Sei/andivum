using System.Text.RegularExpressions;
using Xunit;

namespace Andivum.Api.Tests;

public sealed class SecretLeakTests
{
    [Fact]
    public void Repository_samples_do_not_contain_credentials_or_private_keys()
    {
        var root = FindRepositoryRoot();
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(IsConfigurationOrDocumentationSample)
            .ToArray();

        Assert.NotEmpty(files);

        var forbiddenPatterns = new[]
        {
            new Regex("-----BEGIN [^-]*PRIVATE KEY-----", RegexOptions.Compiled),
            new Regex("eyJ[A-Za-z0-9_-]{20,}\\.[A-Za-z0-9_-]{20,}\\.[A-Za-z0-9_-]{20,}", RegexOptions.Compiled),
            new Regex("andivum_(?:dev|test)_local_only", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (var pattern in forbiddenPatterns)
            {
                Assert.False(
                    pattern.IsMatch(content),
                    $"Potential secret literal matched {pattern} in {Path.GetRelativePath(root, file)}.");
            }
        }
    }

    private static bool IsConfigurationOrDocumentationSample(string path)
    {
        var relative = Path.GetRelativePath(FindRepositoryRoot(), path).Replace('\\', '/');
        return relative.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("config/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("contracts/", StringComparison.OrdinalIgnoreCase) ||
            relative.Equals(".env.example", StringComparison.OrdinalIgnoreCase) ||
            relative.Equals("README.md", StringComparison.OrdinalIgnoreCase) ||
            relative.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase) ||
            relative.EndsWith("appsettings.Development.json", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                File.Exists(Path.Combine(directory.FullName, "package.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be located.");
    }
}
