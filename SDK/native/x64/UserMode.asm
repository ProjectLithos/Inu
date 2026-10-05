bits 64
default rel

section .text

extern InuX64SyscallTraceByte

global InuX64EnterUserMode

; Win64 ABI: RCX=user RIP, RDX=user RSP, R8=opaque first argument.
; The kernel continuation is saved in the per-CPU syscall state. A process-exit
; syscall returns here rather than SYSRET so the managed process layer can safely
; restore the kernel CR3 and release the user address space.
InuX64EnterUserMode:
    test rcx, rcx
    jz .failed
    test rdx, rdx
    jz .failed
    test rdx, 0xF
    jnz .failed

    ; Preserve Win64 non-volatile registers because user code is free to change them.
    push rbx
    push rbp
    push rsi
    push rdi
    push r12
    push r13
    push r14
    push r15

    ; In kernel mode IA32_KERNEL_GS_BASE owns the syscall-state address. Expose it
    ; briefly, save the continuation, then restore the normal kernel GS value.
    swapgs
    mov [gs:0x58], rsp
    lea rax, [rel .returned_from_user]
    mov [gs:0x60], rax
    mov qword [gs:0x68], 0
    mov qword [gs:0x70], 0
    mov qword [gs:0x78], 1
    swapgs

    mov rdi, r8
    push qword 0x1B
    push rdx
    pushfq
    pop rax
    or rax, 0x200
    and rax, ~0x3000
    push rax
    push qword 0x23
    push rcx

    ; Serial marker U = kernel has prepared the complete IRETQ frame and is about to enter ring 3.
    mov al, 'U'
    call InuX64SyscallTraceByte
    iretq

.returned_from_user:
    ; Serial marker K = ring-3 execution returned to the saved kernel continuation.
    mov al, 'K'
    call InuX64SyscallTraceByte

    ; Kernel mode has the normal GS base after both the syscall-exit and user-fault
    ; paths.  Briefly expose the per-CPU state to consume the transition reason:
    ; 1 = controlled Event(ProcessExit), 2 = contained CPL3 exception.
    swapgs
    mov r10, [gs:0x68]
    mov qword [gs:0x78], 0
    mov qword [gs:0x68], 0
    swapgs

    pop r15
    pop r14
    pop r13
    pop r12
    pop rdi
    pop rsi
    pop rbp
    pop rbx
    mov eax, r10d
    test eax, eax
    jnz .return_status
    mov eax, 1
.return_status:
    ret
.failed:
    xor eax, eax
    ret
