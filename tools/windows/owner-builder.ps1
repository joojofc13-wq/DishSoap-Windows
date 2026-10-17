param(
    [switch]$Worker,
    [string]$Source,
    [string]$Destination,
    [string]$Dotnet,
    [string]$Python,
    [string]$Ffmpeg,
    [string]$UpscaleEngine,
    [switch]$NoDownloads,
    [switch]$SkipImport,
    [string]$DataRoot,
    [switch]$UiCheck,
    [string]$ResultFile,
    [string]$ProgressFile,
    [string]$PreviewPath,
    [string]$UiCulture
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$localTools = Join-Path $repoRoot '.local-tools'
$builderScript = $PSCommandPath
$script:buildStage = 1
function Read-BuildLog([string]$Path) {
    # Start-Process keeps its redirected logs open for writing. ReadAllText
    # uses FileShare.Read, which conflicts with that writer on Windows.
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        $reader = New-Object IO.StreamReader($stream)
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
}
function Report-BuildStage([int]$Stage, [string]$Message, [string]$TransferFile) {
    $script:buildStage = $Stage
    # Direct, atomic status writes bypass buffering in redirected console output.
    if ($ProgressFile) {
        $path = [IO.Path]::GetFullPath($ProgressFile)
        New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($path)) | Out-Null
        $json = @{ Stage = $Stage; Message = $Message; TransferFile = $TransferFile } | ConvertTo-Json
        [IO.File]::WriteAllText($path + '.tmp', $json)
        if ([IO.File]::Exists($path)) { [IO.File]::Replace($path + '.tmp', $path, $path + '.bak') }
        else { [IO.File]::Move($path + '.tmp', $path) }
    }
    Write-Host $Message
}

