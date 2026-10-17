# DishSoap Windows — estado real em 2026-10-16

## Resultado

**Dishwasher.exe Windows x64 publicado, jogo real aberto e jogado pelo usuário.**
A versão atual acrescenta iniciador, canvas 1080p, pacote HD 2×, restauração de
cenários, correção DXT e render independente até o limite escolhido (180 incluso).
Detalhes, evidência e limites atuais: [HD_LAUNCHER.md](HD_LAUNCHER.md).
O teste técnico mediu 180,0 FPS de render e 60,0 Hz de lógica; não é benchmark
da campanha. A nova versão não foi aberta sobre a partida em andamento.
O usuário confirmou “funcionando certinho” após corrigirmos o perfil e retirarmos
a sequência automatizada de entrada. Não houve emulação nem uso do Android.

Executável atual: `dist/DishSoap-Windows-Simple/Dishwasher.exe`.
Interface 720p e correção de ruído de cor: [UI_COLOR_FIX.md](UI_COLOR_FIX.md).
Instalador independente: `dist/DishSoap-Setup-Windows-x64/DishSoap.Setup.exe`.
São publicações self-contained .NET 8; o executável do jogo é uma build local
da lógica recompilada, não uma promessa de redistribuição pública dessa lógica.

O pacote de origem fornecido pelo usuário foi reconhecido como STFS/LIVE,
125.685.760 bytes. A extração produziu 560 arquivos. Extraímos sete assemblies
gerenciados dos XEX, descompilamos 62 C# com ILSpy 9.1.0.7988 e referências
XNA/CF originais, aplicamos transformações em GameSource desktop separado.
Arquivos originais e descompilados estão ignorados pelo Git.

## Arquitetura / arquivos adicionados

- `src/Dishwasher.Desktop/Dishwasher.Desktop.csproj`: net8.0-windows,
  MonoGame DesktopGL 3.8.5.1, NVorbis 0.10.5, referências compartilhadas explícitas.
- `Program`, `DesktopBootstrap`, `DesktopGame`: entrada Windows, Game1 real,
  loop, exceções, perfil local, ciclo de vida e diagnóstico opcional.
- `Platform/DesktopPaths`, `DesktopStorage`, `AtomicSave`: LocalAppData,
  separação de dados, XML original atômico e recuperação de backup.
- `Platform/DesktopImporter`, `ImportWindow`: seleção ZIP/STFS/pasta,
  hashes, derivação fora da UI, instalação por geração e ponteiro atômico.
- `Platform/DesktopAudioDecoder`: PCM BE gerenciado e FFmpeg externo x64
  com contêiner RIFF XMA2; XactShim/NVorbis originais vinculados.
- `Platform/DesktopInput`, `DesktopSettings`, `DesktopPresentation`,
  `DesktopThreads`: teclado/SDL gamepads, configurações, escalonamento
  proporcional, filtro fixo de cenário e cancelamento cooperativo nos caminhos locais adaptados.
- `GraphicsProbe`, shader desktop `redhaze.fx` e seu XNB: teste GPU e correção GL.
- `tools/windows`: extração BASIC/LZX, preparação/decompilação/transformações,
  scripts de build/shader e testes de plataforma. LZX com licença BSD preservada.
- `.config/dotnet-tools.json`, `.gitignore`, `THIRD-PARTY-NOTICES.md`,
  `docs/WINDOWS_PORT_PLAN.md`, `docs/WINDOWS_BUILD.md`, este relatório.

O código Android não foi alterado. Shared content manager, XNB/PNG/STFS/XACT,
GamerServices, rede e EffectCompat são vinculados, não copiados indiscriminadamente.

## Compilação e comandos realmente executados

SDK 8.0.426 instalado em `work/toolchain/dotnet` do workspace da conversa;
o SDK inicial estava ausente. Python 3.14.7 e Git estavam disponíveis.

