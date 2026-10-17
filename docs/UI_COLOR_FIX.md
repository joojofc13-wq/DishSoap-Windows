# Interface, compilador e cores — 2026-10-16

O configurador separado `DishSoap.Configurator.exe` mostra DishSoap com controles
padrão do Windows; título da janela e do jogo: `dishwasher`. O botão Salvar
configuração grava as preferências e fecha; não inicia o jogo.
`Dishwasher.exe` lê `Config/desktop.json` e abre diretamente a partida/menu do jogo.
Somente Original e Upscaled; saída 1280×720, sem resolução automática ou
redimensionamento da janela. O supersampling interno 1920×1080 foi preservado.
Sem controle de volume nem legenda de comandos. O volume já salvo é mantido.

O país/região configurado em `HKCU/Control Panel/International/Geo/Name`
determina português, espanhol ou inglês. Se essa informação não estiver
disponível, usa o formato regional com inglês como fallback. Não altera a
configuração do Windows. Os testes de UI permitem sobrescrever o idioma somente
durante `--launcher-check --ui-culture ...` ou `-UiCheck -UiCulture ...`.

## Compilador

O destino escolhido é uma pasta pai, que pode conter arquivos. Cada execução
cria sua própria subpasta `Dishwasher-yyyyMMdd-HHmmss-id`. A interface acompanha
as seis etapas e os contadores de texturas, mostra o log e só chega a 100% após
o resultado de sucesso. Isso representa progresso por etapas, não tempo previsto.

O worker grava um resultado JSON atômico separado por execução. A interface
não depende de ExitCode potencialmente nulo de Start-Process com redirecionamento.
Sucesso exige resultado positivo e executável existente: habilita os botões,
abre a pasta efetiva do executável e exibe um aviso. Falha exibe aviso e detalhes;
não abre a pasta como se o jogo estivesse pronto. Nunca inicia o jogo sozinho.

### Correção do progresso com compilação em andamento

`Start-Process` mantém os arquivos de stdout/stderr abertos para escrita.
`File.ReadAllText` tentava abrir esses arquivos com compartilhamento apenas de
leitura, gerando erro de arquivo em uso. Como a interface ignorava esse erro,
o log e a barra ficavam sem atualizações durante a compilação real.

A leitura agora usa `FileShare.ReadWrite | FileShare.Delete`. As etapas também
são gravadas diretamente num JSON atômico, sem depender do buffer de stdout.
A preparação inicial mostra uma barra animada; downloads informam MB recebidos
quando o arquivo está disponível. Depois, a barra acompanha etapas e texturas,
chegando a 100% somente após a confirmação de sucesso.

A regressão executa um processo filho real com redirecionamento de stdout e
stderr, verifica atualizações antes de ele terminar e testa o aviso de conclusão.
`-UiCheck -Source <game.exe> -Dotnet <dotnet.exe> -Python <python.exe>` também
executa a compilação real com a cópia fornecida, sem importar recursos nem
iniciar o jogo. O teste não tenta atualizar uma janela antiga que já estava aberta.

## Manchas roxas/azuis

O shader bloom original do port testa `B > R` e usa o canal azul como máscara
de emissão. No pacote neural antigo, pequenas mudanças cromáticas em superfícies
neutras ativavam essa máscara. O jogo colore o bloom de roxo ou ciano conforme
o tema. Por isso até a diferença de um byte podia produzir brilho forte.

A receita v2 usa o detalhe de luminância neural com diferenças entre canais
iguais às da textura original interpolada. Um deslocamento comum aos três
canais é limitado antes de saturar, preservando exatamente B−R e G−R. Alpha é
original; pixels transparentes ficam zerados e os pretos permanecem pretos.
Não há supressão global de roxo/azul nem desativação dos efeitos legítimos.
Leitura de PNG usa Clone para evitar arredondamentos RGB por premultiplicação
ao usar DrawImage em pixels translúcidos.

O novo pacote fica em `hd2x-color-safe`; o anterior `hd2x` e o backup ficam
preservados. Novas compilações preparam a receita v2 diretamente. O comando
`TextureTools recolor-pack ORIGINAL_ROOT OLD_PACK NEW_PACK` permite migrar o
pacote antigo sem executar a rede neural novamente, com destino separado.

## Evidência e limites

- 128 folhas normais e oito folhas restauradas: dimensões, alpha e diferenças
  entre canais verificadas, incluindo pixels parcialmente transparentes.
- Nas oito folhas de cenários: mais de 6,6 milhões de pixels opacos antes
  ativavam indevidamente a máscara. Após a correção: **zero ativações novas e
  zero perdas**, comparado à interpolação da textura original.
- Regressão na GPU com o shader bloom real: deriva azul de um byte reproduz o
  defeito; a preservação de cores remove o brilho falso; luz azul original
  continua emitindo. Carregamento/hash/dimensões de 128 texturas passou.
- Estados de progresso, sucesso e falha do compilador verificados, incluindo
  resultado de uma compilação real em pasta pai não vazia; arquivo preexistente
  preservado. Interfaces renderizadas em português, inglês e espanhol.

Os testes isolam a causa confirmada de brilho falso. A composição de cada fase
e os blocos de área vazia vistos na captura ainda precisam de confirmação na
partida pelo usuário; não prometemos que todos os efeitos do port estejam
visualmente fiéis. Não executamos comandos de gameplay nem fechamos sua partida.
