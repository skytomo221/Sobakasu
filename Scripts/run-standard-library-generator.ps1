param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$Catalog,
    [string]$Config,
    [string]$Output,
    [string]$Additions,
    [string]$Diagnostics
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet was not found in PATH."
}
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$launcherProject = Join-Path $ProjectPath "Sobakasu.StandardLibraryGenerator.Standalone.csproj"
if (-not (Test-Path -LiteralPath $launcherProject -PathType Leaf)) {
    throw "Standalone Standard Library Generator project was not found: $launcherProject"
}

function Resolve-ProjectPath([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    if (-not [IO.Path]::IsPathRooted($Value)) { $Value = Join-Path $ProjectPath $Value }
    return [IO.Path]::GetFullPath($Value)
}

$arguments = @("--project", $ProjectPath)
foreach ($pair in @(
    @("--catalog", $Catalog),
    @("--config", $Config),
    @("--output", $Output),
    @("--additions", $Additions),
    @("--diagnostics", $Diagnostics)
)) {
    if (-not [string]::IsNullOrWhiteSpace($pair[1])) {
        $arguments += $pair[0]
        $arguments += Resolve-ProjectPath $pair[1]
    }
}

Push-Location $ProjectPath
try {
    & dotnet run --project $launcherProject -- @arguments
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