```powershell
git clone https://github.com/Hdmaniak/DishSoap.git outputs/DishSoap
dotnet tool install ilspycmd --version 9.1.0.7988 --tool-path ./work/toolchain/tools
dotnet tool restore
python tools/stfs_extract.py <pacote-local> --extract <work/original-extracted>
dotnet run --project tools/windows/ManagedExtractor -- <extraídos> <managed-repro>
dotnet tool run ilspycmd --disable-updatecheck --nested-directories -p -o <decompiled-referenced> <managed/game.exe>
python tools/windows/port-source.py <decompiled-referenced> src/Dishwasher.Desktop/GameSource
dotnet build src/Dishwasher.Desktop/Dishwasher.Desktop.csproj -c Release
dotnet run --project tools/windows/Tests -c Release -- <pasta-nova-de-testes>
dotnet publish src/Dishwasher.Desktop/Dishwasher.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishDir=../../dist/DishSoap-Windows-x64/
./tools/windows/build-windows.ps1 -Dotnet <SDK-local> -SetupOnly
Dishwasher.exe --import <pacote-local> --ffmpeg <FFmpeg-local>
Dishwasher.exe --graphics-check --data-root <work/graphics-final>
```

Os marcadores representam os caminhos locais reais usados; logs integrais foram
preservados no work do workspace. O comando `dotnet` foi executado pelo caminho
completo do SDK isolado. Build/publish do jogo: **sucesso, zero erros**. Permanecem
avisos de campos inutilizados do decompile e do upstream. O instalador independente
compila com dois avisos upstream, zero erros. Nenhuma referência Android carregada.

## Evidência de testes

| Área | Resultado observado |
|---|---|
| Importação real | **458 matched, 0 differed, 0 missing** |
| Áudio importado | **222/222**, 208 WAV + 14 OGG, 216 cues |
| Extração gerenciada | Sete assemblies; segunda extração byte-idêntica |
| GPU | AMD Radeon RX 6650 XT, DesktopGL Reach |
| Shaders | **22/22** load/draw/readback após corrigir redhaze |
| Straight alpha | Pixel GPU **(100,50,25)** para entrada **(200,100,50,128)** |
| Apresentação | Apresentação fixa e filtro de diagonais passaram readback GPU |
| Plataforma | **29 verificações passaram**, incluindo paths, input, XML atômico, corrupção, ARGB BE, PCM, XMA2 e fit |
| Inicialização | Game1 Initialize/LoadContent e carregamento real concluídos |
| Menus | Capturas reais de menu principal, dificuldade e seleção de fase |
| Perfil | Player local; read=0 após reinício; profile.sav/settings.sav persistidos |
| Partida | Usuário jogou e confirmou funcionamento; logs de foot, dishjab, block, land, pistol e impactos |
| Música | Eventos do próprio jogo: soft1 e depois **music2**, instâncias reproduzindo |
| Encerramento | Logs de commit dos dois saves e exit playSeconds=118 |

Os testes de plataforma usam dados sintéticos, não substituem testes de campanha.
As capturas foram feitas do framebuffer do jogo, não de um demo. O suporte a roteiros de entrada foi removido do código depois de interferir
novamente na partida manual. Não serão executados testes automáticos que
controlem a partida do usuário. A instância normal aberta não tem argumentos.

## Ajuste visual solicitado pelo usuário

O iniciador atual oferece saída nativa ou resolução escolhida, janela/tela cheia,
FPS, VSync, contador, volume, interpolação e três qualidades. O canvas agora é
1920×1080; a lógica original mantém coordenadas e passo de 60 Hz.
128 folhas foram ampliadas em 2× e oito folhas de cenário receberam restauração
extra x4plus com redução para 2×. Preservamos transparência, UVs, pivôs e métricas.

Descobrimos a causa do denteado nas diagonais: os índices de cor DXT precisavam
do mesmo swap 8-in-16 dos endpoints. Corrigimos também a paleta de quatro cores
BC3. A comparação da prateleira corrigida mostra travessas contínuas sem depender
de IA. A correção é selecionada pelo desktop; a chamada padrão Android continua
com o comportamento anterior. Veja [HD_LAUNCHER.md](HD_LAUNCHER.md).

## Compilador para quem possui o jogo

