# Inu SDK 0.0.18 embedded in Kath

The embedded Inu SDK surface is version-aligned with Inu 0.0.18. Compiler/runtime pins remain .NET SDK 10.0.302, NativeAOT/ILCompiler 10.0.10, and LLVM/Clang/LLD 22.1.6.

The root bootstrap now accepts both the authoritative FullSource ZIP and an already-extracted flat FullSource tree while preserving exact version gates.
