# Inu 0.0.202

Fixes GUI bootstrap compilation in `Inu.Kernel.Gui`.

- Stops re-pinning the `Surface.Title` fixed buffer through a `Surface*`; the buffer address is already stable through the unmanaged surface pointer.
- Explicitly converts the unsigned `Border` constant to `Int32` before subtracting it from signed window coordinates, preserving the `FillRect` signed-coordinate API.
- Retains the 0.0.201 bootstrap fix that prefers a newer FullSource archive over an older extracted tree.
