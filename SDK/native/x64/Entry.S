.section .text.entry,"ax",@progbits
.global InuKernelEntry
.type InuKernelEntry,@function
.extern InuRuntimeInitialize
.extern KMain
InuKernelEntry:
    cli
    andq $-16, %rsp
    call InuRuntimeInitialize
    testb %al, %al
    jz InuX64Halt
    # BootContext ABI binding is completed with the runtime pack.
    call KMain
    jmp InuX64Halt
.size InuKernelEntry, .-InuKernelEntry
