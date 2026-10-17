param([Parameter(Mandatory=$true)][string]$ContentRoot,[string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'installation-update.ps1')
$ContentRoot=[IO.Path]::GetFullPath($ContentRoot)
$pack=Join-Path $ContentRoot 'hd2x-color-safe'
if (!(Test-Path -LiteralPath (Join-Path $pack 'manifest.json'))) { throw 'A complete color-safe upscaled texture pack is needed for lettering restoration.' }
if (Get-Process -Name Dishwasher -ErrorAction SilentlyContinue) { throw 'Close the game before restoring shared imported textures. The game will not be closed automatically.' }
$baselinePath=Join-Path $ContentRoot 'content-integrity.json'
$baseline=$null
if (Test-Path -LiteralPath $baselinePath) {
    $baseline=Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    foreach ($entry in $baseline.PSObject.Properties) {
        $path=Get-InstallPath $ContentRoot $entry.Name
        if (!(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -ne $entry.Value) { throw ('Existing content failed verification before lettering restoration: '+$entry.Name) }
    }
}
$pending=New-Object 'System.Collections.Generic.List[object]'
$run=Join-Path $repoRoot ('work/lettering-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $run | Out-Null
Push-Location $repoRoot
try {
    & $Dotnet build tools/windows/TextureTools -c Release -v quiet
    if ($LASTEXITCODE) { throw 'Lettering tool build failed.' }
    $tool=Join-Path $repoRoot 'tools/windows/TextureTools/bin/Release/net8.0-windows/TextureTools.dll'
    foreach ($name in @('manifest.json','scenes.json')) {
        $manifestPath=Join-Path $pack $name
        if (!(Test-Path -LiteralPath $manifestPath)) { continue }
        $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $changed=$false
        foreach ($entry in $manifest.Textures) {
            $asset=$entry.Asset+'.png'
            if ($asset -notin @('gfx/maps/maps3.png','gfx/panels/panel8.png') -or $entry.Method -like '*reviewed-lettering-v1*') { continue }
            $original=Get-InstallPath $ContentRoot $asset
            $target=Get-InstallPath $pack $entry.File
            if ((Get-FileHash -LiteralPath $original).Hash -ne $entry.SourceSha256 -or (Get-FileHash -LiteralPath $target).Hash -ne $entry.Sha256) { throw ('Texture checksum mismatch before lettering restoration: '+$asset) }
            $staged=Join-Path $run ([guid]::NewGuid().ToString('N')+'.png')
            & $Dotnet $tool lettering $original $target $staged $asset
            if ($LASTEXITCODE) { throw ('Lettering restoration failed: '+$asset) }
            & $Dotnet $tool lettering-check $target $staged $asset
            if ($LASTEXITCODE) { throw ('Lettering audit failed: '+$asset) }
            $entry.Sha256=(Get-FileHash -LiteralPath $staged).Hash.ToLowerInvariant()
            $entry.Method += '+reviewed-lettering-v1'
            $pending.Add(@{ Target=$target; Staged=$staged })
            $changed=$true
        }
        if ($changed) {
            $staged=Join-Path $run $name
            $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $staged -Encoding UTF8
            $pending.Add(@{ Target=$manifestPath; Staged=$staged })
        }
    }
    if (!$pending.Count) { Write-Host 'Reviewed lettering is already up to date.'; return }
    if ($baseline) {
        foreach ($file in $pending) {
            $relative=$file.Target.Substring($ContentRoot.TrimEnd('\').Length+1).Replace('\','/')
            $baseline | Add-Member -NotePropertyName $relative -NotePropertyValue ((Get-FileHash -LiteralPath $file.Staged).Hash) -Force
        }
        $staged=Join-Path $run 'content-integrity.json'
        $baseline | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $staged -Encoding UTF8
        $pending.Add(@{ Target=$baselinePath; Staged=$staged })
    }
    # Preserve every earlier file before changing any active texture or manifest.
    $i=0
    foreach ($file in $pending) {
        $file.Backup=Join-Path $run ('backup-'+(++$i)+[IO.Path]::GetExtension($file.Target))
        Copy-Item -LiteralPath $file.Target -Destination $file.Backup
    }
    try {
        foreach ($file in $pending) {
            Copy-Item -LiteralPath $file.Staged -Destination $file.Target -Force
            if ((Get-FileHash -LiteralPath $file.Target).Hash -ne (Get-FileHash -LiteralPath $file.Staged).Hash) { throw 'Restored lettering could not be verified.' }
        }
    } catch {
        $failure=$_; $failures=New-Object 'System.Collections.Generic.List[string]'
        foreach ($file in $pending) { try { Copy-Item -LiteralPath $file.Backup -Destination $file.Target -Force } catch { $failures.Add($_.Exception.Message) } }
        if ($failures.Count) { throw "Lettering update failed. Restore backups from $run. $($failures -join '; ')" }
        throw $failure
    }
    Write-Host "Reviewed machine and neon lettering restored. Backups: $run"
} finally { Pop-Location }
