bits 64
default rel
section .text

global InuX64DisableInterrupts
global InuX64EnableInterrupts
global InuX64AreInterruptsEnabled
global InuX64Halt
global InuX64Pause
global InuX64CapturePanicContext
global InuX64PanicDebuggerBreak
global InuX64WaitForInterrupt
global InuX64WritePort8
global InuX64ReadPort8
global InuX64WritePort16
global InuX64ReadPort16
global InuX64WritePort32
global InuX64ReadPort32
global InuX64ReadTimestampCounter
global InuX64SupportsTsc
global InuX64SupportsInvariantTsc
global InuX64ReadMmio64
global InuX64WriteMmio64
global InuX64InitializeThreadContext
global InuX64SwitchThreadContext
global InuX64GetCurrentStackPointer
global InuX64EnableKernelWriteProtect
global InuX64IsExecuteDisableEnabled
global InuX64IsKernelWriteProtectEnabled
global InuX64SupportsSmep
global InuX64EnableSmep
global InuX64SupportsSmap
global InuX64AtomicCompareExchange64
global InuX64AtomicExchange64
global InuX64AtomicFetchAdd64
global InuX64AtomicLoad64
global InuX64AtomicStore64
global InuX64MemoryBarrier
global InuX64BeginSerialRecord
global InuX64TryBeginSerialRecord
global InuX64EndSerialRecord

InuX64DisableInterrupts:
    cli
    mov al, 1
    ret
InuX64EnableInterrupts:
    sti
    mov al, 1
    ret
InuX64AreInterruptsEnabled:
    pushfq
    pop rax
    shr rax, 9
    and rax, 1
    ret
InuX64Halt:
    cli
.halt_forever:
    hlt
    jmp .halt_forever
; Atomically enable maskable interrupts and halt. x86 guarantees the instruction
; after STI executes before a newly enabled maskable interrupt is serviced, so STI;HLT
; closes the scheduler lost-wakeup race between an empty-run-queue check and sleep.
InuX64WaitForInterrupt:
    sti
    hlt
    mov al, 1
    ret
InuX64Pause:
    pause
    mov al, 1
    ret

; Cross-CPU COM1 record gate.  The high dword stores APIC-ID+1 and the low dword
; stores the recursive depth.  Keeping owner+depth in one atomic word closes the
; NMI window that would exist with separate owner/gate fields.
section .bss align=8
InuX64SerialRecordState:
    resq 1
section .text

; Returns a non-zero 32-bit serial owner token in R10 (APIC ID + 1, with wrap
; mapped to 0xFFFFFFFF).  Volatile registers may be clobbered; RBX is preserved
; by InuX64GetCurrentApicId.
%macro INU_SERIAL_OWNER_TOKEN 0
    sub rsp, 40
    call InuX64GetCurrentApicId
    add rsp, 40
    inc eax
    jnz %%owner_ready
    mov eax, 0xFFFFFFFF
%%owner_ready:
    mov r10d, eax
    shl r10, 32
%endmacro

; Blocks until this CPU owns the complete serial diagnostic record.
InuX64BeginSerialRecord:
    INU_SERIAL_OWNER_TOKEN
.begin_retry:
    mov rax, [rel InuX64SerialRecordState]
    test rax, rax
    jz .begin_unowned
    mov r11, rax
    shr r11, 32
    shl r11, 32
    cmp r11, r10
    jne .begin_wait
    mov r11, rax
    inc r11
    lock cmpxchg [rel InuX64SerialRecordState], r11
    jne .begin_retry
    mov eax, 1
    ret
.begin_unowned:
    mov r11, r10
    or r11, 1
    xor eax, eax
    lock cmpxchg [rel InuX64SerialRecordState], r11
    jne .begin_retry
    mov eax, 1
    ret
.begin_wait:
    pause
    jmp .begin_retry

; Interrupt-safe non-blocking acquisition. Returns false if another CPU owns
; COM1; recursively succeeds when the interrupted CPU already owns the record.
InuX64TryBeginSerialRecord:
    INU_SERIAL_OWNER_TOKEN
.try_retry:
    mov rax, [rel InuX64SerialRecordState]
    test rax, rax
    jz .try_unowned
    mov r11, rax
    shr r11, 32
    shl r11, 32
    cmp r11, r10
    jne .try_busy
    mov r11, rax
    inc r11
    lock cmpxchg [rel InuX64SerialRecordState], r11
    jne .try_retry
    mov eax, 1
    ret
