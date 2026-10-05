# PS/2 input

Inu 0.25.0 provides an i8042 controller service, PS/2 Set-1 keyboard decoding, standard three-byte PS/2 mouse decoding, and runtime keyboard layout selection.

The installed keyboard layouts are `English_UK` and `English_USA`. These are real translation tables, including letters with Shift/Caps Lock, number-row symbols, punctuation, keypad characters, UK `£`, UK `#/~`, the ISO UK `\/|` key, and UK AltGr+4 `€`.

The kernel owns ports `0x60` and `0x64`. User processes select a layout only through Inu Get/Set service 32.

Userland command syntax:

```text
keyboard get
keyboard set English_UK
keyboard set English_USA
keyboard list
```

A layout change takes effect immediately for subsequent decoded keyboard events.

In 0.25.0 the PS/2 driver is the sole owner of i8042 reads. Decoded key events are delivered upward to the live console/input consumer; the console no longer rereads ports `0x60`/`0x64`. Printable keys are echoed by the generated kernel, Up/Down scroll, 1/2/3 select framebuffer font presets, and Ctrl+1/2/3 select single/double/triple framebuffer buffering.
