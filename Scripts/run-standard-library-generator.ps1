param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,

    [string]$UnityPath,

    [string]$Config = "Packages\com.skytomo221.sobakasu\Editor\Tools\StandardLibraryGenerator\standard-library-generation-config.json",

    [string]$Output,

    [string]$Additions,

    [string]$Diagnostics,

    [string]$LogFile,

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

if (-not [IO.Path]::IsPathRooted($Config)) {
    $Config = Join-Path $ProjectPath $Config
}

if (-not (Test-Path -LiteralPath $Config -PathType Leaf)) {
    throw "Standard library generation config was not found: $Config"
}

$Config = (Resolve-Path -LiteralPath $Config).Path

$tempDir = Join-Path $ProjectPath "Temp"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

if (-not $LogFile) {
    $LogFile = Join-Path $tempDir "standard-library-generation.log"
}

$executeMethod =
"Skytomo221.Sobakasu.Tools.StandardLibraryGenerator.StandardLibraryGeneratorCommandLine.Generate"

$unityArgs = @(
    "-batchmode",
    "-quit",
    "-projectPath", $ProjectPath,
    "-executeMethod", $executeMethod,
    "-logFile", $LogFile
)

if ($NoGraphics) {
    $unityArgs += "-nographics"
}

$unityArgs += @("-standardLibraryConfig", $Config)

if ($Output) {
    $unityArgs += @("-standardLibraryOutput", [IO.Path]::GetFullPath($Output))
}

if ($Additions) {
    $unityArgs += @("-standardLibraryAdditions", [IO.Path]::GetFullPath($Additions))
}

if ($Diagnostics) {
    $unityArgs += @("-standardLibraryDiagnostics", [IO.Path]::GetFullPath($Diagnostics))
}

Write-Host "Unity:       $UnityPath"
Write-Host "Project:     $ProjectPath"
Write-Host "Method:      $executeMethod"
Write-Host "Config:      $Config"
Write-Host "Output:      $(if ($Output) { $Output } else { '<default StandardLibrary~>' })"
Write-Host "Additions:   $(if ($Additions) { $Additions } else { '<default StandardLibraryAdditions~>' })"
Write-Host "Diagnostics: $(if ($Diagnostics) { $Diagnostics } else { '<default StandardLibraryGenerationReports~>' })"
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

    $argumentLine = ($unityArgs | ForEach-Object {
            ConvertTo-NativeArgument $_
        }) -join " "

    $process = Start-Process `
        -FilePath $UnityPath `
        -ArgumentList $argumentLine `
        -WorkingDirectory $ProjectPath `
        -Wait `
        -PassThru

    $exitCode = $process.ExitCode
}

Write-Host ""

if (-not (Test-Path -LiteralPath $LogFile -PathType Leaf)) {
    Write-Error "Unity did not generate the expected log file. Exit code: $exitCode"
    exit 2
}

$log = Get-Content -LiteralPath $LogFile -Raw

if ($log -match "Sobakasu StandardLibrary~ generated at") {
    $summary = $log -split "\r?\n" |
    Where-Object {
        $_ -match "Sobakasu StandardLibrary~ generated at|Files: .*types: .*Udon API coverage:"
    } |
    Select-Object -Last 2

    Write-Host "Standard library generation completed."
    if ($summary) {
        Write-Host ""
        $summary | ForEach-Object { Write-Host $_ }
    }

    exit 0
}

Write-Error @"
Standard library generation did not report success.
Unity exit code: $exitCode

Check the log:
  $LogFile
"@

exit $(if ($exitCode -ne 0) { $exitCode } else { 2 })
