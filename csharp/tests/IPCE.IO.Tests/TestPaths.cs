namespace IPCE.IO.Tests;

internal static class TestPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string DefaultsRoot { get; } = Path.Combine(
        RepositoryRoot,
        "data",
        "defaults");

    public static string ExamplesRoot { get; } = Path.Combine(
        RepositoryRoot,
        "data",
        "examples");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            bool hasMatlab = File.Exists(Path.Combine(
                directory.FullName,
                "matlab",
                "ipceDefaultConfig.m"));
            bool hasDefaults = Directory.Exists(Path.Combine(
                directory.FullName,
                "data",
                "defaults"));
            if (hasMatlab && hasDefaults)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the IPCE repository root.");
    }
}
