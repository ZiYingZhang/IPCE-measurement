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
$coreTestProject = Join-Path `
    $csharpDirectory `
    "tests\IPCE.Core.Tests\IPCE.Core.Tests.csproj"
$ioTestProject = Join-Path `
    $csharpDirectory `
    "tests\IPCE.IO.Tests\IPCE.IO.Tests.csproj"
$desktopTestProject = Join-Path `
    $csharpDirectory `
    "tests\IPCE.Desktop.Tests\IPCE.Desktop.Tests.csproj"
$solution = Join-Path $csharpDirectory "IPCE.slnx"
$readmePath = Join-Path $csharpDirectory "PORTABLE_README_CN.txt"
$noticesPath = Join-Path `
    $csharpDirectory `
    "src\IPCE.Desktop\Assets\THIRD_PARTY_NOTICES.txt"
$smokeScript = Join-Path $scriptDirectory "smoke-test.ps1"
$testCountScript = Join-Path $scriptDirectory "read-test-counts.ps1"
$testResultsDirectory = Join-Path $distDirectory "test-results"

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

    [System.IO.Directory]::CreateDirectory($distDirectory) | Out-Null
    Remove-SafeDirectory `
        -Path $testResultsDirectory `
        -AllowedRoot $distDirectory
    [System.IO.Directory]::CreateDirectory(
        $testResultsDirectory) | Out-Null

    Write-Output "Running .NET regression..."
    $testProjects = @(
        @{ Project = $coreTestProject; Result = "core.trx" },
        @{ Project = $ioTestProject; Result = "io.trx" },
        @{ Project = $desktopTestProject; Result = "desktop.trx" }
    )
    foreach ($testProject in $testProjects) {
        & dotnet test `
            $testProject.Project `
            -c Release `
            --no-restore `
            --logger "trx;LogFileName=$($testProject.Result)" `
            --results-directory $testResultsDirectory
        Assert-LastExitCode -Operation ".NET regression"
    }
    $testCountsJson = & powershell `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File $testCountScript `
        -CoreResult (Join-Path $testResultsDirectory "core.trx") `
        -IoResult (Join-Path $testResultsDirectory "io.trx") `
        -DesktopResult (Join-Path $testResultsDirectory "desktop.trx")
    Assert-LastExitCode -Operation ".NET test-count collection"
    $dotnetTests = $testCountsJson | ConvertFrom-Json

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
            total = [int]$dotnetTests.total
            core = [int]$dotnetTests.core
            io = [int]$dotnetTests.io
            desktop = [int]$dotnetTests.desktop
            failed = [int]$dotnetTests.failed
            skipped = [int]$dotnetTests.skipped
        }
        matlabSelfTestPassed = $true
        matlabUiSmokePassed = $true
        generatedUtc = [DateTime]::UtcNow.ToString("o")
    }
    $buildInfo | ConvertTo-Json | Set-Content `
        -LiteralPath $buildInfoPath `
        -Encoding UTF8
    Remove-SafeDirectory `
        -Path $testResultsDirectory `
        -AllowedRoot $distDirectory

    Write-Output "Portable build passed."
    Write-Output "Archive: $archivePath"
    Write-Output "Bytes: $($archive.Length)"
    Write-Output "SHA-256: $hash"
}
finally {
    Pop-Location
}