.try_unowned:
    mov r11, r10
    or r11, 1
    xor eax, eax
    lock cmpxchg [rel InuX64SerialRecordState], r11
    jne .try_busy
    mov eax, 1
    ret
.try_busy:
    xor eax, eax
    ret

; Releases one recursion level of the current CPU's serial record.
InuX64EndSerialRecord:
    INU_SERIAL_OWNER_TOKEN
.end_retry:
    mov rax, [rel InuX64SerialRecordState]
    test rax, rax
    jz .end_failed
    mov r11, rax
    shr r11, 32
    shl r11, 32
    cmp r11, r10
    jne .end_failed
    mov r11d, eax
    cmp r11d, 1
    jbe .end_release
    lea r11, [rax - 1]
    lock cmpxchg [rel InuX64SerialRecordState], r11
    jne .end_retry
    mov eax, 1
    ret
.end_release:
    xor r11d, r11d
    lock cmpxchg [rel InuX64SerialRecordState], r11
    jne .end_retry
    mov eax, 1
    ret
.end_failed:
    xor eax, eax
    ret

; Professional synchronization atomics (Microsoft x64 ABI).
; CompareExchange: RCX=location, RDX=expected, R8=replacement, R9=previous*.
InuX64AtomicCompareExchange64:
    mov rax, rdx
    lock cmpxchg [rcx], r8
    sete r10b
    mov [r9], rax
    mov al, r10b
    ret

; Exchange: RCX=location, RDX=value, R8=previous*. XCHG with memory is atomic.
InuX64AtomicExchange64:
    mov rax, rdx
    xchg [rcx], rax
    mov [r8], rax
    mov al, 1
    ret

; FetchAdd: RCX=location, RDX=delta, R8=previous*.
InuX64AtomicFetchAdd64:
    mov rax, rdx
    lock xadd [rcx], rax
    mov [r8], rax
    mov al, 1
    ret

; Acquire-style aligned 64-bit load. RCX=location, RDX=value*.
InuX64AtomicLoad64:
    mov rax, [rcx]
    lfence
    mov [rdx], rax
    mov al, 1
    ret

; Sequentially-consistent 64-bit store. RCX=location, RDX=value.
InuX64AtomicStore64:
    xchg [rcx], rdx
    mov al, 1
    ret

InuX64MemoryBarrier:
    mfence
    mov al, 1
    ret

; RCX=&rip, RDX=&rsp, R8=&rbp, R9=&flags, [rsp+40]=&cr3 (Microsoft x64 ABI)
InuX64CapturePanicContext:
    mov r10, [rsp]              ; managed caller return address
    mov [rcx], r10
    lea r10, [rsp + 8]          ; caller stack immediately after the return address
    mov [rdx], r10
    mov [r8], rbp
    pushfq
    pop r10
    mov [r9], r10
    mov r11, [rsp + 40]
    mov r10, cr3
    mov [r11], r10
    mov al, 1
    ret

InuX64PanicDebuggerBreak:
    int3
    mov al, 1
    ret
InuX64WritePort8:
    mov r8b, dl
    mov dx, cx
    mov al, r8b
    out dx, al
    mov al, 1
    ret
InuX64ReadPort8:
    mov r8, rdx
    mov dx, cx
    in al, dx
    mov [r8], al
    mov al, 1
    ret
InuX64WritePort16:
    mov r8w, dx
    mov dx, cx
    mov ax, r8w
    out dx, ax
    mov al, 1
    ret
InuX64ReadPort16:
    mov r8, rdx
    mov dx, cx
    in ax, dx
    mov [r8], ax
    mov al, 1
    ret
InuX64WritePort32:
    mov r8d, edx
    mov dx, cx
    mov eax, r8d
    out dx, eax
    mov al, 1
    ret
InuX64ReadPort32:
    mov r8, rdx
    mov dx, cx
    in eax, dx
    mov [r8], eax
    mov al, 1
    ret

InuX64ReadTimestampCounter:
    lfence
    rdtsc
    shl rdx, 32
    or rax, rdx
    ret

