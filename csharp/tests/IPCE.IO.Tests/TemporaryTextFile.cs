using System.Text;

namespace IPCE.IO.Tests;

internal sealed class TemporaryTextFile : IDisposable
{
    public TemporaryTextFile(string content, string extension = ".txt")
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"ipce-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(Path, content, new UTF8Encoding(false));
    }

    public string Path { get; }

    public void Dispose()
    {
        File.Delete(Path);
    }
}
