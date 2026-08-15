using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace IPCE.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PortablePackageTests
{
    private const string MatlabRuntimeMarkerDiagnostic =
        "Archive contains MATLAB Runtime marker";
    private static readonly Regex MatlabRuntimeMarkerDiagnosticRegex = new(
        BuildSoftWrapTolerantPattern(MatlabRuntimeMarkerDiagnostic),
        RegexOptions.CultureInvariant);

    [TestMethod]
    public void CompiledExecutable_SmokeArgumentExitsZero()
    {
        string executable = Path.Combine(
            FindCSharpProjectRoot(),
            "src",
            "IPCE.Desktop",
            "bin",
            "Release",
            "net10.0-windows10.0.19041.0",
            "IPCEApp.exe");
        Assert.IsTrue(File.Exists(executable), executable);
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        startInfo.ArgumentList.Add("--smoke-test");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start compiled IPCEApp.");
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail(
                "Compiled --smoke-test mode did not terminate in 30 seconds.");
        }

        Assert.AreEqual(0, process.ExitCode);
    }

    [TestMethod]
    public void ValidArchive_WithExecutablePassesValidation()
    {
        using var files = new TemporaryDirectory();
        string archive = files.CreateZip(
            "valid.zip",
            ("IPCEApp.exe", "portable executable marker"),
            ("PORTABLE_README_CN.txt", "说明"));

        ScriptResult result = Validate(archive);

        Assert.AreEqual(
            0,
            result.ExitCode,
            result.CombinedOutput);
        StringAssert.Contains(
            result.CombinedOutput,
            "Portable archive validation passed");
    }

    [TestMethod]
    public void MissingArchive_FailsWithSpecificDiagnostic()
    {
        using var files = new TemporaryDirectory();
        string archive = Path.Combine(files.Path, "missing.zip");

        ScriptResult result = Validate(archive);

        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(
            result.CombinedOutput,
            "Archive does not exist");
    }

    [TestMethod]
    public void ArchiveAtTwoHundredMegabytes_FailsSizeGate()
    {
        using var files = new TemporaryDirectory();
        string archive = Path.Combine(files.Path, "oversized.zip");
        using (var stream = new FileStream(
            archive,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            stream.SetLength(200L * 1024 * 1024);
        }

        ScriptResult result = Validate(archive);

        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(result.CombinedOutput, "200 MB");
    }

    [TestMethod]
    public void ArchiveWithoutExecutable_FailsWithSpecificDiagnostic()
    {
        using var files = new TemporaryDirectory();
        string archive = files.CreateZip(
            "missing-exe.zip",
            ("PORTABLE_README_CN.txt", "说明"));

        ScriptResult result = Validate(archive);

        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(
            result.CombinedOutput,
            "Archive root is missing IPCEApp.exe");
    }

    [TestMethod]
    public void ArchiveContainingMatlabRuntimeMarker_IsRejected()
    {
        const string ordinaryLineBreak = "alpha\r\nbeta";
        Assert.AreEqual(
            ordinaryLineBreak,
            NormalizeRedirectedPowerShellOutput(ordinaryLineBreak));

        using var files = new TemporaryDirectory();
        string archive = files.CreateZip(
            "runtime.zip",
            ("IPCEApp.exe", "portable executable marker"),
            ("runtime/MATLAB Runtime/v93/mcr.dll", "forbidden"));

        ScriptResult result = Validate(archive);

        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(
            result.CombinedOutput,
            "Archive contains MATLAB Runtime marker");
    }

    private static ScriptResult Validate(string archivePath)
    {
        string script = Path.Combine(
            FindCSharpProjectRoot(),
            "scripts",
            "smoke-test.ps1");
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add("-ArchivePath");
        startInfo.ArgumentList.Add(archivePath);
        startInfo.ArgumentList.Add("-ValidateOnly");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start PowerShell.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        Assert.IsTrue(
            process.WaitForExit(30_000),
            "Portable validation script timed out.");
        return new ScriptResult(
            process.ExitCode,
            NormalizeRedirectedPowerShellOutput(
                $"{output}{Environment.NewLine}{error}"));
    }

    private static string NormalizeRedirectedPowerShellOutput(string output) =>
        MatlabRuntimeMarkerDiagnosticRegex.Replace(
            output,
            MatlabRuntimeMarkerDiagnostic);

    private static string BuildSoftWrapTolerantPattern(string phrase)
    {
        var pattern = new StringBuilder();
        for (int index = 0; index < phrase.Length; index++)
        {
            pattern.Append(Regex.Escape(phrase[index].ToString()));
            if (index + 1 < phrase.Length &&
                IsAsciiLetter(phrase[index]) &&
                IsAsciiLetter(phrase[index + 1]))
            {
                pattern.Append("(?:\\r?\\n)?");
            }
        }

        return pattern.ToString();
    }

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static string FindCSharpProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "IPCE.slnx")) &&
                File.Exists(Path.Combine(
                    directory.FullName,
                    "scripts",
                    "smoke-test.ps1")) &&
                Directory.Exists(Path.Combine(
                    directory.FullName,
                    "src",
                    "IPCE.Desktop")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the IPCE C# project root.");
    }

    private sealed record ScriptResult(
        int ExitCode,
        string CombinedOutput);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"ipce-package-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string CreateZip(
            string fileName,
            params (string Name, string Contents)[] entries)
        {
            string archivePath = System.IO.Path.Combine(Path, fileName);
            using ZipArchive archive = ZipFile.Open(
                archivePath,
                ZipArchiveMode.Create);
            foreach ((string name, string contents) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name);
                using StreamWriter writer = new(entry.Open());
                writer.Write(contents);
            }

            return archivePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
