# Windows HD e iniciador — 2026-10-16

## Resultado

`Dishwasher.exe` abre o jogo diretamente com as preferências salvas.
`DishSoap.Configurator.exe` é um configurador separado, com controles padrão
do Windows, marca **DishSoap** e título **dishwasher**. Não inicia o jogo.
A saída é fixa em **720p (1280×720)**. Configurações: janela/tela cheia, limite de
60/90/120/144/165/180/240/360 FPS, VSync, contador, interpolação e texturas
**Original / Upscaled**. O controle de volume e a legenda de teclas foram
retirados. A região do Windows seleciona português, espanhol ou inglês.
As escolhas persistem em Config/desktop.json; resoluções antigas são migradas.

O canvas interno agora é **1920×1080**. A lógica mantém coordenadas originais
1280×720. Os render targets são ampliados em 1,5×; o SpriteBatch desktop
transforma coordenadas, recortes, escalas e pivôs. Não mudamos colisões, mapas,
velocidade, alcance dos ataques ou o tamanho dos personagens para obter HD.
O canvas interno maior é reduzido para a saída 720p; não é uma opção do iniciador.
Correção de cores e alterações do compilador: [UI_COLOR_FIX.md](UI_COLOR_FIX.md).

## Defeito real nos cenários

As texturas DXT tinham os endpoints RGB565 corrigidos para Xbox, mas os índices
de cor ainda eram lidos com os bytes das palavras de 16 bits na ordem errada.
Isso trocava pares de linhas de cor dentro dos blocos 4×4 e criava o aspecto de
“zíper” nas diagonais. A correção **8-in-16** deixa as travessas das prateleiras
contínuas antes de qualquer rede neural. Corrigimos também a paleta BC3/DXT5:
ela sempre tem quatro cores, mesmo quando endpoint0 <= endpoint1.

A correção é opt-in no decoder compartilhado e usada pelo importador desktop;
o comportamento padrão Android foi preservado. Referência de formato:
[Xenia — texture dump](https://github.com/xenia-project/xenia/blob/master/src/xenia/gpu/texture_dump.cc).
As novas importações registram `textureDecoder: xbox-dxt-8in16-v2` no relatório.
Para a instalação já existente nesta máquina, geramos uma árvore `corrected`
a partir dos XNB do proprietário. Seus PNG antigos e saves foram preservados.

## Pacote de imagens

- 128/128 PNGs ampliados em 2×: 121 folhas com AnimeVideo-v3 e sete folhas de
  fontes com bicúbica, para manter as letras fiéis.
- RGB dilatado no preenchimento transparente antes da inferência; melhoria
  neural de luminância com cores originais preservadas (receita v2).
- Alpha independente, derivado da textura original corrigida, com pixels
  totalmente transparentes zerados; sem premultiplicação no carregamento.
- Oito folhas de cenário adicionais restauradas com Real-ESRGAN x4plus em 4×,
  reduzidas para 2× com supersampling. Não aumentam as dimensões do atlas em
  relação ao pacote HD padrão. A opção Upscaled usa essas folhas quando presentes.
- Fontes CJK têm atlas, glyph bounds, cropping, kerning e métricas adaptados;
  recortes e medições continuam corretos em coordenadas lógicas.

Os modelos e executáveis são baixados separadamente do
[release do autor](https://github.com/xinntao/Real-ESRGAN/releases/tag/v0.2.5.0),
com SHA256 fixo. A preparação requer GPU compatível com Vulkan e ocorre uma
vez, antes de jogar. Não há inferência neural a cada frame.
Imagens derivadas e modelos não entram no ZIP de fontes nem nos commits.

## FPS

O loop externo é variável, com limitador próprio. Um acumulador executa a
simulação original em passos de **1/60 s**. Personagens e câmera podem ser
interpolados entre os dois estados, com restauração do estado real após o
desenho. Teleportes não são interpolados. A interpolação adiciona até um passo
de atraso visual e pode ser desativada.

Contadores ligados ao desenho, como needsRender e camSplatCatchup, avançam
somente uma vez por tick da simulação. O RNG visual foi separado do RNG da
simulação; desenhar mais frames não consome a sequência usada pela lógica.
Ao perder foco o limite de render cai para 30 para reduzir consumo.

O contador opcional aparece dentro da tela, inclusive em fullscreen, com FPS
reais de desenho e frequência da lógica. VSync pode limitar o resultado à
frequência do monitor. “180” é um limite solicitado, não garantia de que todas
as cenas sustentem 180 FPS em qualquer computador.

## Validação realizada

| Verificação | Resultado |
|---|---|
| Build/publish x64 da lógica real | Sucesso, sem erros; avisos de campos do decompile |
| Testes de plataforma | 29 passaram, incluindo novas regressões BC1/BC3 |
| Movimento sintético por 10 segundos | Mesma distância e 600 ticks em 30/60/120/144/180/240 FPS |
| Travamento longo | Trabalho de catch-up limitado; não executa centenas de updates de uma vez |
| RNG | Frames extras não consomem a sequência da simulação |
| GPU HD | 139 verificações passaram: recortes, pivôs, efeitos, fonte e 128 texturas |
| Efeitos | 22 load/draw/readback sem falhas |
| Alpha e dimensões | 128 folhas HD + oito cenas verificadas pixel a pixel |
| Benchmark técnico 1080p após aquecimento | **180,0 FPS de render e 60,0 Hz de lógica**, cena sintética de três segundos na RX 6650 XT |
| Iniciador | Controles e renderização offscreen verificados; sem iniciar uma partida |

O benchmark técnico não substitui uma partida real: campanha inteira, efeitos
de todas as fases e estabilidade prolongada em 180 FPS ainda precisam de
validação manual. Não enviamos mais sequências de controles ao jogo do usuário.

## Compilar e preparar na própria máquina

**Compilar-meu-jogo.cmd** executa o fluxo completo, incluindo a correção DXT,
128 texturas HD, oito cenas e iniciador. O ZIP contém fontes do port, sem o jogo.
Para processar uma importação desktop atual manualmente:

```powershell
./tools/windows/upscale-game.ps1 -ContentRoot 'C:\caminho\da\importacao'
```

O script recusa ampliar uma importação antiga com índices DXT incorretos; nesse
caso reimporte com o executável atualizado. Para testar somente a plataforma:

```powershell
dotnet run --project tools/windows/Tests -c Release -- 'C:\pasta\nova\de\testes'
Dishwasher.exe --graphics-check --render-benchmark --data-root 'C:\pasta\de\diagnostico'
```

O jogo abre diretamente por padrão; `--play` continua aceito por compatibilidade.
O configurador também pode ser aberto explicitamente com `Dishwasher.exe --configure`.
