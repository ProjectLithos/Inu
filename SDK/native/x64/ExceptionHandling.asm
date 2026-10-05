bits 64
default rel
section .text align=16

extern InuRhThrowEx
extern InuRhRethrow
extern InuX64BeginSerialRecord
extern InuX64EndSerialRecord

global RhpThrowEx
global RhpRethrow
global InuEhCallFinally
global InuEhCallCatch
global InuEhResume
global InuEhTrace
global InuEhTraceValue
global InuEhTraceEmergency

; EhContext layout:
; 00 IP, 08 SP, 10 RBX, 18 RBP, 20 RSI, 28 RDI,
; 30 R12, 38 R13, 40 R14, 48 R15 (0x50 bytes)
%define CTX_IP  0x00
%define CTX_SP  0x08
%define CTX_RBX 0x10
%define CTX_RBP 0x18
%define CTX_RSI 0x20
%define CTX_RDI 0x28
%define CTX_R12 0x30
%define CTX_R13 0x38
%define CTX_R14 0x40
%define CTX_R15 0x48
%define CTX_SIZE 0x50


; Serial-only diagnostic breadcrumb. RCX = diagnostic identifier.
; Preserve legacy two-digit output for identifiers <= 0xFF, but emit the complete
; identifier for the wider runtime/GVM ranges used by current Inu diagnostics.
; Examples: 0x90 -> EH:90, 0x193 -> EH:193, 0x1900 -> EH:1900.
%macro INU_OUT_HEX_NIBBLE 0
    cmp al, 9
    jbe %%digit
    add al, 'A' - 10
    jmp %%out
%%digit:
    add al, '0'
%%out:
    out dx, al
%endmacro

InuEhTrace:
%ifdef INU_USERLAND
    ; Kernel port-I/O diagnostics are unavailable at CPL3. Retain the ABI
    ; without accessing privileged ports or changing the caller's registers.
    ret
%else
    push rax
    push rcx
    push rdx
    push r8
    mov r8, rcx
    sub rsp, 40
    call InuX64BeginSerialRecord
    add rsp, 40
    mov dx, 0x03F8
    mov al, 'E'
    out dx, al
    mov al, 'H'
    out dx, al
    mov al, ':'
    out dx, al

    cmp r8, 0xFF
    jbe .trace_two_digits
    cmp r8, 0xFFF
    jbe .trace_three_digits

    mov rax, r8
    shr rax, 12
    and al, 0x0F
    INU_OUT_HEX_NIBBLE
.trace_three_digits:
    mov rax, r8
    shr rax, 8
    and al, 0x0F
    INU_OUT_HEX_NIBBLE
.trace_two_digits:
    mov rax, r8
    shr rax, 4
    and al, 0x0F
    INU_OUT_HEX_NIBBLE
    mov rax, r8
    and al, 0x0F
    INU_OUT_HEX_NIBBLE
    mov al, 10
    out dx, al
    sub rsp, 40
    call InuX64EndSerialRecord
    add rsp, 40
    pop r8
    pop rdx
    pop rcx
    pop rax
    ret
%endif

; Serial-only 64-bit diagnostic value: RCX = diagnostic tag, RDX = value.
; The tag uses the same full-width formatting as InuEhTrace and the value remains
; fixed-width 16 hexadecimal digits. Allocation-free and register-preserving.
InuEhTraceValue:
%ifdef INU_USERLAND
    ; Kernel port-I/O diagnostics are unavailable at CPL3. Retain the ABI
    ; without accessing privileged ports or changing the caller's registers.
    ret
%else
    push rax
    push rcx
    push rdx
    push r8
    push r9
    push r10
    sub rsp, 40
    call InuX64BeginSerialRecord
    add rsp, 40
    mov r8, [rsp + 32]     ; original RCX/tag
    mov r10, [rsp + 24]    ; original RDX/value
    mov dx, 0x03F8
    mov al, 'E'
    out dx, al
    mov al, 'V'
    out dx, al
    mov al, ':'
    out dx, al

    cmp r8, 0xFF
    jbe .value_tag_two_digits
    cmp r8, 0xFFF
    jbe .value_tag_three_digits

    mov rax, r8
    shr rax, 12
    and al, 0x0F
    INU_OUT_HEX_NIBBLE
.value_tag_three_digits:
    mov rax, r8
    shr rax, 8
    and al, 0x0F
    INU_OUT_HEX_NIBBLE
.value_tag_two_digits:
    mov rax, r8
    shr rax, 4
    and al, 0x0F
    INU_OUT_HEX_NIBBLE
    mov rax, r8
    and al, 0x0F
    INU_OUT_HEX_NIBBLE

    mov al, '='
    out dx, al
    mov r9d, 16
.value_hex_loop:
    rol r10, 4
    mov al, r10b
    and al, 0x0F
    INU_OUT_HEX_NIBBLE
    dec r9d
    jnz .value_hex_loop
    mov al, 10
    out dx, al
    sub rsp, 40
    call InuX64EndSerialRecord
    add rsp, 40
    pop r10
    pop r9
    pop r8
    pop rdx
    pop rcx
    pop rax
    ret
%endif

; Emergency breadcrumb used only when the EH runtime itself has failed. It bypasses
; the serial record gate deliberately so a dead/stopped owner CPU cannot hide EH:18FF.
InuEhTraceEmergency:
%ifdef INU_USERLAND
    ; Kernel port-I/O diagnostics are unavailable at CPL3. Retain the ABI
    ; without accessing privileged ports or changing the caller's registers.
    ret
