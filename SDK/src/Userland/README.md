# Inu Userland

Inu separates universal applications, operating-system commands, and privileged kernel mechanisms.

Userland projects in this directory contain user-facing libraries and contracts:

- `Inu.Userland` - aggregate userland project.
- `Settings` - user-configurable setting categories.
- `Fonts` - userland font catalog and font-facing contracts.
- `Images` - userland image contracts.
- `Drivers` - user-visible driver/device contracts; privileged MMIO/PIO stays in the kernel/HAL.

Ordinary command programs are **not** owned by this directory. Their canonical source is `src/System/Commands`, matching the installed `/System/Commands` catalogue. This includes filesystem commands such as `dir` together with `stress`, `selftest`, and the other system commands.

`/bin` is reserved for applications that are universal to all users. `Inu-Shell` belongs there when the current bootstrap shell is promoted from the kernel-hosted compatibility boundary to a standalone packaged application.

`Inu.Kernel.CommandLine` remains the privileged terminal/dispatch boundary for the current freestanding bootstrap. Command and userland code must not directly access hardware ports, MMIO, page tables, interrupt controllers, or other privileged mechanisms.


## Desktop and login

`Desktop` is an ordinary userland GUI component. It owns the wallpaper, panel and desktop policy. `Login` is a separate ordinary userland GUI application drawn above the already-running desktop/compositor. Neither login presentation nor desktop policy belongs in the kernel. The default desktop source uses `/System/Wallpapers/INU-HEX.BMP`, copied from the supplied Inu hexagonal background.
