bits 64
default rel
section .text

global InuX64LoadGlobalDescriptorTable
global InuX64LoadTaskRegister

; Windows x64 ABI: RCX=base, DX=limit, R8W=code selector, R9W=data selector.
InuX64LoadGlobalDescriptorTable:
    sub rsp, 16
    mov [rsp], dx
    mov [rsp + 2], rcx
    lgdt [rsp]
    mov ax, r9w
    mov ds, ax
    mov es, ax
    mov ss, ax
    xor eax, eax
    mov fs, ax
    mov gs, ax
    lea rax, [rel .segments_reloaded]
    push r8
    push rax
    retfq
.segments_reloaded:
    add rsp, 16
    mov al, 1
    ret

; Windows x64 ABI: CX=selector.
InuX64LoadTaskRegister:
    mov ax, cx
    ltr ax
    mov al, 1
    ret


section .bss align=16
InuX64BootstrapGdt: resq 7
InuX64BootstrapTss: resb 104
InuX64BootstrapRsp0Stack: resb 16384
InuX64BootstrapDoubleFaultStack: resb 16384
InuX64BootstrapNmiStack: resb 16384
InuX64BootstrapMachineCheckStack: resb 16384

section .text
global InuX64InitializeBootstrapDescriptors

; Installs the bootstrap processor's GDT and 64-bit TSS.
InuX64InitializeBootstrapDescriptors:
    lea r10, [rel InuX64BootstrapGdt]
    xor eax, eax
    mov [r10 + 0], rax
    mov rax, 0x00AF9A000000FFFF
    mov [r10 + 8], rax
    mov rax, 0x00CF92000000FFFF
    mov [r10 + 16], rax
    mov rax, 0x00CFF2000000FFFF
    mov [r10 + 24], rax
    mov rax, 0x00AFFA000000FFFF
    mov [r10 + 32], rax

    lea r11, [rel InuX64BootstrapTss]
    mov rcx, 13
    xor eax, eax
.clear_tss:
    mov [r11 + rcx * 8 - 8], rax
    loop .clear_tss

    lea rax, [rel InuX64BootstrapRsp0Stack + 16384]
    mov [r11 + 4], rax
    lea rax, [rel InuX64BootstrapDoubleFaultStack + 16384]
    mov [r11 + 36], rax
    lea rax, [rel InuX64BootstrapNmiStack + 16384]
    mov [r11 + 44], rax
    lea rax, [rel InuX64BootstrapMachineCheckStack + 16384]
    mov [r11 + 52], rax
    mov word [r11 + 102], 104

    mov rax, r11
    mov rcx, 103
    mov rdx, rax
    and rdx, 0xFFFFFF
    shl rdx, 16
    or rdx, rcx
    mov rcx, rax
    shr rcx, 24
    and rcx, 0xFF
    shl rcx, 56
    or rdx, rcx
    mov rcx, 0x0000890000000000
    or rdx, rcx
    mov [r10 + 40], rdx
    shr rax, 32
    mov [r10 + 48], rax

    sub rsp, 16
    mov word [rsp], 55
    mov [rsp + 2], r10
    lgdt [rsp]
    add rsp, 16
    mov ax, 0x10
    mov ds, ax
    mov es, ax
    mov ss, ax
    xor eax, eax
    mov fs, ax
    mov gs, ax
    lea rax, [rel .bootstrap_segments_loaded]
    push qword 0x08
    push rax
    retfq
.bootstrap_segments_loaded:
    mov ax, 0x28
    ltr ax
    mov eax, 1
    ret