InuX64SupportsTsc:
    push rbx
    mov eax, 1
    cpuid
    bt edx, 4
    setc al
    movzx eax, al
    pop rbx
    ret

InuX64SupportsInvariantTsc:
    push rbx
    mov eax, 0x80000000
    cpuid
    cmp eax, 0x80000007
    jb .no_invariant_tsc
    mov eax, 0x80000007
    cpuid
    bt edx, 8
    setc al
    movzx eax, al
    pop rbx
    ret
.no_invariant_tsc:
    xor eax, eax
    pop rbx
    ret

InuX64ReadMmio64:
    mov rax, [rcx]
    ret
InuX64WriteMmio64:
    mov [rcx], rdx
    mfence
    mov al, 1
    ret

; ---------------------------------------------------------------------------
; Symmetric multiprocessing bootstrap support.
; The template below is copied by the BSP into a UEFI-reserved 4 KiB page below
; 1 MiB. APs enter it in real mode after SIPI, adopt the active CR3, transition
; to long mode, report their APIC ID, then jump through a BSP-patched absolute
; pointer into permanent kernel code before publishing startup and entering the
; managed CPU-local scheduler.
; ---------------------------------------------------------------------------

global InuX64GetCurrentApicId
global InuX64PrepareApplicationProcessorTrampoline
global InuX64GetApplicationProcessorStartupStatus
global InuX64GetApplicationProcessorObservedApicId
global InuX64PrepareApplicationProcessorDescriptorState
global InuX64PrepareApplicationProcessorTrampolineWithDescriptorState
global InuX64GetSchedulerIdleThreadEntryPoint
global InuX64GetSchedulerRoleWorkerEntryPoint
global InuX64GetSchedulerOneShotThreadEntryPoint
extern InuManagedSchedulerApplicationProcessorEntry
extern InuManagedSchedulerIdleThread
extern InuManagedSchedulerRoleWorker
extern InuManagedSchedulerOneShotThread

section .rdata align=16
bits 16
InuApTrampolineTemplate:
    cli
    cld
    push cs
    pop ds
    lgdt [InuApTrampolineGdtDescriptor - InuApTrampolineTemplate]

    mov eax, cr4
    or eax, 0x20                    ; CR4.PAE
    mov cr4, eax

    mov eax, [InuApTrampolineCr3 - InuApTrampolineTemplate]
    mov cr3, eax

    mov ecx, 0xC0000080             ; IA32_EFER
    rdmsr
    ; The BSP installs NX bits in Inu heap/page-table mappings.  INIT resets
    ; each AP's EFER state, so LME alone is insufficient: with NXE clear, merely
    ; touching an NX-marked kernel stack raises a reserved-bit page fault before
    ; managed AP entry can execute.  The BSP patches the EFER feature bits that
    ; are already active on the boot processor (currently LME + NXE).
    or eax, [InuApTrampolineEferEnableBits - InuApTrampolineTemplate]
    wrmsr

    mov eax, cr0
    or eax, 0x80000001              ; CR0.PG | CR0.PE
    mov cr0, eax

    ; Operand-size override encodes ptr16:32. The BSP patches the absolute
    ; 32-bit linear target because the long-mode code segment has base zero.
    db 0x66, 0xEA
InuApTrampolineFarTarget:
    dd 0
    dw 0x0008

align 8
InuApTrampolineGdt:
    dq 0x0000000000000000
    dq 0x00AF9A000000FFFF           ; 64-bit kernel code
    dq 0x00CF92000000FFFF           ; kernel data
InuApTrampolineGdtEnd:
InuApTrampolineGdtDescriptor:
    dw InuApTrampolineGdtEnd - InuApTrampolineGdt - 1
InuApTrampolineGdtBase:
    dd 0

align 8
InuApTrampolineCr3:
    dd 0
    dd 0
InuApTrampolineEferEnableBits:
    dd 0
    dd 0
InuApTrampolineStackTop:
    dq 0
InuApTrampolineStatus:
    dd 0
InuApTrampolineObservedApicId:
    dd 0
align 8
InuApTrampolineIdtr:
    dw 0
    dq 0
InuApTrampolineHandoffEntry:
    dq 0
InuApTrampolinePermanentDescriptorState:
    dq 0

