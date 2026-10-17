param([string]$Root)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'installation-update.ps1')
if (!$Root) { $Root=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) ('work/update-tests-'+[guid]::NewGuid().ToString('N')) }
$stage=Join-Path $Root 'stage'; $target=Join-Path $Root 'target'
New-Item -ItemType Directory -Force $stage,$target | Out-Null
foreach ($name in @('Dishwasher.exe','Dishwasher.dll','DishSoap.Configurator.exe')) {
    [IO.File]::WriteAllText((Join-Path $stage $name),'new-'+$name)
    [IO.File]::WriteAllText((Join-Path $target $name),'old-'+$name)
}
[IO.File]::WriteAllText((Join-Path $target 'player-config.txt'),'keep')
Install-GameUpdate $stage $target
if ([IO.File]::ReadAllText((Join-Path $target 'Dishwasher.dll')) -ne 'new-Dishwasher.dll' -or [IO.File]::ReadAllText((Join-Path $target 'player-config.txt')) -ne 'keep') { throw 'Update did not preserve unrelated files.' }
$backup=Get-ChildItem -LiteralPath (Join-Path $target 'UpdateBackups') -Directory | Select-Object -First 1
if ([IO.File]::ReadAllText((Join-Path $backup.FullName 'Dishwasher.dll')) -ne 'old-Dishwasher.dll') { throw 'Backup failed.' }
Write-Host 'PASS update replaces runtime files, preserves unrelated files and backs up old files'
foreach ($name in @('Dishwasher.exe','Dishwasher.dll','DishSoap.Configurator.exe')) { [IO.File]::WriteAllText((Join-Path $stage $name),'next-'+$name) }
$locked=[IO.File]::Open((Join-Path $target 'Dishwasher.exe'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
$failed=$false
try { Install-GameUpdate $stage $target } catch { $failed=$true } finally { $locked.Dispose() }
if (!$failed) { throw 'Locked destination should reject update.' }
foreach ($name in @('Dishwasher.exe','Dishwasher.dll','DishSoap.Configurator.exe')) {
    if ([IO.File]::ReadAllText((Join-Path $target $name)) -ne 'new-'+$name) { throw "Rollback failed for $name" }
}
Write-Host 'PASS failed update rolls back earlier replacements'
foreach ($relative in @('../escape','C:\escape','a/../../escape')) {
    $rejected=$false; try { $null=Get-InstallPath $target $relative } catch { $rejected=$true }
    if (!$rejected) { throw "Traversal accepted: $relative" }
}
Write-InstallationManifest $stage
$manifest=Get-Content -LiteralPath (Join-Path $stage 'installation-files.json') -Raw | ConvertFrom-Json
if (@($manifest.PSObject.Properties).Count -ne 3) { throw 'Installation manifest has unexpected files.' }
Write-Host 'PASS manifest records only staged files and package paths stay inside the target'

# Exercise release download validation with local ZIP fixtures, without network
# access or execution of downloaded scripts.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$fixtureRoot=Join-Path $Root 'release-fixture/DishSoap-Builder-v0.3.0-win-x64/tools/windows'
New-Item -ItemType Directory -Force $fixtureRoot | Out-Null
'param([string]$UpdateExisting)' | Set-Content -LiteralPath (Join-Path $fixtureRoot 'owner-builder.ps1')
$script:fixtureZip=Join-Path $Root 'fixture.zip'
[IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $Root 'release-fixture'),$script:fixtureZip)
$assetName='DishSoap-Builder-v0.3.0-win-x64.zip'
$script:releaseFixture=@{ tag_name='v0.3.0'; assets=@(@{ name=$assetName; browser_download_url="https://github.com/joojofc13-wq/DishSoap-Windows/releases/download/v0.3.0/$assetName"; digest=('sha256:'+(Get-FileHash -LiteralPath $script:fixtureZip).Hash.ToLowerInvariant()) }) }
$script:downloadCalls=0
function Invoke-RestMethod { param($Headers,$Uri) return $script:releaseFixture }
function Invoke-WebRequest { param([switch]$UseBasicParsing,$Headers,$Uri,$OutFile) $script:downloadCalls++; Copy-Item -LiteralPath $script:fixtureZip -Destination $OutFile }
function Report-BuildStage { param($Stage,$Message,$TransferFile) }
$downloaded=Get-LatestBuilder (Join-Path $Root 'downloads') '0.2.0'
if (!(Test-Path -LiteralPath $downloaded) -or $script:downloadCalls -ne 1) { throw 'Verified release download failed.' }
$current=Get-LatestBuilder (Join-Path $Root 'downloads') '0.3.0' (($script:releaseFixture.assets[0].digest -split ':')[1])
if ($current -or $script:downloadCalls -ne 1) { throw 'Up-to-date installation should not download again.' }
$republished=Get-LatestBuilder (Join-Path $Root 'downloads') '0.3.0' ('f'*64)
if (!$republished -or $script:downloadCalls -ne 2) { throw 'A replaced release ZIP should be offered again.' }
$script:releaseFixture.assets[0].digest='sha256:'+('0'*64)
$rejected=$false
try { $null=Get-LatestBuilder (Join-Path $Root 'downloads') '0.2.0' } catch { $rejected=$_.Exception.Message -like '*checksum mismatch*' }
if (!$rejected) { throw 'A modified download was accepted.' }
$script:fixtureZip=Join-Path $Root 'traversal.zip'
$zip=[IO.Compression.ZipFile]::Open($script:fixtureZip,[IO.Compression.ZipArchiveMode]::Create)
try { $null=$zip.CreateEntry('../escape.ps1') } finally { $zip.Dispose() }
$script:releaseFixture.assets[0].digest='sha256:'+(Get-FileHash -LiteralPath $script:fixtureZip).Hash.ToLowerInvariant()
$rejected=$false
try { $null=Get-LatestBuilder (Join-Path $Root 'downloads') '0.2.0' } catch { $rejected=$_.Exception.Message -like '*escapes*' }
if (!$rejected) { throw 'A path-traversal archive was accepted.' }
Write-Host 'PASS verified release extraction, current-version no-op, checksum rejection and unsafe ZIP rejection'
