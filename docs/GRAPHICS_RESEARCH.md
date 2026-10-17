# Pesquisa de qualidade visual — 2026-10-16

A investigação posterior mostrou que parte importante do serrilhado vinha da
conversão DXT: pares de linhas de cor eram trocados dentro dos blocos 4×4.
Corrigimos a ordem 8-in-16 e a paleta BC3 no caminho desktop. Isso restaura as
diagonais antes de qualquer upscale. A versão atual tem canvas 1920×1080,
128 texturas 2× e oito cenários com restauração adicional. O iniciador permite
comparação e ajuste. Ver [HD_LAUNCHER.md](HD_LAUNCHER.md).

## Repositórios examinados

| Projeto | Uso possível | Avaliação para este port |
|---|---|---|
| [Real-ESRGAN ncnn Vulkan](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan) | Restaurar/ampliar PNGs localmente, em lote, usando Vulkan | Integrado à preparação local: AnimeVideo-v3 em 121 folhas e x4plus em oito cenários; alpha independente e adaptação de UVs. Binários/modelos baixados separadamente, sem inferência durante a partida. |
| [FidelityFX FSR 1](https://github.com/GPUOpen-Effects/FidelityFX-FSR) | Upscaling espacial e sharpening | Útil para apresentação, mas não reconstrói a textura original. Exemplos DX12/Vulkan exigem adaptação para o caminho DesktopGL/MonoGame. Repositório original sob MIT; não confundir com licenças de outros SDKs. Não integrado. |
| [Anime4K](https://github.com/bloc97/Anime4K) | Upscaling em tempo real de vídeo anime | O autor prioriza anime 1080p de boa qualidade; não otimiza para fontes pequenas/degradadas. Minha avaliação é que pode alterar excessivamente o traço sujo do jogo. Não integrado. |

O shader `sceneEdges.fx` desta alteração é código próprio, sem copiar esses
projetos. Ele usa gradiente de luminância, ponderação diagonal e amostras ao
longo da borda. Áreas uniformes, eixos e alpha são preservados; não é um
modelo neural. O pacote HD é gerado separadamente; o filtro é desativado
quando as texturas HD estão ativas para não suavizar duas vezes.

Teste GPU sintético: 310 pixels de uma escada diagonal foram suavizados,
com áreas uniformes preservadas. Os 22 efeitos passaram load/draw/readback
na RX 6650 XT. Isso valida o funcionamento técnico do filtro, mas não certifica
a aparência de todos os cenários. A comparação visual das fases depende de
partidas manuais; não usamos mais roteiros de entrada no jogo do usuário.