bits 64
align 16
InuApTrampolineLongMode:
    mov ax, 0x10
    mov ds, ax
    mov es, ax
    mov ss, ax

    ; Every AP starts from architectural reset state, not the BSP/UEFI FPU state.
    ; NativeAOT-generated managed code and InuX64SwitchThreadContext may use XMM
    ; registers immediately, so enable x87/SSE state before calling any managed code.
    mov rax, cr0
    and rax, ~0x0C                  ; clear EM and TS
    or rax, 0x22                    ; set MP and NE
    mov cr0, rax
    mov rax, cr4
    or rax, 0x600                   ; OSFXSR | OSXMMEXCPT
    mov cr4, rax
    fninit

    xor ebp, ebp
    lea rsi, [rel InuApTrampolineCr3]
    mov rsp, [rsi + (InuApTrampolineStackTop - InuApTrampolineCr3)]
    and rsp, -16

    ; Replace the reusable low-memory bootstrap GDT with this processor's permanent
    ; GDT/TSS before managed execution.  The shared runtime IDT uses IST1/IST2/IST3
    ; for double fault, NMI, and machine check, therefore every AP requires its own
    ; loaded TSS and emergency stacks rather than inheriting/resetting TR state.
    mov rax, [rsi + (InuApTrampolinePermanentDescriptorState - InuApTrampolineCr3)]
    test rax, rax
    jz .permanent_descriptors_done
    lgdt [rax + 176]
    mov ax, 0x10
    mov ds, ax
    mov es, ax
    mov ss, ax
    mov ax, 0x28
    ltr ax
.permanent_descriptors_done:

    mov eax, 1
    cpuid
    shr ebx, 24
    mov [rsi + (InuApTrampolineObservedApicId - InuApTrampolineCr3)], ebx
    ; Consume every BSP-patched datum while still executing from the copied low-memory
    ; page, then leave that page with an absolute jump.  An ordinary relative CALL to
    ; InuManagedSchedulerApplicationProcessorEntry is not relocatable after the
    ; trampoline bytes are copied below 1 MiB, and publishing startup before instruction
    ; fetch has left the reusable page lets the BSP overwrite code that this AP is still
    ; executing.  The permanent handoff stub publishes startup only after RIP is outside
    ; the copied page, then enters the NativeAOT scheduler from its link-time address.
    lidt [rsi + (InuApTrampolineIdtr - InuApTrampolineCr3)]
    mov rax, [rsi + (InuApTrampolineHandoffEntry - InuApTrampolineCr3)]
    test rax, rax
    jz .ap_park
    lea rdx, [rsi + (InuApTrampolineStatus - InuApTrampolineCr3)]
    mov ecx, ebx
    jmp rax
.ap_park:
    cli
    hlt
    jmp .ap_park
InuApTrampolineTemplateEnd:

section .text
bits 64
InuX64GetCurrentApicId:
    push rbx
    xor eax, eax
    cpuid
    cmp eax, 0x0B
    jb .legacy_apic_id
    mov eax, 0x0B
    xor ecx, ecx
    cpuid
    test ebx, ebx
    jz .legacy_apic_id
    mov eax, edx
    pop rbx
    ret
.legacy_apic_id:
    mov eax, 1
    cpuid
    mov eax, ebx
    shr eax, 24
    pop rbx
    ret

; Permanent AP descriptor-state layout (256 bytes allocated by the BSP):
;   +0   56-byte GDT matching the BSP selector layout
;   +64  104-byte x64 TSS
;   +176 10-byte GDTR (limit + base)
; RCX=descriptor state, RDX=RSP0/kernel stack top, R8=IST1 double-fault top,
; R9=IST2 NMI top, [rsp+40]=IST3 machine-check top (Microsoft x64 ABI).
InuX64PrepareApplicationProcessorDescriptorState:
    test rcx, rcx
    jz .descriptor_prepare_failed
    test rdx, rdx
    jz .descriptor_prepare_failed
    test r8, r8
    jz .descriptor_prepare_failed
    test r9, r9
    jz .descriptor_prepare_failed
    mov r10, [rsp + 40]
    test r10, r10
    jz .descriptor_prepare_failed

    xor eax, eax
    xor r11d, r11d
