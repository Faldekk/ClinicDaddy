[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\artifacts\ClinicDaddy-win-x64"),
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath, $projectRoot)
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
if (-not $resolvedOutputPath.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Publikacja jest dozwolona wyłącznie w podkatalogu artifacts, nigdy w katalogu projektu ani poza nim.'
}
$checkedPath = $resolvedOutputPath
while ($checkedPath -and $checkedPath.Length -ge $artifactsRoot.Length) {
    if ((Test-Path -LiteralPath $checkedPath) -and ((Get-Item -LiteralPath $checkedPath).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Katalog docelowy nie może prowadzić przez dowiązanie: $checkedPath"
    }
    $checkedPath = [IO.Path]::GetDirectoryName($checkedPath)
}

if (Test-Path -LiteralPath $resolvedOutputPath) {
    if (-not $Clean) {
        throw "Katalog docelowy już istnieje: $resolvedOutputPath. Użyj -Clean, aby go zastąpić."
    }

    Remove-Item -LiteralPath $resolvedOutputPath -Recurse -Force
}

dotnet publish (Join-Path $projectRoot 'DrugCompare.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $resolvedOutputPath
if ($LASTEXITCODE -ne 0) { throw "Publikacja nie powiodła się (kod $LASTEXITCODE)." }
foreach ($requiredFile in @('DrugCompare.exe', 'appsettings.json', 'data\medcompare.db')) {
    if (-not (Test-Path -LiteralPath (Join-Path $resolvedOutputPath $requiredFile) -PathType Leaf)) {
        throw "Niekompletna paczka: brakuje $requiredFile."
    }
}

Write-Host "Gotowa paczka: $resolvedOutputPath"
Write-Host "Przy pierwszym uruchomieniu aplikacja kopiuje bazę do %LocalAppData%\ClinicDaddy\data."
