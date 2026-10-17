param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repoRoot
try {
    & $Dotnet tool restore
    if ($LASTEXITCODE) { throw 'MGCB restore failed.' }
    Set-Location src/Dishwasher.Desktop/Content
    & $Dotnet mgcb /platform:DesktopGL /profile:Reach /outputDir:../Assets /intermediateDir:obj/DesktopGL /build:fx/redhaze.fx /build:fx/sceneEdges.fx
    if ($LASTEXITCODE) { throw 'Desktop shader compilation failed.' }
}
finally { Pop-Location }
