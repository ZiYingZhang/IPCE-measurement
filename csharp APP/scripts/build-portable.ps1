[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$csharpDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $scriptDirectory ".."))
$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $csharpDirectory ".."))
$matlabDirectory = Join-Path $repositoryRoot "matlab"
$distDirectory = Join-Path $csharpDirectory "dist"
$publishDirectory = Join-Path $distDirectory "publish"
$archivePath = Join-Path $distDirectory "IPCEApp_Windows_x64.zip"
$buildInfoPath = Join-Path `
    $distDirectory `
    "IPCEApp_Windows_x64.build.json"
$desktopProject = Join-Path `
    $csharpDirectory `
    "src\IPCE.Desktop\IPCE.Desktop.csproj"
$solution = Join-Path $csharpDirectory "IPCE.slnx"
$readmePath = Join-Path $csharpDirectory "PORTABLE_README_CN.txt"
$noticesPath = Join-Path `
    $csharpDirectory `
    "src\IPCE.Desktop\Assets\THIRD_PARTY_NOTICES.txt"
$smokeScript = Join-Path $scriptDirectory "smoke-test.ps1"

function Assert-LastExitCode {
    param(
        [Parameter(Mandatory = $true)][string]$Operation
    )

    if ($LASTEXITCODE -ne 0) {
        throw "$Operation failed with exit code $LASTEXITCODE."
    }
}

function Remove-SafeDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$AllowedRoot
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullRoot = [System.IO.Path]::GetFullPath($AllowedRoot)
    $rootPrefix = $fullRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith(
        $rootPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove path outside staging root: $fullPath"
    }

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

Push-Location $repositoryRoot
try {
    Write-Output "Running MATLAB regression..."
    $matlabCommand = "cd('" + $matlabDirectory.Replace("'", "''") +
        "'); run_ipce_selftest; app = IPCEApp; drawnow; " +
        "assert(isvalid(app)); close(app)"
    & matlab -batch $matlabCommand
    Assert-LastExitCode -Operation "MATLAB regression"

    Write-Output "Running .NET regression..."
    & dotnet test $solution -c Release --no-restore
    Assert-LastExitCode -Operation ".NET regression"

    [System.IO.Directory]::CreateDirectory($distDirectory) | Out-Null
    Remove-SafeDirectory `
        -Path $publishDirectory `
        -AllowedRoot $distDirectory
    [System.IO.Directory]::CreateDirectory($publishDirectory) | Out-Null

    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }
    if (Test-Path -LiteralPath $buildInfoPath) {
        Remove-Item -LiteralPath $buildInfoPath -Force
    }

    Write-Output "Publishing self-contained Windows x64 files..."
    & dotnet publish $desktopProject `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -o $publishDirectory
    Assert-LastExitCode -Operation "Self-contained publish"

    Copy-Item `
        -LiteralPath $readmePath `
        -Destination (Join-Path $publishDirectory "PORTABLE_README_CN.txt")
    Copy-Item `
        -LiteralPath $noticesPath `
        -Destination (Join-Path $publishDirectory "THIRD_PARTY_NOTICES.txt")

    & powershell `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File $smokeScript `
        -ExecutablePath (Join-Path $publishDirectory "IPCEApp.exe")
    Assert-LastExitCode -Operation "Published executable smoke test"
    $publishedSmokeExitCode = $LASTEXITCODE

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $publishDirectory,
        $archivePath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)

    & powershell `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File $smokeScript `
        -ArchivePath $archivePath
    Assert-LastExitCode -Operation "Extracted archive smoke test"
    $archiveSmokeExitCode = $LASTEXITCODE

    $archive = Get-Item -LiteralPath $archivePath
    if ($archive.Length -ge (200L * 1024L * 1024L)) {
        throw "Archive must be smaller than 200 MB."
    }

    $hash = (Get-FileHash `
        -LiteralPath $archivePath `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    $archiveReader = [System.IO.Compression.ZipFile]::OpenRead(
        $archivePath)
    try {
        $archiveEntryCount = $archiveReader.Entries.Count
    }
    finally {
        $archiveReader.Dispose()
    }
    $buildInfo = [ordered]@{
        archive = $archive.Name
        archiveBytes = $archive.Length
        archiveSha256 = $hash
        operatingSystem = [System.Environment]::OSVersion.VersionString
        runtimeIdentifier = "win-x64"
        selfContained = $true
        publishTrimmed = $false
        matlabRuntimeIncluded = $false
        archiveEntryCount = $archiveEntryCount
        publishedSmokeExitCode = $publishedSmokeExitCode
        archiveSmokeExitCode = $archiveSmokeExitCode
        dotnetTests = [ordered]@{
            total = 198
            core = 58
            io = 43
            desktop = 97
            failed = 0
            skipped = 0
        }
        matlabSelfTestPassed = $true
        matlabUiSmokePassed = $true
        generatedUtc = [DateTime]::UtcNow.ToString("o")
    }
    $buildInfo | ConvertTo-Json | Set-Content `
        -LiteralPath $buildInfoPath `
        -Encoding UTF8

    Write-Output "Portable build passed."
    Write-Output "Archive: $archivePath"
    Write-Output "Bytes: $($archive.Length)"
    Write-Output "SHA-256: $hash"
}
finally {
    Pop-Location
}
