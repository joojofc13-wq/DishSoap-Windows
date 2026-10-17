# Local managed assembly extraction

```text
dotnet run --project tools/windows/ManagedExtractor -- EXTRACTED_ROOT MANAGED_ROOT
```

Reads retail XEX2 with BASIC or NORMAL/LZX compression and extracts the embedded x86 managed PE assemblies. It does not execute Xbox code or convert PowerPC modules into Windows programs. The local check covered the seven assemblies in the owner's package. Delta patches and XEX1 are unsupported.

`LzxDecoder.cs` comes from [Xenia Manager](https://github.com/xenia-manager/xenia-manager/blob/main/source/XeniaManager.Files/Utilities/LzxDecoder.cs), obtained on 2026-10-16 under BSD-3-Clause. The full license is in `LICENSE-LZX.txt`. The XEX wrapper was developed for this Windows port, using [Xenia's XEX structures](https://github.com/xenia-project/xenia/blob/master/src/xenia/cpu/xex_module.cc) as a format reference.

Use local files from your own copy. Inputs, extracted assemblies and generated game source stay outside version control and public releases.
