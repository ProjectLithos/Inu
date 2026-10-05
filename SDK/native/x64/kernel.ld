OUTPUT_FORMAT(elf64-x86-64)
ENTRY(InuKernelEntry)
SECTIONS
{
    . = 0x0000000001000000;
    .text : ALIGN(4096) { KEEP(*(.text.entry)) *(.text .text.*) }
    .rodata : ALIGN(4096) { *(.rodata .rodata.*) }
    .data : ALIGN(4096) { *(.data .data.*) }
    .bss : ALIGN(4096) { *(COMMON) *(.bss .bss.*) }
    .eh_frame : ALIGN(8) { KEEP(*(.eh_frame .eh_frame.*)) }
    .managedcode : ALIGN(4096) { KEEP(*(.managedcode .managedcode.*)) }
    /DISCARD/ : { *(.comment) *(.note.GNU-stack) }
}
