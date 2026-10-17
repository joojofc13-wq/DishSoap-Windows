# Shared installation and release helpers. Loading this file performs no I/O.
function Get-InstallPath([string]$Root, [string]$Relative) {
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    if ([IO.Path]::IsPathRooted($Relative) -or $Relative.Contains(':')) { throw 'Invalid package path.' }
    $path = [IO.Path]::GetFullPath((Join-Path $prefix $Relative))
    if (!$path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Package path escapes its folder.' }
    return $path
}
function Assert-GameClosed([string]$Root) {
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\','/') + '\'
    foreach ($process in @(Get-Process -Name Dishwasher,DishSoap.Configurator -ErrorAction SilentlyContinue)) {
        if ($process.Path -and $process.Path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) {
            throw 'Close the game and configurator before updating. They will not be closed automatically.'
        }
    }
}
function Install-GameUpdate([string]$Stage, [string]$Target) {
    Assert-GameClosed $Target
    foreach ($name in @('Dishwasher.exe','Dishwasher.dll','DishSoap.Configurator.exe')) {
        if (!(Test-Path -LiteralPath (Join-Path $Stage $name))) { throw "Update is incomplete: $name" }
    }
    $backup = Join-Path $Target ('UpdateBackups/' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $backup | Out-Null
    $changed = New-Object 'System.Collections.Generic.List[object]'
    try {
        foreach ($file in @(Get-ChildItem -LiteralPath $Stage -Recurse -File)) {
            $relative = $file.FullName.Substring([IO.Path]::GetFullPath($Stage).TrimEnd('\').Length + 1)
            $destination = Get-InstallPath $Target $relative
            $saved = Get-InstallPath $backup $relative
            $existed = Test-Path -LiteralPath $destination
            if ($existed) {
                New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($saved)) | Out-Null
                Copy-Item -LiteralPath $destination -Destination $saved
            }
            $changed.Add(@{ Destination=$destination; Saved=$saved; Existed=$existed })
            New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($destination)) | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
            if ((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw "Update verification failed: $relative" }
        }
    }
    catch {
        $failure=$_
        $rollbackErrors=New-Object 'System.Collections.Generic.List[string]'
        for ($i=$changed.Count-1; $i -ge 0; $i--) {
            $entry = $changed[$i]
            try {
                if ($entry.Existed) {
                    if (!(Test-Path -LiteralPath $entry.Destination) -or (Get-FileHash -LiteralPath $entry.Destination).Hash -ne (Get-FileHash -LiteralPath $entry.Saved).Hash) {
                        Copy-Item -LiteralPath $entry.Saved -Destination $entry.Destination -Force
                    }
                }
                elseif (Test-Path -LiteralPath $entry.Destination) { Remove-Item -LiteralPath $entry.Destination -Force }
            } catch { $rollbackErrors.Add($_.Exception.Message) }
        }
        if ($rollbackErrors.Count) { throw "Update failed: $failure. Restore preserved files from $backup. Rollback errors: $($rollbackErrors -join '; ')" }
        throw $failure
    }
    Write-Host "Previous executable files preserved in: $backup"
}
function Write-InstallationManifest([string]$Root) {
    $hashes=@{}
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -Recurse -File)) {
        if ($file.Name -eq 'installation-files.json') { continue }
        $relative=$file.FullName.Substring([IO.Path]::GetFullPath($Root).TrimEnd('\').Length+1).Replace('\','/')
        $hashes[$relative]=(Get-FileHash -LiteralPath $file.FullName).Hash
    }
    $hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Root 'installation-files.json') -Encoding UTF8
}
function Get-LatestBuilder([string]$Cache, [string]$InstalledVersion, [string]$InstalledDigest) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $headers = @{ 'User-Agent'='DishSoap-Builder'; Accept='application/vnd.github+json' }
    $release = Invoke-RestMethod -Headers $headers -Uri 'https://api.github.com/repos/joojofc13-wq/DishSoap-Windows/releases/latest'
    $tag = [string]$release.tag_name
    if ($tag -notmatch '^v\d+\.\d+\.\d+$') { throw 'The latest release has an unsupported version.' }
    $name = "DishSoap-Builder-$tag-win-x64.zip"
    $asset = @($release.assets | Where-Object name -eq $name)
    if ($asset.Count -ne 1 -or $asset[0].browser_download_url -ne "https://github.com/joojofc13-wq/DishSoap-Windows/releases/download/$tag/$name") { throw 'The release has no matching builder ZIP.' }
    $digest = [string]$asset[0].digest
    if ($digest -notmatch '^sha256:([a-fA-F0-9]{64})$') { throw 'GitHub did not provide a SHA-256 digest for this release.' }
    $hash = $Matches[1]
    $script:latestBuilderDigest=$hash
    if ($InstalledVersion -and ([version]$InstalledVersion -gt [version]$tag.Substring(1) -or ([version]$InstalledVersion -eq [version]$tag.Substring(1) -and $InstalledDigest -eq $hash))) {
        Write-Host "Your game is already up to date ($InstalledVersion)."
        return $null
    }
    $folder = Join-Path $Cache ([guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $folder | Out-Null
    $zipPath = Join-Path $folder $name
    Report-BuildStage 1 "Downloading latest builder ($tag)..." $zipPath
    Invoke-WebRequest -UseBasicParsing -Headers $headers -Uri $asset[0].browser_download_url -OutFile $zipPath
    if ((Get-FileHash -LiteralPath $zipPath).Hash -ne $hash) { throw 'Update checksum mismatch. No downloaded code was executed.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($entry in $zip.Entries) { $null = Get-InstallPath $folder $entry.FullName }
    } finally { $zip.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($zipPath,$folder)
    $kit = Join-Path $folder "DishSoap-Builder-$tag-win-x64"
    $script = Join-Path $kit 'tools/windows/owner-builder.ps1'
    if (!(Test-Path -LiteralPath $script)) { throw 'Downloaded builder is incomplete.' }
    if (!(Select-String -LiteralPath $script -SimpleMatch '[string]$UpdateExisting' -Quiet)) { throw 'This release does not support updating an existing installation.' }
    # Reuse downloaded SDK, Python and FFmpeg without copying their caches.
    return $script
}
