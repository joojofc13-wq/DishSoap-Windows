# Extração local de assemblies gerenciados

`dotnet run --project tools/windows/ManagedExtractor -- <extraídos> <managed>`

Lê XEX2 retail com compressão BASIC ou NORMAL/LZX. Extrai somente PE x86
embutido (game.exe e assemblies .NET CF/XNA); não executa código Xbox.
Foi validado com os sete assemblies do pacote fornecido pelo usuário.
Não transforma módulos PowerPC em executáveis Windows.

LzxDecoder.cs é de Xenia Manager (BSD-3-Clause), obtido em 2026-10-16:
https://github.com/xenia-manager/xenia-manager/blob/main/source/XeniaManager.Files/Utilities/LzxDecoder.cs
Licença integral em LICENSE-LZX.txt. Estrutura XEX verificada no Xenia:
https://github.com/xenia-project/xenia/blob/master/src/xenia/cpu/xex_module.cc
e src/xenia/kernel/util/xex2_info.h. O wrapper é código novo deste port.

Entradas/saídas são propriedade do usuário e ficam fora do controle de versão.
Use apenas arquivos locais de sua cópia. Delta patches e XEX1 não suportados.