function Get-VerifiedDownload([string]$Uri, [string]$Path, [string]$Hash, [string]$Algorithm = 'SHA256') {
    if ($NoDownloads) { throw ('Downloads disabled. Provide paths to .NET, Python and FFmpeg.') }
    New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($Path)) | Out-Null
    if (!(Test-Path -LiteralPath $Path)) {
        Report-BuildStage $script:buildStage ("Downloading official tool: $Uri") ($Path + '.download')
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri $Uri -OutFile ($Path + '.download')
        if ((Get-FileHash -LiteralPath ($Path + '.download') -Algorithm $Algorithm).Hash -ne $Hash) {
            throw ("Invalid hash: $Uri. File was not executed.")
        }
        Move-Item -LiteralPath ($Path + '.download') -Destination $Path
    }
    if ((Get-FileHash -LiteralPath $Path -Algorithm $Algorithm).Hash -ne $Hash) { throw ("Invalid download cache: $Path") }
}
function Find-Dotnet {
    if ($Dotnet) { return (Get-Command $Dotnet -ErrorAction Stop).Source }
    $private = Join-Path $localTools 'dotnet/dotnet.exe'
    if (Test-Path -LiteralPath $private) { return $private }
    $installed = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($installed -and ((& $installed.Source --list-sdks) -match '^8\.0\.')) { return $installed.Source }
    $archive = Join-Path $localTools 'dotnet-sdk-8.0.426-win-x64.zip'
    Get-VerifiedDownload 'https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.426/dotnet-sdk-8.0.426-win-x64.zip' $archive '10261657109f1c62126f106a9ac966739b025b5f6b4a9f88ab7c6e73cacce9aec771895daa3267b8da933a83c32ee91bf458dc6704e1ddcd9bcf0d5edd036abb' 'SHA512'
    Report-BuildStage 1 ('Preparing local .NET 8 SDK (no administrator required)...')
    Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $localTools 'dotnet') -Force
    return $private
}
function Find-Python {
    if ($Python) { return (Get-Command $Python -ErrorAction Stop).Source }
    $private = Join-Path $localTools 'python/python.exe'
    if (Test-Path -LiteralPath $private) { return $private }
    $installed = Get-Command python -ErrorAction SilentlyContinue
    if ($installed) {
        $version = & $installed.Source -c 'import sys; print(sys.version_info >= (3, 10))' 2>$null
        if ($LASTEXITCODE -eq 0 -and $version -eq 'True') { return $installed.Source }
    }
    $archive = Join-Path $localTools 'python-3.14.7-embed-amd64.zip'
    Get-VerifiedDownload 'https://www.python.org/ftp/python/3.14.7/python-3.14.7-embed-amd64.zip' $archive 'D297E5FF019966817AD8502465176139F2D3D840FA4ED84B13BED399A6AB1F15'
    Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $localTools 'python') -Force
    return $private
}
function Find-Ffmpeg {
    if ($Ffmpeg) { return (Get-Command $Ffmpeg -ErrorAction Stop).Source }
    $installed = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($installed) { return $installed.Source }
    $cached = @(Get-ChildItem -LiteralPath $localTools -Recurse -File -Filter ffmpeg.exe -ErrorAction SilentlyContinue)
    if ($cached.Count) { return $cached[0].FullName }
    if ($NoDownloads) { throw ('FFmpeg not found. Supply -Ffmpeg or use -SkipImport to build without importing.') }
    Write-Host ('Checking FFmpeg checksum from Gyan (GPLv3, separate download)...')
    $checksum = ((Invoke-WebRequest -UseBasicParsing 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256').Content.Trim() -split '\s+')[0]
    $archive = Join-Path $localTools ('ffmpeg-' + $checksum.Substring(0,12) + '.zip')
    Get-VerifiedDownload 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' $archive $checksum
    Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $localTools 'ffmpeg') -Force
    $binary = Get-ChildItem -LiteralPath (Join-Path $localTools 'ffmpeg') -Recurse -File -Filter ffmpeg.exe | Select-Object -First 1
    if (!$binary) { throw ('FFmpeg executable missing from the official package.') }
    @("Source: https://www.gyan.dev/ffmpeg/builds/", "License: GPLv3; notices and README preserved in .local-tools/ffmpeg.", "SHA256: $checksum") |
        Set-Content -LiteralPath (Join-Path $localTools 'ffmpeg-origin.txt') -Encoding UTF8
    return $binary.FullName
}

if ($Worker) {
    $workerExit = 1; $gameExe = $null; $failureMessage = $null
    Push-Location $repoRoot
    try {
        if (!$Source -or !(Test-Path -LiteralPath $Source)) { throw ('Select an existing local copy of the game.') }
        if (!$Destination) { $Destination = Join-Path $repoRoot 'dist' }
        $destinationParent = [IO.Path]::GetFullPath($Destination)
        # Always create a separate child, including in a nonempty chosen folder.
        $Destination = Join-Path $destinationParent ('Dishwasher-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,6))
        $run = Join-Path $repoRoot ('work/owner-build-' + [guid]::NewGuid().ToString('N'))
        $generatedSource = Join-Path $run 'GameSource'
        New-Item -ItemType Directory -Force $run,$localTools | Out-Null
        Report-BuildStage 1 ('1/6 Preparing local tools...')
        $sdk = Find-Dotnet
        $pythonExe = Find-Python
        # Local tools (ILSpy/MGCB apphosts) also resolve the private SDK's runtime.
        $env:DOTNET_ROOT = [IO.Path]::GetDirectoryName($sdk)
        Report-BuildStage 2 ('2/6 Extracting your copy, decompiling and adapting for Windows...')
        & (Join-Path $PSScriptRoot 'prepare-game.ps1') -Source $Source -Dotnet $sdk -Python $pythonExe -WorkDirectory $run -GameSourceRoot $generatedSource
        if ($LASTEXITCODE) { throw ('GameSource preparation failed.') }
        $metadataFile = Get-ChildItem -LiteralPath $run -Recurse -File -Filter prepared.json | Select-Object -First 1
        if (!$metadataFile) { throw ('Preparation did not produce prepared.json.') }
        $prepared = Get-Content -LiteralPath $metadataFile.FullName -Raw | ConvertFrom-Json
        $stageName = 'owner-' + [guid]::NewGuid().ToString('N')
        Report-BuildStage 3 ('3/6 Building the game for Windows x64...')
        & $sdk publish src/Dishwasher.Desktop/Dishwasher.Desktop.csproj -c Release -r win-x64 --self-contained true "-p:GameSourceRoot=$generatedSource" "-p:PublishDir=../../dist/$stageName/"
        if ($LASTEXITCODE) { throw ('Build failed. Check the log for errors.') }
        & $sdk publish src/DishSoap.Configurator/DishSoap.Configurator.csproj -c Release -r win-x64 --self-contained true "-p:PublishDir=../../dist/$stageName/"
        if ($LASTEXITCODE) { throw ('Configurator build failed.') }
        $stage = Join-Path $repoRoot ('dist/' + $stageName)
        $leaks = @(Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object {
            $_.Extension -in @('.cs','.xex','.xwb','.xsb','.zdx','.dqx','.dgx','.sav') -or
            $_.Name -eq 'game.exe' -or $_.FullName -match '[\\/](gfx|Resources|GameSource)[\\/]'
        })
        if ($leaks.Count) { throw ('Unexpected original content found in the output package.') }
        New-Item -ItemType Directory -Force $Destination | Out-Null
        Get-ChildItem -LiteralPath $stage -Force | Copy-Item -Destination $Destination -Recurse
        Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/WINDOWS_BUILD.md') -Destination $Destination
        $gameExe = Join-Path $Destination 'Dishwasher.exe'
        if (!$SkipImport) {
            if (!$prepared.ContentSource) { throw ('Game built, but this standalone game.exe has no assets. Import them with the executable.') }
            Report-BuildStage 4 ('4/6 Importing your copy and converting audio...')
            $decoder = Find-Ffmpeg
            Report-BuildStage 4 ('4/6 Importing your copy and converting audio...')
            $importArgs = @('--import', $prepared.ContentSource, '--ffmpeg', $decoder)
            if ($DataRoot) { $importArgs += @('--data-root', [IO.Path]::GetFullPath($DataRoot)) }
            & $gameExe @importArgs
            if ($LASTEXITCODE) { throw ('Build complete, but import failed. Check the log before playing.') }
            Report-BuildStage 5 ('5/6 Preparing upscaled textures...')
            $importRoot = if ($DataRoot) { [IO.Path]::GetFullPath($DataRoot) } else { Join-Path $env:LOCALAPPDATA 'DishSoapWindows' }
            $activeId = (Get-Content -LiteralPath (Join-Path $importRoot 'Config/active-content.txt') -Raw).Trim()
            if ($activeId -notmatch '^[a-fA-F0-9]{32}$') { throw ('Invalid active import identifier.') }
            $activeContent = Join-Path (Join-Path $importRoot 'Imports') $activeId
            & (Join-Path $PSScriptRoot 'upscale-game.ps1') -ContentRoot $activeContent -Dotnet $sdk -Python $pythonExe -EngineDirectory $UpscaleEngine -NoDownloads:$NoDownloads
            if ($LASTEXITCODE) { throw ('Game built, but upscaled texture preparation failed. Check the log.') }
        }
        Report-BuildStage 6 ('6/6 Complete. The game will not start automatically.')
        Write-Host ("Executable: $gameExe")
        @{ Executable = $gameExe; GameSource = $generatedSource; Source = [IO.Path]::GetFullPath($Source); Imported = !$SkipImport } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Destination 'local-build.json') -Encoding UTF8
        $workerExit = 0
    }
    catch { $failureMessage = $_.Exception.Message; [Console]::Error.WriteLine($_.ToString()) }
    finally {
        if ($ResultFile) {
            try {
                $resultPath = [IO.Path]::GetFullPath($ResultFile)
                New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($resultPath)) | Out-Null
                @{ Success = ($workerExit -eq 0); Executable = $gameExe; Error = $failureMessage } |
                    ConvertTo-Json | Set-Content -LiteralPath ($resultPath + '.tmp') -Encoding UTF8
                Move-Item -LiteralPath ($resultPath + '.tmp') -Destination $resultPath -Force
            }
            catch { $workerExit = 1; [Console]::Error.WriteLine($_.ToString()) }
        }
        Pop-Location
    }
    exit $workerExit
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$form = New-Object Windows.Forms.Form
$form.Text = ('DishSoap - Build my copy')
$form.ClientSize = New-Object Drawing.Size(780,560)
$form.StartPosition = 'CenterScreen'
$form.MinimumSize = New-Object Drawing.Size(796,599)
$label = New-Object Windows.Forms.Label
$label.SetBounds(18,16,740,44)
$label.Text = ('Select your legitimate game copy. The kit prepares and builds a Windows x64 executable locally.')
$sourceBox = New-Object Windows.Forms.TextBox
$sourceBox.SetBounds(18,70,566,25)
$sourceBox.Text = $Source
$fileButton = New-Object Windows.Forms.Button
$fileButton.SetBounds(592,68,80,28); $fileButton.Text = ('File...')
$folderButton = New-Object Windows.Forms.Button
$folderButton.SetBounds(678,68,82,28); $folderButton.Text = ('Folder...')
$outLabel = New-Object Windows.Forms.Label
$outLabel.SetBounds(18,110,740,20); $outLabel.Text = ('Save in (a game subfolder will be created here):')
$outBox = New-Object Windows.Forms.TextBox
$outBox.SetBounds(18,134,654,25)
$outBox.Text = if ($Destination) { $Destination } else { Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'DishSoap' }
$outButton = New-Object Windows.Forms.Button
$outButton.SetBounds(678,132,82,28); $outButton.Text = ('Browse...')
$note = New-Object Windows.Forms.Label
$note.SetBounds(18,175,740,42)
$note.Text = ('First build needs internet: .NET 8, Python, FFmpeg and Real-ESRGAN are downloaded if needed. Textures are upscaled 2x.')
$buildButton = New-Object Windows.Forms.Button
$buildButton.SetBounds(18,252,200,34); $buildButton.Text = ('Build my game')
$openButton = New-Object Windows.Forms.Button
$openButton.SetBounds(230,252,165,34); $openButton.Text = ('Open game folder'); $openButton.Enabled = $false
$progressBar = New-Object Windows.Forms.ProgressBar
$progressBar.SetBounds(18,220,742,21); $progressBar.Style = 'Continuous'; $progressBar.Maximum = 100
$logBox = New-Object Windows.Forms.TextBox
$logBox.SetBounds(18,302,742,237); $logBox.Multiline = $true; $logBox.ReadOnly = $true
$logBox.ScrollBars = 'Vertical'; $logBox.Anchor = 'Top,Bottom,Left,Right'
$form.Controls.AddRange(@($label,$sourceBox,$fileButton,$folderButton,$outLabel,$outBox,$outButton,$note,$buildButton,$openButton,$progressBar,$logBox))
$fileButton.Add_Click({
    $picker = New-Object Windows.Forms.OpenFileDialog
    $picker.Filter = ('XBLA package or ZIP|*.*'); $picker.Title = ('Your legitimate copy of the game')
    if ($picker.ShowDialog($form) -eq 'OK') { $sourceBox.Text = $picker.FileName }
    $picker.Dispose()
})
$folderButton.Add_Click({
    $picker = New-Object Windows.Forms.FolderBrowserDialog
    $picker.Description = ('Folder containing your game copy')
    if ($picker.ShowDialog($form) -eq 'OK') { $sourceBox.Text = $picker.SelectedPath }
    $picker.Dispose()
})
$outButton.Add_Click({
    $picker = New-Object Windows.Forms.FolderBrowserDialog
    $picker.Description = ('Choose where to create the game folder')
    if ($picker.ShowDialog($form) -eq 'OK') { $outBox.Text = $picker.SelectedPath }
    $picker.Dispose()
})
$script:workerProcess = $null
$script:stdoutLog = $null; $script:stderrLog = $null; $script:resultLog = $null; $script:progressLog = $null
$script:buildDestination = $null; $script:buildSucceeded = $false
$timer = New-Object Windows.Forms.Timer; $timer.Interval = 700
function Complete-Build([bool]$Success, [string]$Details) {
    $timer.Stop()
    $progressBar.Style = 'Continuous'
    $buildButton.Enabled = $sourceBox.Enabled = $outBox.Enabled = $true
    $fileButton.Enabled = $folderButton.Enabled = $outButton.Enabled = $true
    $script:buildSucceeded = $Success; $openButton.Enabled = $Success
    $script:workerProcess = $null
    if ($Success) {
        $progressBar.Value = 100
        $note.Text = 'Complete! Your game is ready. Opening the game folder.'
        $message = $note.Text + "`r`n`r`n" + (Join-Path $script:buildDestination 'Dishwasher.exe')
        $icon = [Windows.Forms.MessageBoxIcon]::Information
        if (!$UiCheck) {
            try { Start-Process -FilePath explorer.exe -ArgumentList ('"' + $script:buildDestination + '"') }
            catch { $logBox.AppendText("`r`n" + $_.Exception.Message) }
        }
    }
    else {
        $note.Text = 'Build failed. Check the log below.'
        $message = $note.Text + "`r`n`r`n" + $Details
        $icon = [Windows.Forms.MessageBoxIcon]::Error
    }
    if (!$UiCheck) { [void][Windows.Forms.MessageBox]::Show($form, $message, 'DishSoap', [Windows.Forms.MessageBoxButtons]::OK, $icon) }
}
function Update-BuildProgress {
    if (!$script:workerProcess) { return }
    $logText = ''
    foreach ($file in @($script:stdoutLog,$script:stderrLog)) {
        if (Test-Path -LiteralPath $file) {
            try { $logText += (Read-BuildLog $file) + "`r`n" } catch [IO.IOException] { }
        }
    }
    if ($logText -ne $logBox.Text) { $logBox.Text = $logText }
    $logBox.SelectionStart = $logBox.TextLength; $logBox.ScrollToCaret()
    $stageState = $null
    if ($script:progressLog -and (Test-Path -LiteralPath $script:progressLog)) {
        try { $stageState = (Read-BuildLog $script:progressLog) | ConvertFrom-Json } catch [IO.IOException] { } catch [ArgumentException] { }
    }
    $phases = [regex]::Matches($logText, '(?m)^\s*([1-6])/6 ([^\r\n]+)')
    if ($phases.Count -or $stageState) {
        $latest = if ($phases.Count) { $phases[$phases.Count - 1] } else { $null }
        $phase = if ($stageState) { [int]$stageState.Stage } else { [int]$latest.Groups[1].Value }
        $stageMessage = if ($stageState) { $stageState.Message } else { $latest.Value.Trim() }
        if ($phase -lt 1 -or $phase -gt 6) { throw 'Invalid build stage.' }
        $percent = @(0,0,12,35,55,70,98)[$phase]
        $progressBar.Style = if ($phase -eq 1) { 'Marquee' } else { 'Continuous' }
        if ($phase -eq 5) {
            $phaseLog = if ($latest) { $logText.Substring($latest.Index) } else { $logText }
            $textures = [regex]::Matches($phaseLog, '\[(\d+)/(\d+)\]')
            if ($textures.Count) {
                $last = $textures[$textures.Count - 1]
                $done = [int]$last.Groups[1].Value; $total = [int]$last.Groups[2].Value
                if ($total -gt 0) { $percent = if ($total -le 8) { 90 + [int](7 * $done / $total) } else { 70 + [int](19 * $done / $total) } }
            }
        }
        # This is stage progress, not a time estimate; never show 100 before success.
        $progressBar.Value = [Math]::Max($progressBar.Value, [Math]::Min(98,$percent))
        $detail = 'Stage progress; details in the log below.'
        if ($stageState.TransferFile -and (Test-Path -LiteralPath $stageState.TransferFile)) {
            $downloadSize = (Get-Item -LiteralPath $stageState.TransferFile).Length / 1MB
            $detail = ('Downloaded: {0:N1} MB') -f $downloadSize
        }
        $note.Text = $stageMessage + "`r`n" + $detail
    }
    # A worker-written result avoids relying on Start-Process.ExitCode, which
    # can be null for a redirected process. The marker is atomic and per run.
    if ($script:resultLog -and (Test-Path -LiteralPath $script:resultLog)) {
        try { $result = Get-Content -LiteralPath $script:resultLog -Raw | ConvertFrom-Json }
        catch { Complete-Build $false $_.Exception.Message; return }
        $ready = $result.Success -eq $true -and $result.Executable -and (Test-Path -LiteralPath $result.Executable)
        if ($ready) { $script:buildDestination = [IO.Path]::GetDirectoryName($result.Executable) }
        Complete-Build ([bool]$ready) $result.Error
    }
    elseif ($script:workerProcess.HasExited) {
        Complete-Build $false ('The process exited without confirming completion. Check the logs.')
    }
}
$timer.Add_Tick({ Update-BuildProgress })
$buildButton.Add_Click({
    if (!(Test-Path -LiteralPath $sourceBox.Text)) { [Windows.Forms.MessageBox]::Show(('Select your copy of the game.')); return }
    if ([string]::IsNullOrWhiteSpace($outBox.Text)) { [void][Windows.Forms.MessageBox]::Show(('Choose where to save the game.')); return }
    $logs = Join-Path $repoRoot 'work/builder-logs'
    New-Item -ItemType Directory -Force $logs | Out-Null
    $id = [guid]::NewGuid().ToString('N')
    $script:stdoutLog = Join-Path $logs ($id + '.stdout.log'); $script:stderrLog = Join-Path $logs ($id + '.stderr.log')
    $script:resultLog = Join-Path $logs ($id + '.result.json')
    $script:progressLog = Join-Path $logs ($id + '.progress.json')
    $script:buildDestination = [IO.Path]::GetFullPath($outBox.Text)
    $openButton.Enabled = $false; $script:buildSucceeded = $false; $logBox.Clear(); $progressBar.Value = 0
    $shellExe = Join-Path $PSHOME 'powershell.exe'
    if (!(Test-Path -LiteralPath $shellExe)) { $shellExe = (Get-Command powershell.exe).Source }
    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"' + $builderScript + '"'),'-Worker','-Source',('"' + $sourceBox.Text + '"'),'-Destination',('"' + $script:buildDestination + '"'),'-ResultFile',('"' + $script:resultLog + '"'),'-ProgressFile',('"' + $script:progressLog + '"'))
    try { $script:workerProcess = Start-Process -FilePath $shellExe -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $script:stdoutLog -RedirectStandardError $script:stderrLog }
    catch { Complete-Build $false $_.Exception.Message; return }
    $buildButton.Enabled = $sourceBox.Enabled = $outBox.Enabled = $fileButton.Enabled = $folderButton.Enabled = $outButton.Enabled = $false
    $note.Text = ('Building... The first run may download around 450 MB. Progress appears below.')
    $progressBar.Style = 'Marquee'; $progressBar.MarqueeAnimationSpeed = 35
    $timer.Start()
})
$openButton.Add_Click({ Start-Process -FilePath explorer.exe -ArgumentList ('"' + $script:buildDestination + '"') })
$form.Add_FormClosing({ if ($script:workerProcess -and !$script:workerProcess.HasExited) { $_.Cancel = $true; $note.Text = ('Wait for the build to finish before closing.') } })
if ($UiCheck) {
    $form.CreateControl()
    if ($form.Controls.Count -ne 12 -or !$buildButton.Enabled -or $openButton.Enabled) { throw 'Invalid initial interface state.' }
    $testFolder = Join-Path $repoRoot ('work/ui-check-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $testFolder | Out-Null
    $script:stdoutLog = Join-Path $testFolder 'stdout.log'; $script:stderrLog = Join-Path $testFolder 'stderr.log'
    $script:resultLog = Join-Path $testFolder 'result.json'
    "1/6 Tools`r`n3/6 Build`r`n5/6 Textures`r`n[64/128] texture.png" | Set-Content -LiteralPath $script:stdoutLog
    '' | Set-Content -LiteralPath $script:stderrLog
    $script:workerProcess = [pscustomobject]@{ HasExited = $false }
    Update-BuildProgress
    if ($progressBar.Value -lt 70 -or $progressBar.Value -ge 100 -or !$logBox.Text.Contains('[64/128]')) { throw 'Live progress/log polling failed' }
    Write-Host 'Builder live stage/texture progress PASS'
    $script:buildDestination = Join-Path $repoRoot 'dist/test-completion'
    if ($ResultFile) { Copy-Item -LiteralPath $ResultFile -Destination $script:resultLog }
    else { @{ Success = $true; Executable = $builderScript; Error = $null } | ConvertTo-Json | Set-Content -LiteralPath $script:resultLog }
    Update-BuildProgress
    if (!$openButton.Enabled -or !$script:buildSucceeded -or !$buildButton.Enabled -or $progressBar.Value -ne 100) { throw 'Success state failed' }
    Write-Host 'Builder success notification/button state PASS'
    @{ Success = $false; Executable = $null; Error = 'test error' } | ConvertTo-Json | Set-Content -LiteralPath $script:resultLog
    $script:workerProcess = [pscustomobject]@{ HasExited = $true }
    Update-BuildProgress
    if ($openButton.Enabled -or $script:buildSucceeded -or !$buildButton.Enabled) { throw 'Failure state failed' }
    Write-Host 'Builder failure notification/button state PASS'
    # Regression: exercise a genuinely running redirected child, not closed
    # test logs. It reproduces the sharing mode used by the actual builder.
    $streamingTest = Join-Path $testFolder 'streaming-worker.ps1'
    @'
param($StatusPath,$ResultPath,$ProbeFile)
foreach ($phase in @(1,2,3,5)) {
    @{ Stage=$phase; Message=($phase.ToString() + '/6 streaming test'); TransferFile=$null } |
        ConvertTo-Json | Set-Content -LiteralPath ($StatusPath + '.tmp') -Encoding UTF8
    Move-Item -LiteralPath ($StatusPath + '.tmp') -Destination $StatusPath -Force
    [Console]::Out.WriteLine($phase.ToString() + '/6 streaming test')
    if ($phase -eq 5) { [Console]::Out.WriteLine('[64/128] streaming texture') }
    Start-Sleep -Milliseconds 500
}
@{ Success=$true; Executable=$ProbeFile; Error=$null } | ConvertTo-Json |
    Set-Content -LiteralPath $ResultPath -Encoding UTF8
'@ | Set-Content -LiteralPath $streamingTest -Encoding UTF8
    $script:stdoutLog = Join-Path $testFolder 'stream.stdout.log'
    $script:stderrLog = Join-Path $testFolder 'stream.stderr.log'
    $script:progressLog = Join-Path $testFolder 'stream.progress.json'
    $script:resultLog = Join-Path $testFolder 'stream.result.json'
    $testShell = (Get-Command powershell.exe).Source
    $testArgs = @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"' + $streamingTest + '"'),'-StatusPath',('"' + $script:progressLog + '"'),'-ResultPath',('"' + $script:resultLog + '"'),'-ProbeFile',('"' + $builderScript + '"'))
    if ($Source) {
        # Optional integration run builds the supplied owner's managed assembly,
        # skips import/upscale and never launches or controls the game.
        $testArgs = @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"' + $builderScript + '"'),'-Worker','-Source',('"' + [IO.Path]::GetFullPath($Source) + '"'),'-Destination',('"' + (Join-Path $testFolder 'build') + '"'),'-SkipImport','-NoDownloads','-ProgressFile',('"' + $script:progressLog + '"'),'-ResultFile',('"' + $script:resultLog + '"'))
        if ($Dotnet) { $testArgs += @('-Dotnet',('"' + $Dotnet + '"')) }
        if ($Python) { $testArgs += @('-Python',('"' + $Python + '"')) }
    }
    $script:workerProcess = Start-Process -FilePath $testShell -ArgumentList $testArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $script:stdoutLog -RedirectStandardError $script:stderrLog
    $streamProcess = $script:workerProcess; $progressBar.Value = 0
    $sawLiveLog = $false; $sawLiveStage = $false; $sawSpinner = $false
    $timeout = [Diagnostics.Stopwatch]::StartNew()
    $testTimeout = if ($Source) { 180 } else { 15 }
    while ($script:workerProcess -and $timeout.Elapsed.TotalSeconds -lt $testTimeout) {
        Update-BuildProgress
        if (!$streamProcess.HasExited) {
            if (($Source -and $logBox.TextLength -gt 0) -or $logBox.Text.Contains('streaming test')) { $sawLiveLog = $true }
            if ($progressBar.Value -ge 12 -and $progressBar.Value -lt 100) { $sawLiveStage = $true }
            if ($progressBar.Style -eq 'Marquee') { $sawSpinner = $true }
        }
        [Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 40
    }
    if (!$sawLiveLog -or !$sawLiveStage -or (!$sawSpinner -and !$Source) -or !$script:buildSucceeded -or $progressBar.Value -ne 100) {
        throw "Redirected live worker regression failed: log=$sawLiveLog stage=$sawLiveStage spinner=$sawSpinner success=$script:buildSucceeded"
    }
    $streamProcess.WaitForExit()
    Write-Host 'Builder running redirected process/log/stage/completion PASS'
    if ($Source) { Write-Host 'Builder actual game compilation/live UI progress/100% completion PASS' }
    if ($PreviewPath) {
        $note.Text = 'Complete! Your game is ready.'
        $openButton.Enabled = $true
        $form.ShowInTaskbar = $false; $form.StartPosition = 'Manual'
        $form.Location = New-Object Drawing.Point(-10000,-10000); $form.Opacity = 0
        $form.Show(); [Windows.Forms.Application]::DoEvents()
        $image = New-Object Drawing.Bitmap($form.Width,$form.Height)
        $form.DrawToBitmap($image, (New-Object Drawing.Rectangle(0,0,$form.Width,$form.Height)))
        $image.Save([IO.Path]::GetFullPath($PreviewPath)); $image.Dispose(); $form.Hide()
    }
    Write-Host 'Builder UI controls PASS'
    $form.Dispose(); $timer.Dispose(); exit 0
}
[Windows.Forms.Application]::Run($form)
$timer.Dispose(); $form.Dispose()
