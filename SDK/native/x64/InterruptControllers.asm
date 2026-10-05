bits 64
default rel
section .text

global InuX64ControllerReadPort8
global InuX64ControllerWritePort8
global InuX64ReadMsr
global InuX64WriteMsr
global InuX64ReadMmio32
global InuX64WriteMmio32
global InuX64DisableLegacyPic

InuX64ControllerReadPort8:
    mov dx, cx
    xor eax, eax
    in al, dx
    ret
InuX64ControllerWritePort8:
    mov eax, edx
    mov dx, cx
    out dx, al
    mov eax, 1
    ret
InuX64ReadMsr:
    mov ecx, ecx
    rdmsr
    shl rdx, 32
    or rax, rdx
    ret
InuX64WriteMsr:
    mov r8, rdx
    mov eax, r8d
    shr r8, 32
    mov edx, r8d
    wrmsr
    mov eax, 1
    ret
InuX64ReadMmio32:
    mov eax, [rcx]
    ret
InuX64WriteMmio32:
    mov [rcx], edx
    mfence
    mov eax, 1
    ret


; Masks both 8259 PICs so APIC/MSI delivery can own external vectors.
InuX64DisableLegacyPic:
    mov al, 0xFF
    out 0x21, al
    out 0xA1, al
    mov eax, 1
    ret
