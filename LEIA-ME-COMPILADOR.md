# DishSoap — compilar sua própria cópia no Windows

Extraia este kit em uma pasta gravável, abra **Compilar-meu-jogo.cmd** e:

1. Selecione seu pacote XBLA, ZIP ou pasta contendo uma única cópia do jogo.
2. Escolha onde salvar. O compilador cria uma subpasta própria dentro da pasta
   escolhida, mesmo que ela já contenha arquivos.
3. Clique em **Compilar meu jogo**. A barra acompanha as seis etapas e a
   preparação das texturas; o log mostra os detalhes. Ao concluir, um aviso é
   exibido e a pasta com **Dishwasher.exe** abre automaticamente.
4. Opcional: abra **DishSoap.Configurator.exe**, escolha as preferências e clique
   em **Salvar configuração**. Ele só grava as opções, sem iniciar o jogo.
5. Abra **Dishwasher.exe** para jogar diretamente com as preferências salvas.

Requer Windows x64, internet na primeira compilação e GPU com Vulkan para
preparar o pacote HD. O kit obtém .NET 8, Python, FFmpeg e Real-ESRGAN dos
fornecedores quando ausentes, sem instalação administrativa.
As ferramentas ficam em `.local-tools`; a primeira execução pode baixar cerca
de 450 MB. NuGet restaura as dependências de compilação. Não é necessário Git.
Se o Windows bloquear scripts baixados, use as propriedades do ZIP para
desbloqueá-lo e extraia novamente, ou desbloqueie os arquivos conscientemente.

O kit extrai, descompila e adapta a lógica da cópia local, compila para Windows
x64, importa os recursos e prepara 128 texturas HD mais oito folhas de cenário
restauradas. A decodificação DXT do Xbox é corrigida antes da ampliação.
Não inicia uma partida nem envia
comandos ao jogo. O processamento e os logs ficam em `work`; a lógica gerada
e a build local pertencem à sua instalação. Este ZIP não contém a lógica
extraída, texturas, mapas, músicas, saves ou um executável compilado do jogo.

Não compartilhe as pastas `work`, `dist`, `.local-tools`, a saída compilada ou
seus dados importados como se fossem o kit de fontes. Compartilhe o ZIP limpo
do kit; cada pessoa deve fornecer sua própria cópia.

O perfil local dispensa Xbox LIVE. Saves ficam em
`%LOCALAPPDATA%\DishSoapWindows\Saves`, com gravação atômica e backup. O configurador
permite escolher FPS (incluindo 180), VSync, janela/tela cheia, contador,
interpolação e **Original / Upscaled**. A saída é fixa em 720p; o canvas interno
1920×1080 é reduzido para essa saída. A lógica continua em 60 Hz.
Configurador e compilador usam a região do Windows: português para Brasil/Portugal
e demais países lusófonos, espanhol para os países suportados e inglês nos
demais. Logs técnicos das ferramentas podem permanecer no idioma de origem.
O upscale v2 preserva cores/máscaras originais e melhora a luminância, evitando
ativar brilho roxo/azul por ruído de cor da rede neural.
As escolhas ficam em `%LOCALAPPDATA%\DishSoapWindows\Config\desktop.json`.
O jogo lê esse arquivo ao abrir; mudanças são aplicadas na próxima execução.
Detalhes e limites em [HD e configuração](docs/HD_LAUNCHER.md).

`-Destination` indica a pasta pai. Cada compilação gera uma subpasta
`Dishwasher-<data>-<hora>-<id>`; a pasta pai não precisa estar vazia.

## Linha de comando

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/windows/owner-builder.ps1 -Worker -Source "D:\Minha copia" -Destination "D:\Meu jogo compilado"
```

Para usar ferramentas já instaladas, informe `-Dotnet`, `-Python` e `-Ffmpeg`
com os caminhos dos executáveis, e `-UpscaleEngine` para a pasta do Real-ESRGAN
com seus modelos. `-NoDownloads` impede os downloads dessas ferramentas, mas
não o restore NuGet. `-SkipImport` compila sem importar;
depois use `Dishwasher.exe --setup`. Um `game.exe` gerenciado isolado pode
fornecer a lógica para compilar, mas os recursos completos continuam necessários.

Build/importação foram verificados com a cópia local fornecida nesta sessão.
A campanha inteira ainda não foi concluída no port; não há garantia de que
todas as fases, co-op ou funções de rede tenham sido validadas.

O FFmpeg é obtido separadamente e seus avisos são preservados no cache. O kit
usa as licenças do projeto e das dependências, descritas em LICENSE e
THIRD-PARTY-NOTICES.md. Não é afiliado à Ska Studios, Microsoft ou Xbox.
