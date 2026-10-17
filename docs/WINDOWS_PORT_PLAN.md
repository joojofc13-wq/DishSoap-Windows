# DishSoap Windows — plano de implementação (2026-10-16)

Plano inicial, registrado antes da implementação. O usuário forneceu o pacote
durante o trabalho; os bloqueios iniciais foram resolvidos e a arquitetura evoluiu.
Resultados atuais em [WINDOWS_PORT_STATUS.md](WINDOWS_PORT_STATUS.md).

Base: repositório público Hdmaniak/DishSoap, branch master. Foram lidos README,
BUILDING, PROJECT_STATE, PORT_NOTES e o projeto Android; examinados bootstrap,
content manager, shims, importador STFS/XNB/XACT e ponte nativa de áudio.

## Constatações

- Não existe GameSource nem cópia original no workspace. O usuário confirmou que
  ainda irá obter sua cópia. A implementação do jogo e seus testes dependem disso.
- O unwrap de game.exe.xex para um PE gerenciado não está no repositório.
  STFS apenas extrai o contêiner: não efetua esse unwrap.
- C# reutilizável: importador, decodificador XNB, leitor STFS/XACT, PNG,
  DishwasherContentManager, XactShim, GamerServices, rede e EffectCompat.
- Android exclusivo: Activity, SAF, Java/JNI, rumble, touch, configurações/menu,
  bootstrap, logger e libdishaudio ARM64. Não serão vinculados ao desktop.
- Existem 21 fontes e 21 XNB de shaders (19 efeitos documentados + dois Sprite).
  OPENGL usa shader model 3; MatrixTransform e cor dos vértices estão preservados.
  Validar carregamento real dos 21 no DesktopGL antes de reutilizar os binários.
- Ambiente inicial: Git e runtimes .NET presentes; SDK, NuGet CLI e FFmpeg ausentes.
  SDK .NET 8 instalado isoladamente em work/toolchain; NuGet via dotnet restore.

## Arquitetura escolhida

Projeto independente net8.0-windows, DesktopGL fixado à versão do upstream,
WinForms apenas para seleção de arquivos e interface de importação. Arquivos
compartilhados vinculados explicitamente; nenhum projeto Android referenciado.
Não há necessidade de mover código ou alterar o funcionamento Android.
WindowsDX exigiria recompilar efeitos OpenGL e não oferece vantagem inicial.

Build normal exige GameSource real. Build explícito DishwasherBuildGame=false
produz DishSoap.Setup.exe, importador/diagnóstico, nunca uma simulação do jogo.
Compilar este modo valida a plataforma compartilhada, não atinge o Marco B.

Áudio: substituir a ponte ARM64 por backend C# que prepara RIFF XMA2 e invoca um
FFmpeg externo configurável, preservando o XactShim/NVorbis. PCM BE convertido
diretamente. Não distribuir FFmpeg automaticamente; registrar versão/configuração.
Importação ocorre fora da thread da interface e em staging novo por tentativa.
Ativação transacional por ponteiro; não misturar revisões ou sobrescrever saves.

## Ordem e bloqueios

1. Implementar caminhos, logs, importador desktop, configuração e áudio.
2. Compilar/publish do instalador e testar contratos de importação com dados sintéticos.
3. Validar efeitos em um dispositivo DesktopGL real através de diagnóstico explícito.
4. Preparar ILSpy 9.1.0.7988 e transformações conservadoras em árvore desktop própria.
5. Quando fornecidos assembly/recursos: decompilar, resolver erros concretos, ligar
   Game1, validar content manager/matrizes/blend/lense, menus, áudio e saves.
6. Só então declarar Marcos B–D e publicar Dishwasher.exe jogável.

Não oferecer FPS de simulação acima de 60 antes de medir a lógica original.
Resolução/redimensionamento precisam de integração com render targets do Game1;
o host inicialmente mantém a janela fixa para preservar esse contrato.
