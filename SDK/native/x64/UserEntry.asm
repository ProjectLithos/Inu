bits 64
default rel

extern InuRuntimeInitialize
extern InuUserManagedEntry
extern __ReadyToRunHeader

global InuUserEntry
global InuUserSyscall
global InuUserPause
global InuX64BeginSerialRecord
global InuX64EndSerialRecord

section .text
InuUserEntry:
    ; IRETQ supplies an initial stack, not a CALL return address. Align the
    ; call site and reserve Win64's 32-byte home area for each managed call.
    and rsp, -16
    sub rsp, 32
    call InuRuntimeInitialize
    test al, al
    jz .failed
    mov rcx, 0x0000400000100000
    lea rdx, [rel __ReadyToRunHeader]
    call InuUserManagedEntry
.failed:
.halt:
    pause
    jmp .halt

; Win64 managed import ABI: RCX=Get/Set/Event operation, RDX=&UserlandMessage.
; Inu native syscall ABI: RAX=0x4E4F000000000000|(operation<<32), RDI=envelope.
InuUserSyscall:
    ; RDI is nonvolatile under the managed Win64 ABI, but carries the native envelope.
    push rdi
    mov rax, rcx
    shl rax, 32
    mov r8, 0x4E4F000000000000
    or rax, r8
    mov rdi, rdx
    syscall
    pop rdi
    ret

InuUserPause:
    pause
    ret

; Userland exception diagnostics deliberately do not touch privileged serial I/O.
InuX64BeginSerialRecord:
    xor eax, eax
    ret
InuX64EndSerialRecord:
    xor eax, eax
    ret
