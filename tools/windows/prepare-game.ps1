param(
    [Parameter(Mandatory=$true)][string]$Source,
    [string]$Dotnet = 'dotnet',
    [string]$Python = 'python',
    [string]$WorkDirectory,
    [string]$GameSourceRoot
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$WorkDirectory) { $WorkDirectory = Join-Path $repoRoot 'work' }
if (!$GameSourceRoot) { $GameSourceRoot = Join-Path $repoRoot 'src/Dishwasher.Desktop/GameSource' }
$runRoot = Join-Path ([IO.Path]::GetFullPath($WorkDirectory)) ('prepare-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $runRoot | Out-Null
$sourceItem = Get-Item -LiteralPath $Source
Push-Location $repoRoot
try {
    & $Dotnet tool restore
    if ($LASTEXITCODE) { throw 'Falha ao restaurar ILSpy/MGCB.' }
    if ($sourceItem.PSIsContainer) {
        $assemblies = @(Get-ChildItem -LiteralPath $sourceItem.FullName -Recurse -File -Filter 'game.exe.xex')
        if ($assemblies.Count -eq 1) { $extracted = $assemblies[0].Directory.FullName }
        else {
            $packages = @(Get-ChildItem -LiteralPath $sourceItem.FullName -Recurse -File | Where-Object {
                $stream = [IO.File]::OpenRead($_.FullName)
                try { $bytes = New-Object byte[] 4; $n = $stream.Read($bytes,0,4); $magic = [Text.Encoding]::ASCII.GetString($bytes); $n -eq 4 -and $magic -in @('LIVE','CON ','PIRS') }
                finally { $stream.Dispose() }
            })
            if ($packages.Count -ne 1) { throw 'Selecione o pacote XBLA ou uma pasta contendo uma única cópia extraída.' }
            $sourceItem = $packages[0]
        }
    }
    if (!$sourceItem.PSIsContainer -and $sourceItem.Extension -eq '.exe') { $managedAssembly = $sourceItem.FullName }
    elseif (!$sourceItem.PSIsContainer) {
        $package = $sourceItem.FullName
        if ($sourceItem.Extension -eq '.zip') {
            $zipRoot = Join-Path $runRoot 'zip'
            Expand-Archive -LiteralPath $package -DestinationPath $zipRoot
            $candidates = @(Get-ChildItem -LiteralPath $zipRoot -Recurse -File | Where-Object { $_.Name -eq 'game.exe.xex' })
            if ($candidates.Count -eq 1) { $extracted = $candidates[0].Directory.FullName }
            else {
                $files = @(Get-ChildItem -LiteralPath $zipRoot -Recurse -File)
                if ($files.Count -ne 1) { throw 'ZIP precisa conter a árvore extraída ou um pacote XBLA único.' }
                $package = $files[0].FullName
            }
        }
        if (!$extracted) {
            $extracted = Join-Path $runRoot 'extracted'
            & $Python tools/stfs_extract.py $package --extract $extracted
            if ($LASTEXITCODE) { throw 'Falha ao extrair STFS.' }
        }
    }
    if (!$managedAssembly) {
        $managed = Join-Path $runRoot 'managed'
        & $Dotnet run --project tools/windows/ManagedExtractor -- $extracted $managed
        if ($LASTEXITCODE) { throw 'Falha ao extrair assemblies do XEX.' }
        $managedAssembly = Join-Path $managed 'game.exe'
        if (!(Test-Path -LiteralPath $managedAssembly)) { throw 'game.exe gerenciado não encontrado.' }
    }
    $decompiled = Join-Path $runRoot 'decompiled'
    & $Dotnet tool run ilspycmd --disable-updatecheck --nested-directories -p -o $decompiled $managedAssembly
    if ($LASTEXITCODE) { throw 'Falha na descompilação ILSpy.' }
    & $Python tools/windows/port-source.py $decompiled $GameSourceRoot
    if ($LASTEXITCODE) { throw 'Falha nas transformações. A árvore existente foi preservada.' }
    Write-Host 'GameSource desktop preparado. Originais Android preservados. Execute build-windows.ps1.'
    Write-Host "Entrada descompilada preservada em $decompiled"
    $contentSource = if ($extracted) { $extracted } else { $null }
    @{ GameSourceRoot = [IO.Path]::GetFullPath($GameSourceRoot); ContentSource = $contentSource; ManagedAssembly = $managedAssembly } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'prepared.json') -Encoding UTF8
}
finally { Pop-Location }
