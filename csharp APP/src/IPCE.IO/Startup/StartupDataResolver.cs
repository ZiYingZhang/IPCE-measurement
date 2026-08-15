using System.Reflection;
using IPCE.Core.Errors;

namespace IPCE.IO.Startup;

public sealed record ResolvedStartupData(
    string FileName,
    byte[] Content,
    bool IsEmbedded,
    string Source);

public static class StartupDataResolver
{
    private const string ResourcePrefix = "IPCE.IO.Defaults.";

    public static ResolvedStartupData Resolve(
        string fileName,
        string? applicationDirectory = null)
    {
        string directory =
            applicationDirectory ?? AppContext.BaseDirectory;
        string overridePath = Path.Combine(directory, fileName);
        if (File.Exists(overridePath))
        {
            return new ResolvedStartupData(
                fileName,
                File.ReadAllBytes(overridePath),
                false,
                overridePath);
        }

        Assembly assembly = typeof(DefaultConfiguration).Assembly;
        string resourceName = ResourcePrefix + fileName;
        using Stream? stream =
            assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new IpceException(
                "IPCE:StartupDataNotFound",
                $"未找到启动默认文件或嵌入资源：{fileName}");
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return new ResolvedStartupData(
            fileName,
            memory.ToArray(),
            true,
            resourceName);
    }
}
