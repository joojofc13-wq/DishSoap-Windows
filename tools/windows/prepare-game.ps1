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
    if ($LASTEXITCODE) { throw 'ILSpy/MGCB restore failed.' }
    if ($sourceItem.PSIsContainer) {
        $assemblies = @(Get-ChildItem -LiteralPath $sourceItem.FullName -Recurse -File -Filter 'game.exe.xex')
        if ($assemblies.Count -eq 1) { $extracted = $assemblies[0].Directory.FullName }
        else {
            $packages = @(Get-ChildItem -LiteralPath $sourceItem.FullName -Recurse -File | Where-Object {
                $stream = [IO.File]::OpenRead($_.FullName)
                try { $bytes = New-Object byte[] 4; $n = $stream.Read($bytes,0,4); $magic = [Text.Encoding]::ASCII.GetString($bytes); $n -eq 4 -and $magic -in @('LIVE','CON ','PIRS') }
                finally { $stream.Dispose() }
            })
            if ($packages.Count -ne 1) { throw 'Select an XBLA package or a folder containing a single extracted copy.' }
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
                if ($files.Count -ne 1) { throw 'The ZIP must contain an extracted tree or a single XBLA package.' }
                $package = $files[0].FullName
            }
        }
        if (!$extracted) {
            $extracted = Join-Path $runRoot 'extracted'
            & $Python tools/stfs_extract.py $package --extract $extracted
            if ($LASTEXITCODE) { throw 'STFS extraction failed.' }
        }
    }
    if (!$managedAssembly) {
        $managed = Join-Path $runRoot 'managed'
        & $Dotnet run --project tools/windows/ManagedExtractor -- $extracted $managed
        if ($LASTEXITCODE) { throw 'XEX assembly extraction failed.' }
        $managedAssembly = Join-Path $managed 'game.exe'
        if (!(Test-Path -LiteralPath $managedAssembly)) { throw 'Managed game.exe was not found.' }
    }
    $decompiled = Join-Path $runRoot 'decompiled'
    & $Dotnet tool run ilspycmd --disable-updatecheck --nested-directories -p -o $decompiled $managedAssembly
    if ($LASTEXITCODE) { throw 'ILSpy decompilation failed.' }
    & $Python tools/windows/port-source.py $decompiled $GameSourceRoot
    if ($LASTEXITCODE) { throw 'Source transformation failed. The existing tree was preserved.' }
    Write-Host 'Desktop GameSource prepared. Android originals preserved. Run build-windows.ps1.'
    Write-Host "Decompiled input preserved at $decompiled"
    $contentSource = if ($extracted) { $extracted } else { $null }
    @{ GameSourceRoot = [IO.Path]::GetFullPath($GameSourceRoot); ContentSource = $contentSource; ManagedAssembly = $managedAssembly } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'prepared.json') -Encoding UTF8
}
finally { Pop-Location }
