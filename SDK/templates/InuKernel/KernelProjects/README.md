# Inu Kernel Projects

Put separately compiled kernel-side projects below this directory.

Projects stored here are not linked into the kernel merely because of their folder location. `Inu.Configuration.json` and its generated props/targets decide which kernel projects are active and referenced. Reconfiguration can remove a project from the active graph without deleting its files.

In Visual Studio use **Add > New Project** and choose a Inu kernel template such as **Inu Kernel Driver** or **Inu Kernel Library**, then place the new project somewhere below `KernelProjects`.

Filesystem projects are optional kernel projects. For FAT support, add **Inu Filesystem - FatFs** below this folder and explicitly call `FatFs.Install()` after `KernelStorage.Initialize()`.