.descriptor_clear:
    mov [rcx + r11], rax
    add r11, 8
    cmp r11, 256
    jb .descriptor_clear

    ; GDT: null, ring-0 code/data, ring-3 data/code, then 16-byte TSS descriptor.
    mov rax, 0x00AF9A000000FFFF
    mov [rcx + 8], rax
    mov rax, 0x00CF92000000FFFF
    mov [rcx + 16], rax
    mov rax, 0x00CFF2000000FFFF
    mov [rcx + 24], rax
    mov rax, 0x00AFFA000000FFFF
    mov [rcx + 32], rax

    lea r11, [rcx + 64]
    mov [r11 + 4], rdx             ; TSS.RSP0
    mov [r11 + 36], r8             ; TSS.IST1 double fault
    mov [r11 + 44], r9             ; TSS.IST2 NMI
    mov [r11 + 52], r10            ; TSS.IST3 machine check
    mov word [r11 + 102], 104

    ; Encode the 64-bit available-TSS descriptor at selector 0x28.
    mov rax, r11
    mov r8, rax
    and r8, 0xFFFFFF
    shl r8, 16
    or r8, 103
    mov r9, rax
    shr r9, 24
    and r9, 0xFF
    shl r9, 56
    or r8, r9
    mov r9, 0x0000890000000000
    or r8, r9
    mov [rcx + 40], r8
    shr rax, 32
    mov [rcx + 48], rax

    mov word [rcx + 176], 55
    mov [rcx + 178], rcx
    mov eax, 1
    ret
.descriptor_prepare_failed:
    xor eax, eax
    ret

; Legacy 3-argument trampoline preparation ABI retained for API compatibility.
; It now mirrors BSP EFER.NXE but does not install a permanent AP TSS.
; RCX = low-memory trampoline address, RDX = active CR3, R8 = AP stack top.
InuX64PrepareApplicationProcessorTrampoline:
    xor r9d, r9d
    jmp InuX64PrepareApplicationProcessorTrampolineCommon

; Preferred SMP path with permanent per-AP GDT/TSS.
; RCX = low-memory trampoline address, RDX = active CR3, R8 = AP stack top,
; R9 = permanent AP descriptor-state address.
InuX64PrepareApplicationProcessorTrampolineWithDescriptorState:
    test r9, r9
    jz InuX64PrepareApplicationProcessorTrampolineFailed
InuX64PrepareApplicationProcessorTrampolineCommon:
    test rcx, rcx
    jz InuX64PrepareApplicationProcessorTrampolineFailed
    test rcx, 0xFFF
    jnz InuX64PrepareApplicationProcessorTrampolineFailed
    cmp rcx, 0x100000
    jae InuX64PrepareApplicationProcessorTrampolineFailed
    test rdx, rdx
    jz InuX64PrepareApplicationProcessorTrampolineFailed
    mov rax, 0xFFFFFFFF
    cmp rdx, rax
    ja InuX64PrepareApplicationProcessorTrampolineFailed
    test r8, r8
    jz InuX64PrepareApplicationProcessorTrampolineFailed

    push rsi
    push rdi
    mov r10, rcx
    mov r11, rdx                   ; preserve CR3 across RDMSR below
    lea rsi, [rel InuApTrampolineTemplate]
    mov rdi, r10
    mov ecx, InuApTrampolineTemplateEnd - InuApTrampolineTemplate
    rep movsb

    lea rax, [r10 + (InuApTrampolineGdt - InuApTrampolineTemplate)]
    mov [r10 + (InuApTrampolineGdtBase - InuApTrampolineTemplate)], eax
    lea rax, [r10 + (InuApTrampolineLongMode - InuApTrampolineTemplate)]
    mov [r10 + (InuApTrampolineFarTarget - InuApTrampolineTemplate)], eax
    mov [r10 + (InuApTrampolineCr3 - InuApTrampolineTemplate)], r11d
    mov [r10 + (InuApTrampolineStackTop - InuApTrampolineTemplate)], r8
    mov [r10 + (InuApTrampolinePermanentDescriptorState - InuApTrampolineTemplate)], r9
    ; Mirror the BSP's execute-disable enablement onto the AP before paging begins.
    ; LME is always required; NXE is copied only when already active on the BSP.
    mov ecx, 0xC0000080
    rdmsr
    and eax, 0x00000800             ; EFER.NXE
    or eax, 0x00000100              ; EFER.LME
    mov [r10 + (InuApTrampolineEferEnableBits - InuApTrampolineTemplate)], eax
    sidt [r10 + (InuApTrampolineIdtr - InuApTrampolineTemplate)]
    lea rax, [rel InuX64ApplicationProcessorEntryHandoff]
    mov [r10 + (InuApTrampolineHandoffEntry - InuApTrampolineTemplate)], rax
    mov dword [r10 + (InuApTrampolineStatus - InuApTrampolineTemplate)], 0
    mov dword [r10 + (InuApTrampolineObservedApicId - InuApTrampolineTemplate)], 0xFFFFFFFF
    mfence
    pop rdi
    pop rsi
    mov eax, 1
    ret