%else
    push rax
    push rcx
    push rdx
    push r8
    mov r8, rcx
    mov dx, 0x03F8
    mov al, 'E'
    out dx, al
    mov al, 'H'
    out dx, al
    mov al, ':'
    out dx, al
    cmp r8, 0xFF
    jbe .emergency_two_digits
    cmp r8, 0xFFF
    jbe .emergency_three_digits
    mov rax, r8
    shr rax, 12
    and al, 0x0F
    INU_OUT_HEX_NIBBLE
.emergency_three_digits:
    mov rax, r8
    shr rax, 8
    and al, 0x0F
    INU_OUT_HEX_NIBBLE
.emergency_two_digits:
    mov rax, r8
    shr rax, 4
    and al, 0x0F
    INU_OUT_HEX_NIBBLE
    mov rax, r8
    and al, 0x0F
    INU_OUT_HEX_NIBBLE
    mov al, 10
    out dx, al
    pop r8
    pop rdx
    pop rcx
    pop rax
    ret
%endif

; INPUT RCX = managed exception object. Capture the throw-site machine state and
; enter the managed freestanding EH dispatcher. This routine never returns.
RhpThrowEx:
    push rcx
    mov rcx, 0xE0
    call InuEhTrace
    pop rcx
    mov r10, rsp
    sub rsp, CTX_SIZE + 0x28        ; context + Win64 shadow + alignment
    lea rdx, [rsp + 0x20]
    mov rax, [r10]
    mov [rdx + CTX_IP], rax
    lea rax, [r10 + 8]
    mov [rdx + CTX_SP], rax
    mov [rdx + CTX_RBX], rbx
    mov [rdx + CTX_RBP], rbp
    mov [rdx + CTX_RSI], rsi
    mov [rdx + CTX_RDI], rdi
    mov [rdx + CTX_R12], r12
    mov [rdx + CTX_R13], r13
    mov [rdx + CTX_R14], r14
    mov [rdx + CTX_R15], r15
    call InuRhThrowEx
    int3
    jmp $

; Rethrow uses the same context capture; the managed dispatcher retains the
; currently handled exception object for the active EH dispatch.
RhpRethrow:
    mov r10, rsp
    sub rsp, CTX_SIZE + 0x28
    lea rcx, [rsp + 0x20]
    mov rax, [r10]
    mov [rcx + CTX_IP], rax
    lea rax, [r10 + 8]
    mov [rcx + CTX_SP], rax
    mov [rcx + CTX_RBX], rbx
    mov [rcx + CTX_RBP], rbp
    mov [rcx + CTX_RSI], rsi
    mov [rcx + CTX_RDI], rdi
    mov [rcx + CTX_R12], r12
    mov [rcx + CTX_R13], r13
    mov [rcx + CTX_R14], r14
    mov [rcx + CTX_R15], r15
    call InuRhRethrow
    int3
    jmp $

; void InuEhCallFinally(UInt64 handler, EhContext* context)
; RCX=handler, RDX=context. Load the parent frame's preserved register state,
; call the NativeAOT finally/fault funclet, then restore dispatcher state.
InuEhCallFinally:
    push rbp
    push rbx
    push rsi
    push rdi
    push r12
    push r13
    push r14
    push r15
    sub rsp, 0x28
    mov r11, rcx
    mov r10, rdx
    mov rbx, [r10 + CTX_RBX]
    mov rbp, [r10 + CTX_RBP]
    mov rsi, [r10 + CTX_RSI]
    mov rdi, [r10 + CTX_RDI]
    mov r12, [r10 + CTX_R12]
    mov r13, [r10 + CTX_R13]
    mov r14, [r10 + CTX_R14]
    mov r15, [r10 + CTX_R15]
    call r11
    add rsp, 0x28
    pop r15
    pop r14
    pop r13
    pop r12
    pop rdi
    pop rsi
    pop rbx
    pop rbp
    ret

; UInt64 InuEhCallCatch(Object ex, UInt64 handler, EhContext* context)
; RCX=exception, RDX=handler, R8=context. Catch funclet returns continuation IP.
InuEhCallCatch:
    push rbp
    push rbx
    push rsi
    push rdi
    push r12
    push r13
    push r14
    push r15
    sub rsp, 0x38
    mov [rsp + 0x20], rcx
    mov r11, rdx
    mov r10, r8
    mov rbx, [r10 + CTX_RBX]
    mov rbp, [r10 + CTX_RBP]
    mov rsi, [r10 + CTX_RSI]
    mov rdi, [r10 + CTX_RDI]
    mov r12, [r10 + CTX_R12]
    mov r13, [r10 + CTX_R13]
    mov r14, [r10 + CTX_R14]
    mov r15, [r10 + CTX_R15]
    mov rcx, [rsp + 0x20]
    call r11
    mov [rsp + 0x28], rax
    add rsp, 0x38
    pop r15
    pop r14
    pop r13
    pop r12
    pop rdi
    pop rsi
    pop rbx
    pop rbp
    ; The continuation was kept in caller stack memory that is gone now; RAX is
    ; still the funclet result by ABI and preserved through the pops above.
    ret

; noreturn InuEhResume(UInt64 resumeIp, EhContext* context)
; Restore the selected frame and continue at the catch continuation.
InuEhResume:
    mov r10, rcx
    mov r11, rdx
    mov rbx, [r11 + CTX_RBX]
    mov rbp, [r11 + CTX_RBP]
    mov rsi, [r11 + CTX_RSI]
    mov rdi, [r11 + CTX_RDI]
    mov r12, [r11 + CTX_R12]
    mov r13, [r11 + CTX_R13]
    mov r14, [r11 + CTX_R14]
    mov r15, [r11 + CTX_R15]
    mov rsp, [r11 + CTX_SP]
    jmp r10
