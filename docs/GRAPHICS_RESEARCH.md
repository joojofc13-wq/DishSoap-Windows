# Graphics tools considered for Windows

The current pipeline uses [Real-ESRGAN](https://github.com/xinntao/Real-ESRGAN) through the [NCNN Vulkan implementation](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan). AnimeVideo-v3 provides the main 2x art pass; x4plus provides the background restoration pass. The engine and models are obtained separately and keep their own licenses.

[Anime4K](https://github.com/bloc97/Anime4K) and [AMD FidelityFX FSR](https://github.com/GPUOpen-Effects/FidelityFX-FSR) were considered as reference options. Neither implementation is integrated or copied into this port. The port currently uses its own canvas/texture adapters and scene filtering.

Texture decoding must be correct before enhancement. The desktop DXT fix corrects color-index ordering and the BC3 palette. AI output also needs source alpha and color relationships because the game uses channels as effect masks. Increasing dimensions alone does not fix either problem.

Local GPU diagnostics exercise the rewritten shader set, atlas sampling, fonts and transparency. They are isolated checks rather than proof of campaign-wide visual accuracy. See [rendering](HD_LAUNCHER.md) and [color-mask fixes](UI_COLOR_FIX.md).
