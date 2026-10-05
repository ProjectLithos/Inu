# Inu + Kath 0.0.18

This release repairs the flat FullSource build bootstrap so an extracted package can be built directly when the original ZIP is no longer present. When a FullSource ZIP is available, the build still prefers and extracts that exact package before compiling.

Toolchain pins remain .NET SDK 10.0.302, NativeAOT/ILCompiler 10.0.10, LLVM/Clang/LLD 22.1.6, Node.js 22.22.0, and Python 3.13.15.
