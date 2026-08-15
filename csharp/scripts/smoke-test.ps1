[CmdletBinding(DefaultParameterSetName = "Archive")]
param(
    [Parameter(Mandatory = $true, ParameterSetName = "Archive")]
    [string]$ArchivePath,

    [Parameter(Mandatory = $true, ParameterSetName = "Executable")]
    [string]$ExecutablePath,

    [Parameter(ParameterSetName = "Archive")]
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"
$maximumArchiveBytes = 200L * 1024L * 1024L

function Invoke-IPCESmokeTest {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "IPCEApp.exe does not exist: $Path"
    }

    $process = Start-Process `
        -FilePath $Path `
        -ArgumentList "--smoke-test" `
        -PassThru `
        -Wait `
        -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        throw "Compiled smoke test failed with exit code $($process.ExitCode)."
    }

    Write-Output "Compiled smoke test passed: $Path"
}

try {
    if ($PSCmdlet.ParameterSetName -eq "Executable") {
        Invoke-IPCESmokeTest -Path (
            [System.IO.Path]::GetFullPath($ExecutablePath))
        exit 0
    }

    $fullArchivePath = [System.IO.Path]::GetFullPath($ArchivePath)
    if (-not (Test-Path -LiteralPath $fullArchivePath -PathType Leaf)) {
        throw "Archive does not exist: $fullArchivePath"
    }

    $archiveLength = (Get-Item -LiteralPath $fullArchivePath).Length
    if ($archiveLength -ge $maximumArchiveBytes) {
        throw "Archive must be smaller than 200 MB; actual bytes: $archiveLength"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($fullArchivePath)
    try {
        $entryNames = @($archive.Entries | ForEach-Object { $_.FullName })
        if (-not ($entryNames -contains "IPCEApp.exe")) {
            throw "Archive root is missing IPCEApp.exe."
        }

        $runtimeEntry = $entryNames | Where-Object {
            $entryName = $_
            if ($entryName -match "(?i)MATLAB Runtime") {
                return $true
            }
            if ($entryName -match "(?i)(^|[/\\])mcr([^/\\]*|[/\\])") {
                return $true
            }
            if ($entryName -match "(?i)(^|[/\\])v93([^/\\]*|[/\\])") {
                return $true
            }
            return $false
        } | Select-Object -First 1
        if ($null -ne $runtimeEntry) {
            throw "Archive contains MATLAB Runtime marker: $runtimeEntry"
        }
    }
    finally {
        $archive.Dispose()
    }

    Write-Output "Portable archive validation passed: $fullArchivePath"
    if ($ValidateOnly) {
        exit 0
    }

    $temporaryDirectory = Join-Path `
        ([System.IO.Path]::GetTempPath()) `
        ("ipce-smoke-" + [Guid]::NewGuid().ToString("N"))
    [System.IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
    try {
        [System.IO.Compression.ZipFile]::ExtractToDirectory(
            $fullArchivePath,
            $temporaryDirectory)
        Invoke-IPCESmokeTest -Path (
            Join-Path $temporaryDirectory "IPCEApp.exe")
    }
    finally {
        if (Test-Path -LiteralPath $temporaryDirectory) {
            Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
        }
    }
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
