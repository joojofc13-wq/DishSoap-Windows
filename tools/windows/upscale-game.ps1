param(
    [Parameter(Mandatory=$true)][string]$ContentRoot,
    [string]$Dotnet = 'dotnet',
    [string]$Python = 'python',
    [string]$EngineDirectory,
    [switch]$NoDownloads
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$ContentRoot = (Get-Item -LiteralPath $ContentRoot).FullName
if (!(Test-Path -LiteralPath (Join-Path $ContentRoot 'gfx'))) { throw 'Selecione uma importacao completa que contenha gfx/*.png.' }
Push-Location $repoRoot
try {
    if (!$EngineDirectory) { $EngineDirectory = Join-Path $repoRoot '.local-tools/realesrgan-20220424' }
    $EngineDirectory = [IO.Path]::GetFullPath($EngineDirectory)
    $engine = Join-Path $EngineDirectory 'realesrgan-ncnn-vulkan.exe'
    $model = Join-Path $EngineDirectory 'models/realesr-animevideov3-x2.bin'
    if (!(Test-Path -LiteralPath $engine) -or !(Test-Path -LiteralPath $model)) {
        if ($NoDownloads) { throw 'Real-ESRGAN ausente. Informe -EngineDirectory ou permita o download.' }
        New-Item -ItemType Directory -Force $EngineDirectory | Out-Null
        $archive = Join-Path $EngineDirectory 'upstream-20220424.zip'
        $uri = 'https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesrgan-ncnn-vulkan-20220424-windows.zip'
        Write-Host 'Baixando Real-ESRGAN NCNN Vulkan e modelos do repositorio do autor (44 MB)...'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri $uri -OutFile ($archive + '.download')
        $hash = (Get-FileHash -LiteralPath ($archive + '.download') -Algorithm SHA256).Hash
        if ($hash -ne 'ABC02804E17982A3BE33675E4D471E91EA374E65B70167ABC09E31ACB412802D') { throw 'Checksum Real-ESRGAN invalido; o arquivo nao foi executado.' }
        Move-Item -LiteralPath ($archive + '.download') -Destination $archive -Force
        Expand-Archive -LiteralPath $archive -DestinationPath $EngineDirectory -Force
        @("Origin: $uri", "SHA256: $hash", 'Engine MIT: https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan/blob/master/LICENSE',
          'Models/project: https://github.com/xinntao/Real-ESRGAN', 'Downloaded separately; not part of the source kit.') |
            Set-Content -LiteralPath (Join-Path $EngineDirectory 'DISH-SOAP-ORIGIN.txt') -Encoding UTF8
    }
    if ((Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash -ne '548A36F9C3F4AB8DA56CD3B13BADF23968BEE207B396DAD14D04B830E5F2AB2D') {
        throw 'Modelo animevideov3-x2 diferente do modelo verificado.'
    }
    & $Dotnet build tools/windows/TextureTools -c Release
    if ($LASTEXITCODE) { throw 'Falha ao compilar o processador de texturas.' }
    $pack = Join-Path $ContentRoot 'hd2x-color-safe'
    $sourceTextures = if (Test-Path -LiteralPath (Join-Path $ContentRoot 'corrected/gfx')) { Join-Path $ContentRoot 'corrected' } else { $ContentRoot }
    if ($sourceTextures -eq $ContentRoot) {
        $report = Get-Content -LiteralPath (Join-Path $ContentRoot 'import-report.json') -Raw | ConvertFrom-Json
        if ($report.textureDecoder -ne 'xbox-dxt-8in16-v2') { throw 'Reimporte sua copia com a build atual antes do upscale: a importacao antiga possui indices DXT incorretos.' }
    }
    $scratch = Join-Path $repoRoot ('work/upscale-' + [guid]::NewGuid().ToString('N'))
    & $Python tools/windows/upscale-assets.py --source $sourceTextures --destination $pack --engine $EngineDirectory --texture-tools tools/windows/TextureTools/bin/Release/net8.0-windows/TextureTools.dll --dotnet (Get-Command $Dotnet).Source --work $scratch
    if ($LASTEXITCODE) { throw 'Upscale incompleto. Os arquivos originais foram preservados.' }
    & $Dotnet tools/windows/TextureTools/bin/Release/net8.0-windows/TextureTools.dll audit $sourceTextures $pack
    if ($LASTEXITCODE) { throw 'Verificacao de dimensoes/transparencia falhou.' }
    Write-Host 'Restaurando os cenarios com modelo x4plus e supersampling para 2x...'
    & $Python tools/windows/upscale-assets.py --source $sourceTextures --destination $pack --engine $EngineDirectory --texture-tools tools/windows/TextureTools/bin/Release/net8.0-windows/TextureTools.dll --dotnet (Get-Command $Dotnet).Source --work (Join-Path $scratch 'scenes') --backgrounds
    if ($LASTEXITCODE) { throw 'Restauracao dos cenarios falhou.' }
    & $Dotnet tools/windows/TextureTools/bin/Release/net8.0-windows/TextureTools.dll audit-maps $sourceTextures (Join-Path $pack 'scenes')
    if ($LASTEXITCODE) { throw 'Verificacao dos cenarios falhou.' }
    Write-Host 'Pacote HD 2x pronto. Sera carregado na proxima inicializacao do jogo.'
}
finally { Pop-Location }
