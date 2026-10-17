param([string]$DestinationZip)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$DestinationZip) { $DestinationZip = Join-Path ([IO.Path]::GetDirectoryName($repoRoot)) 'DishSoap-Compiler-Kit.zip' }
$DestinationZip = [IO.Path]::GetFullPath($DestinationZip)
if (Test-Path -LiteralPath $DestinationZip) { throw 'ZIP ja existe. Escolha outro nome para preservar o pacote anterior.' }
Push-Location $repoRoot
try {
    if (& git status --porcelain --untracked-files=normal) { throw 'Finalize os arquivos rastreados antes de empacotar HEAD.' }
    New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($DestinationZip)) | Out-Null
    # Only committed source. Ignored owner inputs, generated source, builds,
    # saves, tool caches and the .git directory cannot enter this archive.
    $paths = @('.gitignore','.config','README.md','LICENSE','THIRD-PARTY-NOTICES.md',
        'Compilar-meu-jogo.cmd','LEIA-ME-COMPILADOR.md','docs','tools',
        'src/Dishwasher.Desktop','src/DishSoap.Configurator','src/Dishwasher/DishwasherContentManager.cs',
        'src/Dishwasher/Platform/Import','src/Dishwasher/Platform/Audio/XactShim.cs',
        'src/Dishwasher/Platform/Shim_GamerServices.cs','src/Dishwasher/Platform/Shim_Net.cs',
        'src/Dishwasher/Platform/Shim_EffectCompat.cs','src/Dishwasher/Assets/fx',
        'src/Dishwasher/Assets/import-manifest.json')
    & git archive --format=zip --prefix=DishSoap-Compiler-Kit/ "--output=$DestinationZip" HEAD -- $paths
    if ($LASTEXITCODE) { throw 'git archive falhou.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($DestinationZip)
    try {
        $entries = @($zip.Entries | ForEach-Object { $_.FullName })
        $invalid = @($entries | Where-Object {
            $_ -match '/(GameSource|work|dist|\.local-tools|\.git|gfx|Resources)/' -or
            $_ -match '\.(exe|dll|pdb|xex|xwb|xsb|zdx|dqx|dgx|sav)$'
        })
        if ($invalid.Count) { throw ('Pacote reprovado: ' + ($invalid -join ', ')) }
        foreach ($required in @('Compilar-meu-jogo.cmd','LEIA-ME-COMPILADOR.md','tools/windows/owner-builder.ps1','src/DishSoap.Configurator/DishSoap.Configurator.csproj','src/Dishwasher.Desktop/ConfigurationWindow.cs','LICENSE')) {
            if ($entries -notcontains ('DishSoap-Compiler-Kit/' + $required)) { throw "Arquivo obrigatorio ausente: $required" }
        }
        Write-Host "Pacote verificado: $($zip.Entries.Count) entradas; sem arquivos originais do jogo."
    }
    finally { $zip.Dispose() }
    $hash = (Get-FileHash -LiteralPath $DestinationZip -Algorithm SHA256).Hash
    "$hash  $([IO.Path]::GetFileName($DestinationZip))" | Set-Content -LiteralPath ($DestinationZip + '.sha256') -Encoding ASCII
    Write-Host "ZIP: $DestinationZip"
    Write-Host "SHA256: $hash"
}
finally { Pop-Location }
