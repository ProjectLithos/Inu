bits 64
default rel

section .text

extern InuManagedSyscallDispatch

global InuX64ConfigureSystemCalls
global InuX64EnableSmap
global InuX64IsSmapEnabled
global InuX64BeginUserMemoryAccess
global InuX64EndUserMemoryAccess
global InuX64SyscallEntry
global InuX64RequestUserModeExit
global InuX64SyscallTraceByte

; Per-CPU syscall state layout, addressed through IA32_KERNEL_GS_BASE after SWAPGS.
; +00 kernel syscall stack top
; +08 saved user RSP
; +10 saved user RIP (RCX from SYSCALL)
; +18 saved user RFLAGS (R11 from SYSCALL)
; +20 encoded syscall number
; +28 argument 0 (RDI)
; +30 argument 1 (RSI)
; +38 argument 2 (RDX)
; +40 argument 3 (R10)
; +48 argument 4 (R8)
; +50 argument 5 (R9)
; +58 saved kernel RSP for the active ring-3 process
; +60 saved kernel continuation RIP
; +68 user-exit requested flag
; +70 user-exit code
; +78 user-mode active flag

%define IA32_EFER           0xC0000080
%define IA32_STAR           0xC0000081
%define IA32_LSTAR          0xC0000082
%define IA32_FMASK          0xC0000084
%define IA32_KERNEL_GS_BASE 0xC0000102

; Earliest-possible one-byte ring-3/syscall breadcrumbs. Raw E/D/R/X/U/K bytes are
; intentionally isolated on QEMU isa-debugcon (0xE9); they must never share COM1 with
; line-oriented EH/EV/console diagnostics because stress can emit them concurrently on
; several CPUs. AL contains the marker byte; RDX is volatile.
InuX64SyscallTraceByte:
    mov dx, 0x00E9
    out dx, al
    ret

; RCX = syscall state virtual address, RDX = aligned kernel syscall stack top.
InuX64ConfigureSystemCalls:
    test rcx, rcx
    jz .configure_failed
    test rdx, rdx
    jz .configure_failed
    test rdx, 0xF
    jnz .configure_failed
    mov [rcx], rdx

    ; Make SWAPGS expose the supplied per-CPU state on ring-3 -> ring-0 entry.
    mov r8, rcx
    mov ecx, IA32_KERNEL_GS_BASE
    mov eax, r8d
    mov rdx, r8
    shr rdx, 32
    wrmsr

    ; Enable the SYSCALL extension.
    mov ecx, IA32_EFER
    rdmsr
    or eax, 1
    wrmsr

    ; STAR: kernel CS=0x08. SYSRET base=0x13, producing SS=0x1B and CS=0x23.
    mov ecx, IA32_STAR
    mov eax, 0
    mov edx, 0x00130008
    wrmsr

    mov ecx, IA32_LSTAR
    lea r8, [rel InuX64SyscallEntry]
    mov eax, r8d
    mov rdx, r8
    shr rdx, 32
    wrmsr

    ; Clear TF, IF and DF on entry. The dispatcher executes with interrupts disabled.
    mov ecx, IA32_FMASK
    mov eax, 0x700
    xor edx, edx
    wrmsr

    mov eax, 1
    ret
.configure_failed:
    xor eax, eax
    ret

InuX64EnableSmap:
    push rbx
    mov eax, 7
    xor ecx, ecx
    cpuid
    bt ebx, 20
    jnc .smap_unsupported
    mov rax, cr4
    bts rax, 21
    mov cr4, rax
    mov eax, 1
    pop rbx
    ret
.smap_unsupported:
    xor eax, eax
    pop rbx
    ret

InuX64IsSmapEnabled:
    mov rax, cr4
    shr rax, 21
    and eax, 1
    ret

; STAC/CLAC are executed only when CR4.SMAP is active, so the helpers are safe
; on processors that do not implement SMAP.
InuX64BeginUserMemoryAccess:
    mov rax, cr4
    bt rax, 21
    jnc .begin_done
    stac
.begin_done:
    mov eax, 1
    ret

InuX64EndUserMemoryAccess:
    mov rax, cr4
    bt rax, 21
    jnc .end_done
    clac
.end_done:
    mov eax, 1
    ret


