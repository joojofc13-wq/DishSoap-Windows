# DishSoap Windows — build e execução

Requisitos: Windows x64, SDK .NET 8, Python 3, sua cópia legítima do jogo.
O Android SDK/JDK/NDK não é necessário. MonoGame DesktopGL 3.8.5.1 e NVorbis
0.10.5 são restaurados por NuGet. O executável publicado inclui o runtime .NET.

## Preparar o jogo localmente

```powershell
./tools/windows/prepare-game.ps1 -Source 'C:\copia\pacote-XBLA'
# Também aceita ZIP, pasta extraída, ou PE gerenciado game.exe com dependências ao lado.
```

O script restaura ILSpy 9.1.0.7988, extrai os assemblies XEX2 BASIC/LZX, descompila
com as referências XNA/CF originais e prepara GameSource separado para desktop.
Preserva os originais e recusa sobrescrever uma árvore GameSource existente.
O relatório port-report.json registra hashes e arquivos transformados.
Mudanças em revisões diferentes devem ser compiladas e examinadas; não há uma
promessa de transformação universal para qualquer decompile.

```powershell
./tools/windows/build-windows.ps1
# Equivalente:
dotnet publish src/Dishwasher.Desktop/Dishwasher.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishDir=../../dist/DishSoap-Windows-x64/
```

Saída: `dist/DishSoap-Windows-x64/Dishwasher.exe`. A build local contém lógica
recompilada da cópia do proprietário. Não redistribua o jogo recompilado sem os
direitos necessários. Nenhum original, GameSource ou recurso é copiado ao dist.

Para compilar somente nosso importador/diagnóstico, sem GameSource:

```powershell
./tools/windows/build-windows.ps1 -SetupOnly
```

Esse modo produz `DishSoap.Setup.exe`, não substitui o jogo por um demo.

## Importar recursos e configurar áudio

No primeiro lançamento, selecione um pacote STFS/LIVE, ZIP de recursos extraídos
ou a pasta raiz contendo `data`, `gfx`, `Resources`, `sfx`. A pasta externa que
contém apenas subpastas numéricas do Xbox precisa ser aberta até chegar ao pacote.
O arquivo sem extensão dentro de `58410902\000D0000` é uma entrada válida.

Para XMA, instale separadamente FFmpeg x64 com decoder xma2 e encoder libvorbis.
Informe seu caminho na variável `DISHWASHER_FFMPEG` ou use `--ffmpeg` na CLI.
O FFmpeg não é baixado ou incluído automaticamente no aplicativo. Uma build
do fornecedor Gyan foi usada nos testes locais; origem e licença estão em
THIRD-PARTY-NOTICES.md. PCM BE é convertido em C#; XMA é decodificado durante
a importação, não durante a partida. Reprodução usa XactShim + NVorbis/OpenAL.

```powershell
$env:DISHWASHER_FFMPEG = 'C:\ffmpeg\bin\ffmpeg.exe'
./dist/DishSoap-Windows-x64/Dishwasher.exe --import 'C:\copia\pacote'
./dist/DishSoap-Windows-x64/Dishwasher.exe
```

Dados em `%LOCALAPPDATA%\DishSoapWindows`:

| Pasta | Uso |
|---|---|
| Imports | Uma instalação nova por importação, sem misturar revisões |
| Saves | XML original do jogo, profile.sav/settings.sav e backups atômicos |
| Config | desktop.json e ponteiro da instalação ativa |
| Cache | Arquivos temporários de conversão |
| Logs | Inicialização, importação, áudio e falhas |

Uma tentativa com erro preserva a instalação anterior. Um import parcial apresenta
avisos em import-report.json e no log. Recursos indispensáveis devem ser convertidos
antes da ativação. Não apague Imports enquanto o jogo estiver aberto.

## Controles e imagem

O perfil local Player entra automaticamente; não requer Xbox LIVE.
No modo normal, teclado e gamepads SDL/MonoGame são consultados continuamente.

| Tecla | Botão de controle / função padrão |
|---|---|
| WASD ou setas | Movimento / menus |
| Enter | Confirmar / Start |
| Escape | Voltar / pausa |
| J | A (pulo / confirmar) |
| K | X (ataque) |
| L | B (agarrar / voltar) |
| I | Y (ataque forte) |
| Q / E | LB / RB |
| U / O | LT / RT |
| Alt+Enter | Alternar tela cheia |

O mouse não aciona as opções originais. Os ícones A/B na interface são os
botões originais; use Enter/J para A e Escape/L para B. Mapeamentos podem ser
alterados em Config/desktop.json com o jogo fechado.

O executável abre um iniciador com FPS, VSync, resolução de saída, janela/tela
cheia, volume, contador na tela, interpolação e qualidade Original / HD 2× /
HD 2× com cenários restaurados. Esse pedido substitui a qualidade fixa anterior.
Alt+Enter continua alternando tela cheia durante a partida. Para iniciar direto
com as preferências salvas, use `--play`.

Canvas interno 1920×1080; simulação original a 60 Hz, com render independente
e interpolação visual de personagens/câmera. 180 FPS é um limite selecionável;
VSync e desempenho podem limitar o resultado. O contador mostra FPS reais e
Hz da lógica, inclusive em tela cheia. Não há garantia de 180 estáveis em toda
a campanha. Ver [HD_LAUNCHER.md](HD_LAUNCHER.md) para arquitetura e validação.

Para compilar sua cópia com o pacote HD, abra Compilar-meu-jogo.cmd.
O fluxo completo corrige a ordem dos índices DXT, amplia 128 texturas em 2× e
restaura mais oito folhas de cenários. Fontes preservam suas métricas e os
sprites mantêm recortes/pivôs. O processamento é anterior à partida, com modelos
obtidos separadamente, e exige GPU Vulkan. O kit não contém imagens do jogo.
Veja [o guia do compilador](../LEIA-ME-COMPILADOR.md).

## Diagnóstico e testes

```powershell
Dishwasher.exe --diagnose
Dishwasher.exe --graphics-check
Dishwasher.exe --graphics-check --render-benchmark --data-root C:\diagnostico
dotnet run --project tools/windows/Tests -c Release -p:DishwasherBuildGame=false
```

`--graphics-check` abre uma pequena janela de diagnóstico e testa os 22 shaders
por carregamento/desenho/readback, além de alpha, recortes HD e fontes.
`--render-benchmark` mede por três segundos após aquecimento uma cena técnica
1080p a 180 FPS, sem iniciar uma partida. Isso não prova
fidelidade de toda a composição do jogo. Redhaze usa variante desktop sem branch
booleana problemática; recompilar com tools/windows/build-shaders.ps1.

Flags de diagnóstico de tempo/captura: --smoke-seconds N e --capture-dir PASTA.
Não são necessárias para jogar. O suporte a --input-script e à substituição
sintética de gamepad foi removido; builds novas só leem os controles reais.
Capturas derivadas do jogo devem permanecer locais e fora dos commits públicos.