InuX64PrepareApplicationProcessorTrampolineFailed:
    xor eax, eax
    ret

InuX64GetApplicationProcessorStartupStatus:
    test rcx, rcx
    jz .status_zero
    mov eax, [rcx + (InuApTrampolineStatus - InuApTrampolineTemplate)]
    ret
.status_zero:
    xor eax, eax
    ret

InuX64GetApplicationProcessorObservedApicId:
    test rcx, rcx
    jz .observed_invalid
    mov eax, [rcx + (InuApTrampolineObservedApicId - InuApTrampolineTemplate)]
    ret
.observed_invalid:
    mov eax, 0xFFFFFFFF
    ret

; Permanent AP transition target.  RCX=observed APIC id, RDX=low-memory startup-status*.
; The copied trampoline reaches this label through a BSP-patched absolute pointer, so
; no link-time relative branch is ever executed from the relocated low-memory bytes.
; Publishing the startup marker here also proves that instruction fetch has left the
; shared trampoline page before the BSP is allowed to reuse it for the next processor.
InuX64ApplicationProcessorEntryHandoff:
    test rdx, rdx
    jz .ap_handoff_park
    mov dword [rdx], 1
    mfence
    sub rsp, 32
    call InuManagedSchedulerApplicationProcessorEntry
    add rsp, 32
.ap_handoff_park:
    cli
.ap_handoff_halt:
    hlt
    jmp .ap_handoff_halt


; Thread context layout (256 bytes):
;  0 RBX, 8 RBP, 16 RDI, 24 RSI, 32 R12, 40 R13, 48 R14, 56 R15,
; 64 RSP, 72 RIP, 80..239 XMM6..XMM15, 240 initial RCX argument.
; RCX=context, RDX=stack top, R8=entry point, R9=argument.
InuX64GetCurrentStackPointer:
    lea rax, [rsp + 8]
    ret

InuX64InitializeThreadContext:
    test rcx, rcx
    jz .thread_init_failed
    test rdx, rdx
    jz .thread_init_failed
    test r8, r8
    jz .thread_init_failed
    pxor xmm0, xmm0
    xor rax, rax
    mov [rcx + 0], rax
    mov [rcx + 8], rax
    mov [rcx + 16], rax
    mov [rcx + 24], rax
    mov [rcx + 32], rax
    mov [rcx + 40], rax
    mov [rcx + 48], rax
    mov [rcx + 56], rax
    movdqu [rcx + 80], xmm0
    movdqu [rcx + 96], xmm0
    movdqu [rcx + 112], xmm0
    movdqu [rcx + 128], xmm0
    movdqu [rcx + 144], xmm0
    movdqu [rcx + 160], xmm0
    movdqu [rcx + 176], xmm0
    movdqu [rcx + 192], xmm0
    movdqu [rcx + 208], xmm0
    movdqu [rcx + 224], xmm0
    and rdx, -16
    sub rdx, 40
    lea rax, [rel InuX64ThreadReturned]
    mov [rdx], rax
    mov [rcx + 64], rdx
    mov [rcx + 72], r8
    mov [rcx + 240], r9
    mov eax, 1
    ret
.thread_init_failed:
    xor eax, eax
    ret

