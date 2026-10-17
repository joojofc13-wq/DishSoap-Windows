param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repoRoot
try {
    & $Dotnet tool restore
    if ($LASTEXITCODE) { throw 'Falha ao restaurar MGCB.' }
    Set-Location src/Dishwasher.Desktop/Content
    & $Dotnet mgcb /platform:DesktopGL /profile:Reach /outputDir:../Assets /intermediateDir:obj/DesktopGL /build:fx/redhaze.fx /build:fx/sceneEdges.fx
    if ($LASTEXITCODE) { throw 'Falha ao compilar shader desktop.' }
}
finally { Pop-Location }
