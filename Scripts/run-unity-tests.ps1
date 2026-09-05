param(
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Platform = "EditMode",

    [Alias("Filter")]
    [string]$TestFilter,

    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,

    [string]$UnityPath,

    [string]$TestResults,

    [string]$LogFile,

    [switch]$BatchMode,

    [switch]$NoGraphics,

    [switch]$UseRtk
)

$ErrorActionPreference = "Stop"

function Get-UnityVersion {
    param([string]$Root)

    $versionFile = Join-Path $Root "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $versionFile)) {
        throw "ProjectVersion.txt was not found: $versionFile"
    }

    $line = Get-Content -LiteralPath $versionFile |
        Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } |
        Select-Object -First 1

    if (-not $line) {
        throw "m_EditorVersion was not found in $versionFile"
    }

    return ($line -replace '^m_EditorVersion:\s*', '').Trim()
}

function Find-UnityEditor {
    param(
        [string]$Root,
        [string]$ExplicitPath
    )

    if ($ExplicitPath) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Leaf)) {
            throw "Unity.exe was not found: $ExplicitPath"
        }
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    if ($env:UNITY_EDITOR_PATH -and
        (Test-Path -LiteralPath $env:UNITY_EDITOR_PATH -PathType Leaf)) {
        return (Resolve-Path -LiteralPath $env:UNITY_EDITOR_PATH).Path
    }

    $version = Get-UnityVersion -Root $Root

    $candidates = @(
        "D:\Unity\Editor\$version\Editor\Unity.exe",
        "C:\Unity\Editor\$version\Editor\Unity.exe",
        "E:\Unity\Editor\$version\Editor\Unity.exe",
        "$env:ProgramFiles\Unity\Hub\Editor\$version\Editor\Unity.exe",
        "D:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
        "E:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw @"
Unity $version was not found.

Specify it explicitly:
  -UnityPath "D:\Unity\Editor\$version\Editor\Unity.exe"

or set:
  `$env:UNITY_EDITOR_PATH = "D:\Unity\Editor\$version\Editor\Unity.exe"
"@
}

function ConvertTo-NativeArgument {
    param([string]$Value)

    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    return '"' + ($Value -replace '"', '\"') + '"'
}

$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$UnityPath = Find-UnityEditor -Root $ProjectPath -ExplicitPath $UnityPath

$tempDir = Join-Path $ProjectPath "Temp"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

$suffix = if ($TestFilter) {
    ($TestFilter -replace '[^A-Za-z0-9._-]', '_')
} else {
    "all"
}

if (-not $TestResults) {
    $TestResults = Join-Path $tempDir "$($Platform.ToLowerInvariant())-$suffix.xml"
}

if (-not $LogFile) {
    $LogFile = Join-Path $tempDir "$($Platform.ToLowerInvariant())-$suffix.log"
}

Remove-Item -LiteralPath $TestResults -Force -ErrorAction SilentlyContinue

$unityArgs = @(
    "-projectPath", $ProjectPath,
    "-runTests",
    "-testPlatform", $Platform,
    "-testResults", $TestResults,
    "-logFile", $LogFile
)

if ($TestFilter) {
    $unityArgs += @("-testFilter", $TestFilter)
}

if ($BatchMode) {
    $unityArgs += "-batchmode"
}

if ($NoGraphics) {
    $unityArgs += "-nographics"
}

Write-Host "Unity:       $UnityPath"
Write-Host "Project:     $ProjectPath"
Write-Host "Platform:    $Platform"
Write-Host "Filter:      $(if ($TestFilter) { $TestFilter } else { '<all>' })"
Write-Host "Results XML: $TestResults"
Write-Host "Log:         $LogFile"
Write-Host ""

if ($UseRtk) {
    if (-not (Get-Command rtk -ErrorAction SilentlyContinue)) {
        throw "rtk was not found in PATH."
    }

    Write-Host "Running via: rtk proxy"
    & rtk proxy $UnityPath @unityArgs
    $exitCode = $LASTEXITCODE
}
else {
    Write-Host "Running Unity directly (waiting for Unity to exit)"

    $argumentLine = ($unityArgs | ForEach-Object { ConvertTo-NativeArgument $_ }) -join " "
    $process = Start-Process `
        -FilePath $UnityPath `
        -ArgumentList $argumentLine `
        -WorkingDirectory $ProjectPath `
        -Wait `
        -PassThru

    $exitCode = $process.ExitCode
}

Write-Host ""

if (-not (Test-Path -LiteralPath $TestResults -PathType Leaf)) {
    Write-Error "Test result XML was not generated. Unity exit code: $exitCode`nCheck: $LogFile"
    exit 2
}

Write-Host "Test result written: $TestResults"

$run = $null
try {
    [xml]$xml = Get-Content -LiteralPath $TestResults -Raw
    $run = $xml.'test-run'

    if ($run) {
        Write-Host ""
        Write-Host "Total:   $($run.total)"
        Write-Host "Passed:  $($run.passed)"
        Write-Host "Failed:  $($run.failed)"
        Write-Host "Skipped: $($run.skipped)"
    }
}
catch {
    Write-Warning "The test result XML was generated but could not be summarized: $($_.Exception.Message)"
}

if ($exitCode -ne 0) {
    exit $exitCode
}

if ($run -and [int]$run.failed -gt 0) {
    exit 1
}

exit 0
