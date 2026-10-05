# Inu

Inu is the clean-rewrite systems SDK. Its installed source layout is deliberately compact:

```text
C:\DCLG\Inu
├── Build.bat
├── Bootstrap-Inu.ps1
├── Install-Toolchain.bat
├── Install-Toolchain.ps1
├── Test-Toolchain.ps1
├── VERSION
├── src\
│   ├── Common\
│   └── Language\
│       ├── Input\
│       └── Output\
├── targets\
├── toolchains\
├── tools\
└── docs\
```

There is no `Repos` directory and no nested source ZIP model. `C:\KandI\Build.bat` selects/extracts the FullSource ZIP, mirrors the exact current source into `Inu` and `Kath`, removes obsolete root/source files, provisions private tools, and builds both products. The Kath&Inu root is reduced to the supported Build/Run/VERSION/Inu/Kath contract.