Compilar-meu-jogo.cmd abre a interface Windows para preparar a própria cópia,
compilar, importar e gerar os dois pacotes HD. Ferramentas faltantes ficam em cache
privado, com downloads verificados. Não inclui GameSource ou imagens do jogo.
As escolhas visuais e de FPS são feitas no iniciador depois de compilar.

O primeiro fluxo completo com HD 128/128 foi executado a partir da cópia fornecida,
sem GameSource prévio. Importação em data-root isolado, sem alterar a partida.
Regressões do decoder e relógio, renderização HD, transparência e benchmark
foram verificados. O compilador não inicia a partida automaticamente.
Guia: [LEIA-ME-COMPILADOR.md](../LEIA-ME-COMPILADOR.md).

## Bugs encontrados e corrigidos

1. XEX unwrap não fornecido upstream: implementada extração local BASIC/LZX.
2. Descompilar sem os assemblies XNA/CF gerava C# inválido. Referências extraídas
   junto com game.exe resolvem esse problema; nenhuma adaptação cega desse lixo.
3. Duas incompatibilidades restantes: Color byte/int ambíguo e parâmetro removido
   de UnloadContent; transformações reproduzíveis adicionadas.
4. Redhaze pré-compilado emitia `if(vec4)` no GLSL desktop. Variante aritmética
   preserva parâmetros bool, taps, tint e comportamento, sem branch problemática.
5. Perfil console não selecionado e slots sem Gamer local: perfil local automático.
6. Um XMA usa **7.995 Hz**. Validação corrigida e resampling para faixa OpenAL;
   resultado de áudio melhorou de 221/222 para 222/222.
7. Fullscreen original apresentava apenas região 1280×720 em framebuffer maior:
   canvas lógico + composição proporcional independente da janela.
8. PublishDir absoluto com apóstrofo no nome de usuário quebra item transform do
   SDK .NET 8 (MSB3094). Script usa PublishDir relativo e foi executado com sucesso.
9. Janela de teste automatizado bloqueava a entrada manual; teste encerrado e
   instância normal aberta. Uma inicialização oculta causou áudio sem imagem;
   foi encerrada e relançada com janela normal. Esses dois eram problemas do
   procedimento de teste, não evidência de falha permanente dos controles.
10. Escrita direta de saves substituída por staging/validação XML/File.Replace,
    preservando formato e backup; recuperação conserva o arquivo corrompido.
11. Espera de armazenamento Xbox/Thread.Abort substituída pelo shim síncrono;
    loaders locais têm cancelamento cooperativo nos caminhos adaptados.

## Limites e próximos testes

- Não foi concluída a campanha inteira, nem validadas todas as transições,
  co-op, LAN, achievements ou placares. LIVE é shim; não é acesso ao serviço.
- A evidência de gameplay inclui uso real do usuário e áudio do jogo. Não é uma
  comparação visual exaustiva com o Xbox 360.
- Gamepad do usuário respondeu no teste manual. DualShock 4, hotplug e múltiplos
  controles específicos ainda precisam de validação física; dependem do SDL.
- Fit 1080p/1440p verificado matematicamente; todas as fases nessas resoluções
  e resize/Alt+Enter durante efeitos complexos ainda não foram auditados.
- Simulação em 60 Hz e limites de render no iniciador. A interpolação cobre
  personagens/câmera; animações originais continuam com a cadência da lógica.
  Desempenho e efeitos de todas as fases em 180 FPS ainda precisam de partida manual.
- Reimportações e tentativas falhas são preservadas; não há limpeza automática.
- O importador isolado requer FFmpeg; o compilador obtém a ferramenta separadamente quando ausente.
- Sem arquivos externos faltando para jogar a cópia já importada neste computador.

**Maior prioridade técnica:** testar uma sequência completa de fases com saves,
a versão HD e limites de FPS, comparando com uma referência
Xbox; depois validar controles adicionais e co-op. Os marcos A–C foram atingidos;
D tem partida real confirmada, mas a campanha inteira não foi certificada;
E tem publicação local x64, sem uma distribuição pública licenciada do jogo.