; RCX=current context, RDX=next context.
InuX64SwitchThreadContext:
    test rcx, rcx
    jz .thread_switch_failed
    test rdx, rdx
    jz .thread_switch_failed
    mov [rcx + 0], rbx
    mov [rcx + 8], rbp
    mov [rcx + 16], rdi
    mov [rcx + 24], rsi
    mov [rcx + 32], r12
    mov [rcx + 40], r13
    mov [rcx + 48], r14
    mov [rcx + 56], r15
    lea rax, [rsp + 8]
    mov [rcx + 64], rax
    mov rax, [rsp]
    mov [rcx + 72], rax
    movdqu [rcx + 80], xmm6
    movdqu [rcx + 96], xmm7
    movdqu [rcx + 112], xmm8
    movdqu [rcx + 128], xmm9
    movdqu [rcx + 144], xmm10
    movdqu [rcx + 160], xmm11
    movdqu [rcx + 176], xmm12
    movdqu [rcx + 192], xmm13
    movdqu [rcx + 208], xmm14
    movdqu [rcx + 224], xmm15

    mov rbx, [rdx + 0]
    mov rbp, [rdx + 8]
    mov rdi, [rdx + 16]
    mov rsi, [rdx + 24]
    mov r12, [rdx + 32]
    mov r13, [rdx + 40]
    mov r14, [rdx + 48]
    mov r15, [rdx + 56]
    movdqu xmm6, [rdx + 80]
    movdqu xmm7, [rdx + 96]
    movdqu xmm8, [rdx + 112]
    movdqu xmm9, [rdx + 128]
    movdqu xmm10, [rdx + 144]
    movdqu xmm11, [rdx + 160]
    movdqu xmm12, [rdx + 176]
    movdqu xmm13, [rdx + 192]
    movdqu xmm14, [rdx + 208]
    movdqu xmm15, [rdx + 224]
    mov rsp, [rdx + 64]
    mov rcx, [rdx + 240]
    mov r10, [rdx + 72]
    mov eax, 1
    jmp r10
.thread_switch_failed:
    xor eax, eax
    ret

; Kernel thread entry points are non-returning at this roadmap stage.
InuX64ThreadReturned:
    cli
.thread_return_halt:
    hlt
    jmp .thread_return_halt

; User/kernel separation primitives.
InuX64IsExecuteDisableEnabled:
    mov ecx, 0xC0000080             ; IA32_EFER
    rdmsr
    shr eax, 11                     ; EFER.NXE
    and eax, 1
    ret

InuX64EnableKernelWriteProtect:
    mov rax, cr0
    bts rax, 16                     ; CR0.WP
    mov cr0, rax
    mov eax, 1
    ret

InuX64IsKernelWriteProtectEnabled:
    mov rax, cr0
    shr rax, 16
    and eax, 1
    ret

InuX64SupportsSmep:
    push rbx
    xor eax, eax
    cpuid
    cmp eax, 7
    jb .smep_not_supported
    mov eax, 7
    xor ecx, ecx
    cpuid
    bt ebx, 7
    setc al
    movzx eax, al
    pop rbx
    ret
.smep_not_supported:
    xor eax, eax
    pop rbx
    ret

InuX64EnableSmep:
    push rbx
    xor eax, eax
    cpuid
    cmp eax, 7
    jb .smep_unsupported
    mov eax, 7
    xor ecx, ecx
    cpuid
    bt ebx, 7
    jnc .smep_unsupported
    mov rax, cr4
    bts rax, 20                    ; CR4.SMEP
    mov cr4, rax
    mov eax, 1
    pop rbx
    ret
.smep_unsupported:
    xor eax, eax
    pop rbx
    ret

InuX64SupportsSmap:
    push rbx
    xor eax, eax
    cpuid
    cmp eax, 7
    jb .smap_not_supported
    mov eax, 7
    xor ecx, ecx
    cpuid
    bt ebx, 20
    setc al
    movzx eax, al
    pop rbx
    ret
.smap_not_supported:
    xor eax, eax
    pop rbx
    ret



; Returns the NativeAOT-exported scheduler idle-thread entry point.
InuX64GetSchedulerIdleThreadEntryPoint:
    lea rax, [rel InuManagedSchedulerIdleThread]
    ret

; Returns the NativeAOT-exported scheduler role-worker entry point.
InuX64GetSchedulerRoleWorkerEntryPoint:
    lea rax, [rel InuManagedSchedulerRoleWorker]
    ret

; Returns the NativeAOT-exported one-shot scheduler-thread entry point.
InuX64GetSchedulerOneShotThreadEntryPoint:
    lea rax, [rel InuManagedSchedulerOneShotThread]
    ret
