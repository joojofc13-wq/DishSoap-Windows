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
    if ($LASTEXITCODE) { throw 'Publicação falhou. Consulte o erro acima; GameSource é obrigatório para o jogo.' }
    if (!$SetupOnly) {
        & $Dotnet publish src/DishSoap.Configurator/DishSoap.Configurator.csproj -c Release -r win-x64 --self-contained true "-p:PublishDir=$publishRelative"
        if ($LASTEXITCODE) { throw 'Publicação do configurador falhou.' }
    }
    Copy-Item -LiteralPath docs/WINDOWS_BUILD.md -Destination (Join-Path $output 'WINDOWS_BUILD.md')
    $prohibited = @(Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object {
        $_.Extension -in @('.cs','.xex','.xwb','.xsb','.zdx','.dqx','.dgx','.sav') -or
        $_.Name -eq 'game.exe' -or $_.FullName -match '[\\/](gfx|Resources|GameSource)[\\/]'
    })
    if ($prohibited.Count) { throw 'A publicação contém arquivos que não devem ser redistribuídos.' }
    if ($GraphicsCheck) {
        & (Join-Path $output $name) --graphics-check
        if ($LASTEXITCODE) { throw 'Teste DesktopGL falhou.' }
    }
    Write-Host "Publicado: $(Join-Path $output $name)"
    if (!$SetupOnly) { Write-Host 'Build local contém lógica recompilada da sua cópia; não é um pacote público com direitos de redistribuição do jogo.' }
}
finally { Pop-Location }
