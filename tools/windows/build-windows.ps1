param([string]$Dotnet = 'dotnet', [switch]$SetupOnly, [switch]$GraphicsCheck)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$kind = if ($SetupOnly) { 'false' } else { 'true' }
$name = if ($SetupOnly) { 'DishSoap.Setup.exe' } else { 'Dishwasher.exe' }
$output = Join-Path $repoRoot ('dist/' + $(if ($SetupOnly) { 'DishSoap-Setup-Windows-x64' } else { 'DishSoap-Windows-x64' }))
# Publish overwrites its own build files; it never deletes a user directory.
Push-Location $repoRoot
try {
    # Keep PublishDir relative: .NET 8's item transforms break on an absolute
    # PublishDir containing an apostrophe (as in this user's Windows profile).
    $publishRelative = if ($SetupOnly) { '../../dist/DishSoap-Setup-Windows-x64/' } else { '../../dist/DishSoap-Windows-x64/' }
    & $Dotnet publish src/Dishwasher.Desktop/Dishwasher.Desktop.csproj -c Release -r win-x64 --self-contained true "-p:DishwasherBuildGame=$kind" "-p:PublishDir=$publishRelative"
    if ($LASTEXITCODE) { throw 'Publish failed. Check the error above; GameSource is required for the game.' }
    if (!$SetupOnly) {
        & $Dotnet publish src/DishSoap.Configurator/DishSoap.Configurator.csproj -c Release -r win-x64 --self-contained true "-p:PublishDir=$publishRelative"
        if ($LASTEXITCODE) { throw 'Settings editor publish failed.' }
    }
    Copy-Item -LiteralPath docs/WINDOWS_BUILD.md -Destination (Join-Path $output 'WINDOWS_BUILD.md')
    $prohibited = @(Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object {
        $_.Extension -in @('.cs','.xex','.xwb','.xsb','.zdx','.dqx','.dgx','.sav') -or
        $_.Name -eq 'game.exe' -or $_.FullName -match '[\\/](gfx|Resources|GameSource)[\\/]'
    })
    if ($prohibited.Count) { throw 'The published folder contains files that must not be redistributed.' }
    if ($GraphicsCheck) {
        & (Join-Path $output $name) --graphics-check
        if ($LASTEXITCODE) { throw 'DesktopGL test failed.' }
    }
    Write-Host "Published: $(Join-Path $output $name)"
    if (!$SetupOnly) { Write-Host 'The local build contains recompiled logic from your copy; it does not grant game redistribution rights.' }
}
finally { Pop-Location }
