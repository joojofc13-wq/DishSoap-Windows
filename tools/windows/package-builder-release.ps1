param([string]$Version = '0.2.0', [string]$Dotnet = 'dotnet', [string]$DestinationZip)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+([-.][A-Za-z0-9.-]+)?$') { throw 'Invalid version.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$name = "DishSoap-Builder-v$Version-win-x64"
if (!$DestinationZip) { $DestinationZip = Join-Path $repoRoot "dist/$name.zip" }
$DestinationZip = [IO.Path]::GetFullPath($DestinationZip)
if (Test-Path -LiteralPath $DestinationZip) { throw 'Destination ZIP already exists.' }
Push-Location $repoRoot
try {
    $id = [guid]::NewGuid().ToString('N')
    $work = Join-Path $repoRoot "work/release-$id"
    $publish = Join-Path $repoRoot "dist/builder-publish-$id"
    $stage = Join-Path $work $name
    New-Item -ItemType Directory -Force $work | Out-Null
    & (Join-Path $PSScriptRoot 'package-builder.ps1') -DestinationZip (Join-Path $work 'source.zip')
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $work 'source.zip'), $work)
    Move-Item -LiteralPath (Join-Path $work 'DishSoap-Compiler-Kit') -Destination $stage
    # Relative publish paths also work in Windows profiles containing apostrophes.
    & $Dotnet publish src/DishSoap.Builder/DishSoap.Builder.csproj -c Release -r win-x64 --self-contained true "-p:Version=$Version" "-p:PublishDir=../../dist/builder-publish-$id/"
    if ($LASTEXITCODE) { throw 'Compiled builder publication failed.' }
    $binaries = @(Get-ChildItem -LiteralPath $publish -File -Recurse | Where-Object { $_.Extension -ne '.pdb' })
    foreach ($file in $binaries) {
        $relative = $file.FullName.Substring($publish.Length + 1)
        $target = Join-Path $stage $relative
        if (Test-Path -LiteralPath $target) { throw "Publish collision: $relative" }
        New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($target)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
    # Redistributed runtimes retain the license files from the exact NuGet packs.
    $assetJson = Get-Content -LiteralPath src/DishSoap.Builder/obj/project.assets.json -Raw | ConvertFrom-Json
    $noticeRoot = Join-Path $stage 'licenses/dotnet'
    foreach ($package in @('Microsoft.NETCore.App.Runtime.win-x64','Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
        $dependency = @($assetJson.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object { $_.name -eq $package } | Select-Object -First 1)
        if ($dependency.Count -ne 1) { throw "Cannot identify runtime license package: $package" }
        $runtimeVersion = ($dependency[0].version.Trim('[',']') -split ',')[0].Trim()
        $packageDir = $null
        foreach ($folder in $assetJson.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path $folder ($package.ToLowerInvariant() + '/' + $runtimeVersion)
            if (Test-Path -LiteralPath $candidate) { $packageDir = $candidate; break }
        }
        if (!$packageDir) { throw "Runtime license package not found: $package" }
        $notices = @(Get-ChildItem -LiteralPath $packageDir -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)(\..*)?$' })
        if (!$notices.Count) { throw "Runtime license missing: $package" }
        $destination = Join-Path $noticeRoot $package
        New-Item -ItemType Directory -Force $destination | Out-Null
        $notices | Copy-Item -Destination $destination
    }
    $files = @(Get-ChildItem -LiteralPath $stage -Recurse -File)
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($stage.Length + 1).Replace('\','/')
        if ($relative -match '(^|/)(GameSource|managed|decompiled|work|dist|\.local-tools|\.git|gfx|Resources|Saves)/' -or $relative -match '\.(xex|xwb|xsb|zdx|dqx|dgx|sav|pdb)$' -or $file.Name -in @('game.exe','Dishwasher.exe','DishSoap.Configurator.exe')) {
            throw "Private content rejected: $relative"
        }
        if ($file.Extension -in @('.exe','.dll')) {
            $origin = Join-Path $publish $relative
            if (!(Test-Path -LiteralPath $origin) -or (Get-FileHash -LiteralPath $origin).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw "Unverified binary: $relative" }
        }
    }
    if (!(Test-Path -LiteralPath (Join-Path $stage 'DishSoap.Builder.exe'))) { throw 'Compiled entry point missing.' }
    $commit = (& git rev-parse HEAD).Trim()
    @("Version: $Version", "Source commit: $commit", 'Platform: Windows x64; self-contained .NET 8 builder', 'No original game files, generated GameSource or AI model weights are included.') | Set-Content -LiteralPath (Join-Path $stage 'BUILD_INFO.txt') -Encoding UTF8
    $manifest = @($files | Sort-Object FullName | ForEach-Object { ((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()) + '  ' + $_.FullName.Substring($stage.Length + 1).Replace('\','/') })
    $manifest += ((Get-FileHash -LiteralPath (Join-Path $stage 'BUILD_INFO.txt') -Algorithm SHA256).Hash.ToLowerInvariant()) + '  BUILD_INFO.txt'
    $manifest | Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding ASCII
    New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($DestinationZip)) | Out-Null
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $DestinationZip, [IO.Compression.CompressionLevel]::Optimal, $true)
    $hash = (Get-FileHash -LiteralPath $DestinationZip -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($DestinationZip))" | Set-Content -LiteralPath ($DestinationZip + '.sha256') -Encoding ASCII
    Write-Host "Compiled builder ZIP: $DestinationZip"
    Write-Host "Extracted verification folder: $stage"
    Write-Host "SHA256: $hash"
}
finally { Pop-Location }