; Requests that the active ring-3 execution returns to its saved kernel continuation.
; Win64 ABI: RCX = signed process exit code. This is valid only while servicing
; a SYSCALL from an Inu user process (GS points at the per-CPU syscall state).
InuX64RequestUserModeExit:
    cmp qword [gs:0x78], 1
    jne .exit_request_failed
    mov [gs:0x70], rcx
    mov qword [gs:0x68], 1
    mov eax, 1
    ret
.exit_request_failed:
    xor eax, eax
    ret

; x64 user ABI accepted by Inu:
;   RAX = explicitly namespaced service number
;   RDI,RSI,RDX,R10,R8,R9 = six arguments (Linux register order)
; SYSCALL supplies user RIP in RCX and user RFLAGS in R11.
InuX64SyscallEntry:
    swapgs
    mov [gs:0x08], rsp
    mov [gs:0x10], rcx
    mov [gs:0x18], r11
    mov [gs:0x20], rax
    mov [gs:0x28], rdi
    mov [gs:0x30], rsi
    mov [gs:0x38], rdx
    mov [gs:0x40], r10
    mov [gs:0x48], r8
    mov [gs:0x50], r9

    mov rsp, [gs:0x00]
    and rsp, -16

    ; Preserve the user registers that SYSCALL promises not to clobber.
    push r15
    push r14
    push r13
    push r12
    push rbp
    push rbx
    push rdi
    push rsi
    push r10
    push r9
    push r8
    push rdx

    ; High-frequency entry/dispatch/return breadcrumbs are Debug-only. In Release/No Debug
    ; they interleave across CPUs and can corrupt the structured EH/EV serial diagnostics.
%ifdef INU_DEBUG
    mov al, 'E'
    call InuX64SyscallTraceByte
%endif

    ; Win64 call ABI: RCX,RDX,R8,R9 then stack arguments, plus shadow space.
    sub rsp, 64
    mov rcx, [gs:0x20]
    mov rdx, [gs:0x28]
    mov r8,  [gs:0x30]
    mov r9,  [gs:0x38]
    mov rax, [gs:0x40]
    mov [rsp+32], rax
    mov rax, [gs:0x48]
    mov [rsp+40], rax
    mov rax, [gs:0x50]
    mov [rsp+48], rax
    call InuManagedSyscallDispatch
    add rsp, 64
    jmp .dispatch_complete

.dispatch_complete:
    ; Every Get/Set/Event handler converges here exactly once after managed dispatch.
    ; There is no case-style fall-through: the managed dispatcher returns one result,
    ; then this common epilogue chooses either controlled kernel return or SYSRETQ.

    ; Debug-only marker D = managed Get/Set/Event dispatch returned.
%ifdef INU_DEBUG
    push rax
    mov al, 'D'
    call InuX64SyscallTraceByte
    pop rax
%endif

    ; Process-exit syscalls do not SYSRET. Abandon the syscall stack and resume
    ; the kernel continuation saved by InuX64EnterUserMode after restoring the
    ; normal kernel GS state. The managed process layer performs CR3 cleanup.
    cmp qword [gs:0x68], 0
    je .return_to_user
    jmp .return_to_kernel

.return_to_kernel:
    mov r12, [gs:0x58]
    mov r13, [gs:0x60]
    mov qword [gs:0x78], 0
    ; Keep +0x68 set until InuX64EnterUserMode's saved continuation consumes the
    ; transition status.  Value 1 means controlled Event(ProcessExit); value 2 is
    ; reserved for a contained CPL3 exception from the interrupt boundary.
    mov al, 'X'
    call InuX64SyscallTraceByte
    swapgs
    mov rsp, r12
    mov eax, 1
    jmp r13

.return_to_user:
    ; RAX is the ABI return value. Restore every other preserved register.
    pop rdx
    pop r8
    pop r9
    pop r10
    pop rsi
    pop rdi
    pop rbx
    pop rbp
    pop r12
    pop r13
    pop r14
    pop r15

    mov rcx, [gs:0x10]
    mov r11, [gs:0x18]
    ; Debug-only marker R = all managed work completed and SYSRETQ is next.
%ifdef INU_DEBUG
    push rax
    push rdx
    mov al, 'R'
    call InuX64SyscallTraceByte
    pop rdx
    pop rax
%endif
    mov rsp, [gs:0x08]
    swapgs
    ; Encode SYSRETQ explicitly. Plain SYSRET is operand-size sensitive and can select the
    ; compatibility-mode return form, which is invalid for Inu's 64-bit ring-3 processes.
    db 0x48, 0x0F, 0x07
