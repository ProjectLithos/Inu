bits 64
default rel


; NativeAOT module-table bookends. ILC contributes one .modules$I entry for
; each managed module. COFF '$' subsection ordering places these sentinels
; around every ILC entry once lld-link merges .modules into .rdata.
section .modules$A align=8
global __modules_a
__modules_a:
    dq 0

section .modules$Z align=8
global __modules_z
__modules_z:
    dq 0

section .text

global InuKernelEntry
%ifdef INU_DEBUG
global InuDebugImageAnchor
global InuDebugResume
%endif
global InuCaptureUefiFramebuffer
global InuCaptureFinalUefiMemoryMap
global InuCaptureUefiAcpiRoot
global InuX64NmiDiagnosticState
global InuX64GetNmiDiagnosticStateAddress
extern InuRuntimeInitialize
extern InuManagedEntry
extern InuX64Halt
extern __ReadyToRunHeader

; EFI_GRAPHICS_OUTPUT_PROTOCOL_GUID
section .rdata align=16
InuGraphicsOutputProtocolGuid:
    db 0xDE, 0xA9, 0x42, 0x90, 0xDC, 0x23, 0x38, 0x4A
    db 0x96, 0xFB, 0x7A, 0xDE, 0xD0, 0x80, 0x51, 0x6A
InuSerialEntryTag: db 'NOKERN:ENTRY',10
InuSerialFramebufferFailTag: db 'NOKERN:WARN:GOP',10
InuSerialFramebufferOkTag: db 'NOKERN:GOP',10
InuSerialMemoryMapOkTag: db 'NOKERN:MEMMAP',10
InuSerialMemoryMapPlanFallbackTag: db 'NOKERN:MMAP:PLAN:FALLBACK',10
InuSerialMemoryMapAllocFallbackTag: db 'NOKERN:MMAP:ALLOC:FALLBACK',10
InuSerialMemoryMapFinalGetFailTag: db 'NOKERN:FAIL:MEMMAP:GET',10
InuSerialMemoryMapServicesFailTag: db 'NOKERN:FAIL:MEMMAP:SERVICES',10
InuSerialMemoryMapWorkspaceFailTag: db 'NOKERN:FAIL:MEMMAP:WORKSPACE',10
InuSerialMemoryMapMetadataFailTag: db 'NOKERN:FAIL:MEMMAP:METADATA',10
InuSerialMemoryMapRetriesFailTag: db 'NOKERN:FAIL:MEMMAP:RETRIES',10
InuSerialMemoryMapExitFailTag: db 'NOKERN:FAIL:MEMMAP:EXIT',10
InuSerialMemoryMapRetryTag: db 'NOKERN:MMAP:RETRY',10
InuSerialMemoryMapStatusTag: db 'NOKERN:MMAP:STATUS='
InuSerialRuntimeFailTag: db 'NOKERN:FAIL:RUNTIME',10
InuSerialRuntimeOkTag: db 'NOKERN:RUNTIME',10
InuSerialManagedTag: db 'NOKERN:MANAGED',10
%ifdef INU_DEBUG
InuDebugResumeTag: db 'NORESUME'
InuDebugRuntimeTag: db 'NORTINIT'
InuDebugManagedTag: db 'NOMANAGE'
%endif

; Native boot context consumed by the managed no-CoreLib bootstrap.
; 00 UInt64 signature (ASCII "INU")
; 08 UInt64 framebuffer address
; 10 UInt64 framebuffer size
; 18 UInt32 width
; 1C UInt32 height
; 20 UInt32 pixels per scan line
; 24 UInt32 UEFI pixel format
; 28 UInt32 red mask
; 2C UInt32 green mask
; 30 UInt32 blue mask
; 34 UInt32 reserved mask
; 38 UInt64 final UEFI memory-map address
; 40 UInt64 final UEFI memory-map byte length
; 48 UInt64 final UEFI map key accepted by ExitBootServices
; 50 UInt64 UEFI memory descriptor size
; 58 UInt32 UEFI memory descriptor version
; 5C UInt32 GetMemoryMap/ExitBootServices capture attempts
; 60 UInt64 final EFI_STATUS (zero on success)
; 68 UInt64 final-map flag (one only after ExitBootServices succeeds)
; 70 UInt64 UEFI-allocated bootstrap page-table workspace address
; 78 UInt64 UEFI-allocated bootstrap page-table workspace page count
; 80 UInt64 ACPI RSDP physical address from UEFI configuration tables
; 88 UInt64 UEFI-reserved application-processor SIPI trampoline address
; 90 UInt64 application-processor trampoline page count
; 98 UInt64 bootloader-preloaded system asset bundle address
; A0 UInt64 system asset bundle byte length
; A8 UInt64 loaded kernel image base
; B0 UInt64 preserved EFI image handle
; B8 UInt64 preserved EFI system-table pointer
section .data align=16
%ifdef INU_DEBUG
InuDebugSavedRflags:
    dq 0
%endif
InuBootContext:
    dq 0x4E59524F41564F4E
    dq 0
    dq 0
    dd 0
    dd 0
    dd 0
    dd 0
    dd 0
    dd 0
    dd 0
    dd 0
    dq InuFinalMemoryMapBuffer
    dq 0
    dq 0
    dq 0
    dd 0
    dd 0
    dq 0
    dq 0
    dq 0
    dq 0
    dq 0
    dq 0
    dq 0
    dq 0
    dq 0
    dq 0
    dq 0
    dq 0

; The final memory map must be captured into storage allocated before the last
; GetMemoryMap call. A fixed 512 KiB buffer provides descriptor-growth headroom
; without calling AllocatePool between GetMemoryMap and ExitBootServices.
section .bss align=4096
; Early NMI takeover/diagnostic state.  Firmware is allowed to leave LAPIC LVT
; state behind.  Inu snapshots it immediately after ExitBootServices, masks the
; firmware-owned NMI-producing LVT entries, and later applies the MADT policy.
; Layout (qwords): magic,count,initial APIC_BASE,LINT0,LINT1,thermal,perf,port61,
; last APIC_BASE,LINT0,LINT1,thermal,perf,ESR,port61,flags.
InuX64NmiDiagnosticState:
    resq 16
InuFinalMemoryMapBuffer:
    resb 524288
InuFinalMemoryMapBufferEnd:

; Inu must not continue managed execution on firmware-owned stack storage after
; ExitBootServices. Keep an image-owned, page-aligned bootstrap stack alive until
; the kernel stack arena is established by a later subsystem.  This is NOBITS/BSS
; storage, so use ALIGNB: ALIGN would try to emit fill bytes and NASM quite
; correctly discards those initializers with a warning.
alignb 4096
InuBootstrapStack:
    resb 1048576
InuBootstrapStackEnd:

section .text
global InuX64GetBootstrapStackBase
global InuX64GetBootstrapStackTop
global InuX64GetManagedModuleTableStart
global InuX64GetManagedModuleTableEnd
global InuX64GetManagedReadyToRunHeader
InuX64GetBootstrapStackBase:
    lea rax, [rel InuBootstrapStack]
    ret
InuX64GetBootstrapStackTop:
    lea rax, [rel InuBootstrapStackEnd]
    ret
InuX64GetManagedModuleTableStart:
    lea rax, [rel __modules_a]
    ret
InuX64GetManagedModuleTableEnd:
    lea rax, [rel __modules_z]
    ret
InuX64GetManagedReadyToRunHeader:
    lea rax, [rel __ReadyToRunHeader]
    ret
InuX64GetNmiDiagnosticStateAddress:
    lea rax, [rel InuX64NmiDiagnosticState]
    ret

; Takes ownership of firmware NMI routing before managed bootstrap can be
; interrupted by stale LAPIC/PC chipset state.  This does not discard the
; firmware state: the original LVT values are retained above for diagnostics.
; The MADT-driven managed policy later selectively re-enables declared NMIs.
InuX64QuiesceFirmwareNmiSources:
    ; Mark diagnostic state as valid and remember the legacy chipset NMI status.
    mov rax, 0x31494D4E554E49       ; "INUNMI1" little-endian-ish marker
    mov [rel InuX64NmiDiagnosticState + 0], rax
    in al, 0x61
    movzx eax, al
    mov [rel InuX64NmiDiagnosticState + 56], rax
    ; Mask the PC/AT NMI gate while Inu replaces firmware interrupt policy.
    mov al, 0x80
    out 0x70, al

    mov ecx, 0x1B                  ; IA32_APIC_BASE
    rdmsr
    mov r8d, eax
    mov r9d, edx
    mov eax, r8d
    mov edx, r9d
    shl rdx, 32
    or rax, rdx
    mov [rel InuX64NmiDiagnosticState + 16], rax
    test r8d, (1 << 11)
    jz .done
    test r8d, (1 << 10)
    jnz .x2apic

    ; xAPIC MMIO. Preserve firmware values, then mask LINT0/LINT1, thermal and
    ; performance-monitor LVTs so none can asynchronously inherit an NMI mode.
    mov eax, r8d
    and eax, 0xFFFFF000
    mov edx, r9d
    and edx, 0xF
    shl rdx, 32
    or rax, rdx
    mov r10, rax
    mov eax, [r10 + 0x350]
    mov [rel InuX64NmiDiagnosticState + 24], rax
    or eax, (1 << 16)
    mov [r10 + 0x350], eax
    mov eax, [r10 + 0x360]
    mov [rel InuX64NmiDiagnosticState + 32], rax
    or eax, (1 << 16)
    mov [r10 + 0x360], eax
    mov eax, [r10 + 0x330]
    mov [rel InuX64NmiDiagnosticState + 40], rax
    or eax, (1 << 16)
    mov [r10 + 0x330], eax
    mov eax, [r10 + 0x340]
    mov [rel InuX64NmiDiagnosticState + 48], rax
    or eax, (1 << 16)
    mov [r10 + 0x340], eax
    or qword [rel InuX64NmiDiagnosticState + 120], 1
    jmp .done
.x2apic:
    mov ecx, 0x835                 ; x2APIC LVT LINT0
    rdmsr
    mov [rel InuX64NmiDiagnosticState + 24], rax
    or eax, (1 << 16)
    xor edx, edx
    wrmsr
    mov ecx, 0x836                 ; x2APIC LVT LINT1
    rdmsr
    mov [rel InuX64NmiDiagnosticState + 32], rax
    or eax, (1 << 16)
    xor edx, edx
    wrmsr
    mov ecx, 0x833                 ; x2APIC LVT thermal
    rdmsr
    mov [rel InuX64NmiDiagnosticState + 40], rax
    or eax, (1 << 16)
    xor edx, edx
    wrmsr
    mov ecx, 0x834                 ; x2APIC LVT performance monitor
    rdmsr
    mov [rel InuX64NmiDiagnosticState + 48], rax
    or eax, (1 << 16)
    xor edx, edx
    wrmsr
    or qword [rel InuX64NmiDiagnosticState + 120], 2
.done:
    ret

InuKernelEntry:
    ; Preserve every UEFI entry argument before the first serial/debug/firmware call.
    ; RCX/RDX are volatile in the Microsoft x64 ABI; the early serial emitter itself
    ; consumes them, so deferring this capture destroys ImageHandle/SystemTable.
    mov [rel InuBootContext + 0xB0], rcx
    mov [rel InuBootContext + 0xB8], rdx
    mov [rel InuBootContext + 0x98], r8
    mov [rel InuBootContext + 0xA0], r9
    mov rax, [rsp + 0x28]
    mov [rel InuBootContext + 0xA8], rax
    lea rcx, [rel InuSerialEntryTag]
    mov edx, 13
    call InuEarlySerialEmit
%ifdef INU_DEBUG
InuDebugImageAnchor:
    ; Preserve the firmware flags in image-owned memory. Debugger/GDB traffic is allowed to
    ; alter volatile registers, so the resume path must never depend on a register surviving
    ; the rendezvous. Keep interrupts enabled while HLT parks the CPU so QEMU remains responsive.
    pushfq
    pop rax
    mov [rel InuDebugSavedRflags], rax
    sti
    ; Publish the actual runtime-relocated anchor address through QEMU isa-debugcon
    ; (I/O port 0xE9), then wait in a private rendezvous loop while the IDE arms
    ; source breakpoints. A literal INT3 is not used because system emulation can
    ; deliver it as a guest #BP exception instead of a GDB stop packet.
    ; Record: ASCII "NODBG64!" followed by the 64-bit little-endian anchor address.
    mov r10, rdx
    lea r9, [rel InuDebugImageAnchor]
    mov dx, 0x00E9
    mov al, 'N'
    out dx, al
    mov al, 'O'
    out dx, al
    mov al, 'D'
    out dx, al
    mov al, 'B'
    out dx, al
    mov al, 'G'
    out dx, al
    mov al, '6'
    out dx, al
    mov al, '4'
    out dx, al
    mov al, '!'
    out dx, al
    mov rax, r9
    mov r8d, 8
.debug_address_loop:
    out dx, al
    shr rax, 8
    dec r8d
    jnz .debug_address_loop
    mov rdx, r10
.debug_rendezvous:
    hlt
    jmp .debug_rendezvous
InuDebugResume:
    ; The IDE rewrites RIP here after arming breakpoints. Disable interrupts only while
    ; publishing the resume checkpoint, then restore the exact firmware flags from memory.
    cli
    push rcx
    push rdx
    lea rcx, [rel InuDebugResumeTag]
    mov edx, 8
    call InuDebugEmitTag
    pop rdx
    pop rcx
    mov rax, [rel InuDebugSavedRflags]
    push rax
    popfq
%endif
    ; UEFI x64 enters with ImageHandle in RCX and EFI_SYSTEM_TABLE* in RDX.
    ; Preserve both values and maintain Windows x64 shadow space/alignment.
    push rbp
    mov rbp, rsp
    push r12
    push r13
    sub rsp, 32
    mov r12, [rel InuBootContext + 0xB0]
    mov r13, [rel InuBootContext + 0xB8]

    mov rcx, r13
    call InuCaptureUefiAcpiRoot

    mov rcx, r13
    call InuCaptureUefiFramebuffer
    test al, al
    jnz .framebuffer_ok
    ; GOP is optional. Serial remains the guaranteed bootstrap console and a
    ; later graphics driver may publish a display after managed startup.
    lea rcx, [rel InuSerialFramebufferFailTag]
    mov edx, 16
    call InuEarlySerialEmit
    jmp .framebuffer_done
.framebuffer_ok:
    lea rcx, [rel InuSerialFramebufferOkTag]
    mov edx, 11
    call InuEarlySerialEmit
.framebuffer_done:

    ; This routine obtains the map whose key is passed immediately to
    ; ExitBootServices. A stale key causes a fresh GetMemoryMap retry.
    mov rcx, r12
    mov rdx, r13
    call InuCaptureFinalUefiMemoryMap
    test al, al
    jnz .memory_map_ok
    ; InuCaptureFinalUefiMemoryMap emits the precise failure stage and status.
    ; Do not overwrite that diagnostic with the old generic MEMMAP marker.
    jmp InuX64Halt
.memory_map_ok:
    lea rcx, [rel InuSerialMemoryMapOkTag]
    mov edx, 14
    call InuEarlySerialEmit

    ; ExitBootServices has succeeded. Abandon the firmware stack before any
    ; managed allocator or page-table code can reclaim firmware-owned storage.
    lea rsp, [rel InuBootstrapStackEnd]
    and rsp, -16
    xor ebp, ebp
    sub rsp, 32                 ; Windows x64 shadow space for subsequent calls.

    ; Firmware interrupt-controller programming is no longer authoritative once
    ; ExitBootServices succeeds. Quiesce inherited NMI sources before managed code.
    call InuX64QuiesceFirmwareNmiSources

%ifdef INU_DEBUG
    push rcx
    push rdx
    lea rcx, [rel InuDebugRuntimeTag]
    mov edx, 8
    call InuDebugEmitTag
    pop rdx
    pop rcx
%endif
    call InuRuntimeInitialize
    test al, al
    jnz .runtime_ok
    lea rcx, [rel InuSerialRuntimeFailTag]
    mov edx, 20
    call InuEarlySerialEmit
    jmp InuX64Halt
.runtime_ok:
    lea rcx, [rel InuSerialRuntimeOkTag]
    mov edx, 15
    call InuEarlySerialEmit

    cli
%ifdef INU_DEBUG
    push rcx
    push rdx
    lea rcx, [rel InuDebugManagedTag]
    mov edx, 8
    call InuDebugEmitTag
    pop rdx
    pop rcx
%endif
    lea rcx, [rel InuSerialManagedTag]
    mov edx, 15
    call InuEarlySerialEmit
    lea rcx, [rel InuBootContext]
    call InuManagedEntry
    jmp InuX64Halt



; RCX = bytes, EDX = count. Mirrors pre-managed boot breadcrumbs to COM1.
; COM1 is configured by BOOTX64.EFI; this routine only polls THR-empty and writes.
InuEarlySerialEmit:
    push rax
    push rcx
    push rdx
    push rsi
    push r8
    mov rsi, rcx
    mov r8d, edx
.serial_next:
    test r8d, r8d
    jz .serial_done
    mov ecx, 1000000
.serial_wait:
    mov dx, 0x03FD
    in al, dx
    test al, 0x20
    jnz .serial_ready
    dec ecx
    jnz .serial_wait
    jmp .serial_done
.serial_ready:
    mov dx, 0x03F8
    mov al, [rsi]
    out dx, al
    inc rsi
    dec r8d
    jmp .serial_next
.serial_done:
    pop r8
    pop rsi
    pop rdx
    pop rcx
    pop rax
    ret

; RCX = EFI_STATUS/value. Emits NOKERN:MMAP:STATUS=0xXXXXXXXXXXXXXXXX followed by LF.
InuEarlySerialEmitStatus:
    push rax
    push rcx
    push rdx
    push r8
    push r9
    lea rcx, [rel InuSerialMemoryMapStatusTag]
    mov edx, 19
    call InuEarlySerialEmit
    mov r9, [rsp + 24]          ; original RCX saved by push rcx
    mov dx, 0x03F8
    mov al, '0'
    out dx, al
    mov al, 'x'
    out dx, al
    mov r8d, 16
.status_hex_loop:
    mov rax, r9
    shr rax, 60
    cmp al, 9
    jbe .status_decimal
    add al, 'A' - 10
    jmp .status_write
.status_decimal:
    add al, '0'
.status_write:
    mov dx, 0x03F8
    out dx, al
    shl r9, 4
    dec r8d
    jnz .status_hex_loop
    mov al, 10
    out dx, al
    pop r9
    pop r8
    pop rdx
    pop rcx
    pop rax
    ret

%ifdef INU_DEBUG
; RCX = tag bytes, EDX = byte count. Preserves all registers it touches.
InuDebugEmitTag:
    push rax
    push rcx
    push rdx
    push rsi
    mov rsi, rcx
    mov ecx, edx
    mov dx, 0x00E9
.debug_tag_loop:
    mov al, [rsi]
    out dx, al
    inc rsi
    dec ecx
    jnz .debug_tag_loop
    pop rsi
    pop rdx
    pop rcx
    pop rax
    ret
%endif


; Captures the RSDP pointer from the UEFI configuration table before ExitBootServices.
; RCX = EFI_SYSTEM_TABLE*. Prefer ACPI 2.0 GUID, then fall back to ACPI 1.0 GUID.
InuCaptureUefiAcpiRoot:
    push rbx
    push rsi
    push rdi
    mov qword [rel InuBootContext + 0x80], 0
    test rcx, rcx
    jz .acpi_failed
    mov rbx, [rcx + 0x68]       ; NumberOfTableEntries
    mov rsi, [rcx + 0x70]       ; ConfigurationTable
    test rbx, rbx
    jz .acpi_failed
    test rsi, rsi
    jz .acpi_failed
    xor edi, edi                ; ACPI 1.0 fallback RSDP
.acpi_scan:
    ; EFI_ACPI_20_TABLE_GUID = 8868e871-e4f1-11d3-bc22-0080c73c8881
    mov rax, [rsi]
    mov rdx, 0x11D3E4F18868E871
    cmp rax, rdx
    jne .acpi_check_v1
    mov rax, [rsi + 8]
    mov rdx, 0x81883CC7800022BC
    cmp rax, rdx
    jne .acpi_check_v1
    mov rax, [rsi + 16]
    test rax, rax
    jz .acpi_check_v1
    mov [rel InuBootContext + 0x80], rax
    mov al, 1
    jmp .acpi_return
.acpi_check_v1:
    ; ACPI_TABLE_GUID = eb9d2d30-2d88-11d3-9a16-0090273fc14d
    mov rax, [rsi]
    mov rdx, 0x11D32D88EB9D2D30
    cmp rax, rdx
    jne .acpi_next
    mov rax, [rsi + 8]
    mov rdx, 0x4DC13F279000169A
    cmp rax, rdx
    jne .acpi_next
    mov rax, [rsi + 16]
    test rax, rax
    jz .acpi_next
    mov rdi, rax
.acpi_next:
    add rsi, 24
    dec rbx
    jnz .acpi_scan
    test rdi, rdi
    jz .acpi_failed
    mov [rel InuBootContext + 0x80], rdi
    mov al, 1
    jmp .acpi_return
.acpi_failed:
    xor eax, eax
.acpi_return:
    pop rdi
    pop rsi
    pop rbx
    ret

InuCaptureFinalUefiMemoryMap:
    ; RCX = EFI_HANDLE ImageHandle, RDX = EFI_SYSTEM_TABLE*.
    push rbx
    push rsi
    push rdi
    push r12
    push r13
    push r14
    sub rsp, 40                 ; 32-byte shadow space plus fifth argument.

    mov rbx, rcx
    test rbx, rbx
    jz .services_failed
    test rdx, rdx
    jz .services_failed

    mov rsi, [rdx + 0x60]       ; EFI_SYSTEM_TABLE.BootServices
    test rsi, rsi
    jz .services_failed
    mov r13, [rsi + 0x28]       ; EFI_BOOT_SERVICES.AllocatePages
    mov rdi, [rsi + 0x38]       ; EFI_BOOT_SERVICES.GetMemoryMap
    mov r12, [rsi + 0xE8]       ; EFI_BOOT_SERVICES.ExitBootServices
    test r13, r13
    jz .services_failed
    test rdi, rdi
    jz .services_failed
    test r12, r12
    jz .services_failed

    ; Capture one planning map while Boot Services are still available. This pass is
    ; advisory only: firmware-specific map metadata must never prevent Inu from
    ; reaching the authoritative final GetMemoryMap/ExitBootServices handshake.
    mov qword [rel InuBootContext + 0x40], InuFinalMemoryMapBufferEnd - InuFinalMemoryMapBuffer
    mov qword [rel InuBootContext + 0x48], 0
    mov qword [rel InuBootContext + 0x50], 0
    mov dword [rel InuBootContext + 0x58], 0
    lea rcx, [rel InuBootContext + 0x40]
    lea rdx, [rel InuFinalMemoryMapBuffer]
    lea r8, [rel InuBootContext + 0x48]
    lea r9, [rel InuBootContext + 0x50]
    lea rax, [rel InuBootContext + 0x58]
    mov [rsp + 32], rax
    call rdi
    test rax, rax
    jnz .use_fallback_workspace
    call InuValidateCapturedMap
    test al, al
    jz .use_fallback_workspace

    lea rcx, [rel InuFinalMemoryMapBuffer]
    mov rdx, [rel InuBootContext + 0x40]
    mov r8, [rel InuBootContext + 0x50]
    call InuPlanBootstrapPageTables
    test rax, rax
    jz .use_fallback_workspace
    mov r14, rax
    jmp .allocate_workspace

.use_fallback_workspace:
    ; 1024 pages (4 MiB) is enough bootstrap page-table storage for approximately
    ; one TiB of 2 MiB direct-map leaves, with substantial hierarchy headroom.
    ; Once the PMM/direct map are established, later page tables come from the PMM.
    mov r14d, 1024
    lea rcx, [rel InuSerialMemoryMapPlanFallbackTag]
    mov edx, 26
    call InuEarlySerialEmit

.allocate_workspace:
    mov [rel InuBootContext + 0x78], r14
    mov qword [rel InuBootContext + 0x70], 0

    ; AllocateAnyPages + EfiLoaderData. Allocation intentionally happens before the
    ; final map/key capture and therefore becomes part of the retained final map.
    xor ecx, ecx
    mov edx, 2
    mov r8, r14
    lea r9, [rel InuBootContext + 0x70]
    call r13
    test rax, rax
    jz .workspace_allocated

    ; A calculated workspace can be too fragmented to obtain contiguously even when
    ; the final map itself is perfectly valid. Retry once with the conservative 4 MiB
    ; bootstrap reserve instead of misreporting that condition as a memory-map failure.
    cmp r14, 1024
    jbe .workspace_allocation_failed
    mov r14d, 1024
    lea rcx, [rel InuSerialMemoryMapAllocFallbackTag]
    mov edx, 28
    call InuEarlySerialEmit
    jmp .allocate_workspace

.workspace_allocation_failed:
    mov [rel InuBootContext + 0x60], rax
    lea rcx, [rel InuSerialMemoryMapWorkspaceFailTag]
    mov edx, 29
    call InuEarlySerialEmit
    mov rcx, [rel InuBootContext + 0x60]
    call InuEarlySerialEmitStatus
    jmp .final_failed

.workspace_allocated:
    mov rax, [rel InuBootContext + 0x70]
    test rax, rax
    jz .workspace_address_failed
    test rax, 0xFFF
    jnz .workspace_address_failed

    ; Prove the reserved workspace is writable under the inherited UEFI mappings.
    mov qword [rax], 0
    mov rcx, [rel InuBootContext + 0x78]
    shl rcx, 12
    dec rcx
    mov byte [rax + rcx], 0

    ; Reserve one SIPI target page below 1 MiB while Boot Services can still
    ; satisfy AllocateMaxAddress. Failure is non-fatal: the managed SMP layer
    ; will retain BSP-only operation and report TrampolineUnavailable.
    mov qword [rel InuBootContext + 0x88], 0x000000000009F000
    mov qword [rel InuBootContext + 0x90], 0
    mov ecx, 1                      ; AllocateMaxAddress
    mov edx, 2                      ; EfiLoaderData
    mov r8d, 1                      ; one 4 KiB page
    lea r9, [rel InuBootContext + 0x88]
    call r13
    test rax, rax
    jnz .ap_trampoline_unavailable
    mov rax, [rel InuBootContext + 0x88]
    test rax, rax
    jz .ap_trampoline_unavailable
    test rax, 0xFFF
    jnz .ap_trampoline_unavailable
    cmp rax, 0x100000
    jae .ap_trampoline_unavailable
    mov qword [rax], 0
    mov byte [rax + 4095], 0
    mov qword [rel InuBootContext + 0x90], 1
    jmp .retry_final_map
.ap_trampoline_unavailable:
    mov qword [rel InuBootContext + 0x88], 0
    mov qword [rel InuBootContext + 0x90], 0

.retry_final_map:
    inc dword [rel InuBootContext + 0x5C]
    cmp dword [rel InuBootContext + 0x5C], 8
    ja .retry_exhausted

    mov qword [rel InuBootContext + 0x40], InuFinalMemoryMapBufferEnd - InuFinalMemoryMapBuffer
    mov qword [rel InuBootContext + 0x48], 0
    mov qword [rel InuBootContext + 0x50], 0
    mov dword [rel InuBootContext + 0x58], 0

    lea rcx, [rel InuBootContext + 0x40]
    lea rdx, [rel InuFinalMemoryMapBuffer]
    lea r8, [rel InuBootContext + 0x48]
    lea r9, [rel InuBootContext + 0x50]
    lea rax, [rel InuBootContext + 0x58]
    mov [rsp + 32], rax
    call rdi
    test rax, rax
    jz .final_map_returned
    mov [rel InuBootContext + 0x60], rax
    lea rcx, [rel InuSerialMemoryMapFinalGetFailTag]
    mov edx, 23
    call InuEarlySerialEmit
    mov rcx, [rel InuBootContext + 0x60]
    call InuEarlySerialEmitStatus
    jmp .final_failed
.final_map_returned:

    call InuValidateFinalCapturedMap
    test al, al
    jz .final_metadata_failed

    ; No allocation or firmware operation occurs between the successful
    ; GetMemoryMap above and this ExitBootServices call.
    mov rcx, rbx
    mov rdx, [rel InuBootContext + 0x48]
    call r12
    test rax, rax
    jz .final_succeeded

    mov [rel InuBootContext + 0x60], rax
    mov rdx, 0x8000000000000002 ; EFI_INVALID_PARAMETER: stale map key.
    cmp rax, rdx
    jne .exit_boot_services_failed
    lea rcx, [rel InuSerialMemoryMapRetryTag]
    mov edx, 18
    call InuEarlySerialEmit
    jmp .retry_final_map
.exit_boot_services_failed:
    lea rcx, [rel InuSerialMemoryMapExitFailTag]
    mov edx, 24
    call InuEarlySerialEmit
    mov rcx, [rel InuBootContext + 0x60]
    call InuEarlySerialEmitStatus
    jmp .final_failed

.services_failed:
    mov qword [rel InuBootContext + 0x60], -1
    lea rcx, [rel InuSerialMemoryMapServicesFailTag]
    mov edx, 28
    call InuEarlySerialEmit
    mov rcx, [rel InuBootContext + 0x60]
    call InuEarlySerialEmitStatus
    jmp .final_failed

.workspace_address_failed:
    mov qword [rel InuBootContext + 0x60], -2
    lea rcx, [rel InuSerialMemoryMapWorkspaceFailTag]
    mov edx, 29
    call InuEarlySerialEmit
    mov rcx, [rel InuBootContext + 0x60]
    call InuEarlySerialEmitStatus
    jmp .final_failed

.final_metadata_failed:
    mov qword [rel InuBootContext + 0x60], -3
    lea rcx, [rel InuSerialMemoryMapMetadataFailTag]
    mov edx, 28
    call InuEarlySerialEmit
    mov rcx, [rel InuBootContext + 0x60]
    call InuEarlySerialEmitStatus
    jmp .final_failed

.retry_exhausted:
    mov qword [rel InuBootContext + 0x60], -4
    lea rcx, [rel InuSerialMemoryMapRetriesFailTag]
    mov edx, 27
    call InuEarlySerialEmit
    mov rcx, [rel InuBootContext + 0x60]
    call InuEarlySerialEmitStatus
    jmp .final_failed

.final_succeeded:
    mov qword [rel InuBootContext + 0x60], 0
    mov qword [rel InuBootContext + 0x68], 1
    mov al, 1
    jmp .final_return

.final_failed_status:
    mov [rel InuBootContext + 0x60], rax
    jmp .final_failed

.final_failed_no_status:
    mov qword [rel InuBootContext + 0x60], -5
    lea rcx, [rel InuSerialMemoryMapMetadataFailTag]
    mov edx, 28
    call InuEarlySerialEmit
    mov rcx, [rel InuBootContext + 0x60]
    call InuEarlySerialEmitStatus
.final_failed:
    xor eax, eax
.final_return:
    add rsp, 40
    pop r14
    pop r13
    pop r12
    pop rdi
    pop rsi
    pop rbx
    ret

; Validates the authoritative final map using only the UEFI guarantees Inu
; actually consumes after ExitBootServices. DescriptorSize may grow in future UEFI
; revisions, and MemoryMapSize need not satisfy Inu's former extra divisibility
; policy as long as at least one complete descriptor is present.
InuValidateFinalCapturedMap:
    cmp qword [rel InuBootContext + 0x40], 0
    je .final_invalid
    cmp qword [rel InuBootContext + 0x40], InuFinalMemoryMapBufferEnd - InuFinalMemoryMapBuffer
    ja .final_invalid
    cmp qword [rel InuBootContext + 0x50], 40
    jb .final_invalid
    mov rax, [rel InuBootContext + 0x40]
    cmp rax, [rel InuBootContext + 0x50]
    jb .final_invalid
    mov al, 1
    ret
.final_invalid:
    xor eax, eax
    ret

; Strict validation used only for the advisory planning pass. If firmware metadata
; does not satisfy these planner conveniences, boot falls back to a fixed workspace.
InuValidateCapturedMap:
    cmp qword [rel InuBootContext + 0x40], 0
    je .invalid
    cmp qword [rel InuBootContext + 0x40], InuFinalMemoryMapBufferEnd - InuFinalMemoryMapBuffer
    ja .invalid
    cmp qword [rel InuBootContext + 0x50], 40
    jb .invalid
    test qword [rel InuBootContext + 0x50], 7
    jnz .invalid
    mov rax, [rel InuBootContext + 0x40]
    xor edx, edx
    div qword [rel InuBootContext + 0x50]
    test rdx, rdx
    jnz .invalid
    test rax, rax
    jz .invalid
    mov al, 1
    ret
.invalid:
    xor eax, eax
    ret

; Calculates a conservative page-table workspace from the current UEFI map.
; RCX = map, RDX = map bytes, R8 = descriptor bytes. Returns pages in RAX.
; The plan assumes 2 MiB direct-map leaves; 1 GiB support can only reduce usage.
InuPlanBootstrapPageTables:
    push rbx
    push rsi
    push rdi
    push r12
    mov rsi, rcx
    mov rdi, rdx
    mov r12, r8
    test rsi, rsi
    jz .plan_fail
    cmp r12, 40
    jb .plan_fail
    mov rax, rdi
    xor edx, edx
    div r12
    test rdx, rdx
    jnz .plan_fail
    mov rbx, rax                ; descriptor count
    mov r10, 3                 ; private PML4 + two allocation-split edge PT pages
.plan_loop:
    test rbx, rbx
    jz .plan_done
    cmp dword [rsi], 7         ; EfiConventionalMemory
    jne .plan_next
    mov rax, [rsi + 0x20]      ; attributes
    bt rax, 63                 ; EFI_MEMORY_RUNTIME
    jc .plan_next
    mov r9, [rsi + 0x08]       ; physical start
    mov rax, [rsi + 0x18]      ; number of pages
    test rax, rax
    jz .plan_next
    mov rcx, rax
    shr rcx, 52
    test rcx, rcx
    jnz .plan_fail
    shl rax, 12
    mov r11, r9
    add r11, rax               ; exclusive physical end
    jc .plan_fail
    dec r11                    ; inclusive end

    ; One PDPT page per touched 512 GiB region (over-counting duplicates is safe).
    mov rax, r11
    shr rax, 39
    mov rcx, r9
    shr rcx, 39
    sub rax, rcx
    inc rax
    add r10, rax
    jc .plan_fail

    ; One PD page per touched 1 GiB region when using 2 MiB leaves.
    mov rax, r11
    shr rax, 30
    mov rcx, r9
    shr rcx, 30
    sub rax, rcx
    inc rax
    add r10, rax
    jc .plan_fail

    ; At most two PT pages are needed for unaligned 2 MiB edge fragments.
    test r9, 0x1FFFFF
    jz .plan_end_edge
    inc r10
.plan_end_edge:
    inc r11
    test r11, 0x1FFFFF
    jz .plan_next
    mov rax, r9
    shr rax, 21
    mov rcx, r11
    dec rcx
    shr rcx, 21
    cmp rax, rcx
    je .plan_next
    inc r10
.plan_next:
    add rsi, r12
    dec rbx
    jmp .plan_loop
.plan_done:
    ; Keep the pre-firmware allocation bounded to 256 MiB.
    test r10, r10
    jz .plan_fail
    cmp r10, 65536
    ja .plan_fail
    mov rax, r10
    jmp .plan_return
.plan_fail:
    xor eax, eax
.plan_return:
    pop r12
    pop rdi
    pop rsi
    pop rbx
    ret

InuCaptureUefiFramebuffer:
    ; RCX = EFI_SYSTEM_TABLE*. Preserve RBX and allocate shadow space plus
    ; one local qword used as the LocateProtocol output slot.
    push rbx
    sub rsp, 48
    mov qword [rsp + 32], 0

    test rcx, rcx
    jz .failed

    ; EFI_SYSTEM_TABLE.BootServices is at offset 0x60 on x64.
    mov rax, [rcx + 0x60]
    test rax, rax
    jz .failed

    ; EFI_BOOT_SERVICES.LocateProtocol is at offset 0x140.
    mov rax, [rax + 0x140]
    test rax, rax
    jz .failed

    lea rcx, [rel InuGraphicsOutputProtocolGuid]
    xor edx, edx
    lea r8, [rsp + 32]
    call rax
    test rax, rax
    jnz .failed

    mov rbx, [rsp + 32]
    test rbx, rbx
    jz .failed

    ; EFI_GRAPHICS_OUTPUT_PROTOCOL.Mode is at offset 0x18.
    mov rbx, [rbx + 0x18]
    test rbx, rbx
    jz .failed

    ; EFI_GRAPHICS_OUTPUT_PROTOCOL_MODE.Info is at 0x08.
    mov rdx, [rbx + 0x08]
    test rdx, rdx
    jz .failed

    ; FrameBufferBase and FrameBufferSize are at 0x18 and 0x20.
    mov rax, [rbx + 0x18]
    mov [rel InuBootContext + 0x08], rax
    mov rax, [rbx + 0x20]
    mov [rel InuBootContext + 0x10], rax

    ; EFI_GRAPHICS_OUTPUT_MODE_INFORMATION fields.
    mov eax, [rdx + 0x04]
    mov [rel InuBootContext + 0x18], eax
    mov eax, [rdx + 0x08]
    mov [rel InuBootContext + 0x1C], eax
    mov eax, [rdx + 0x20]
    mov [rel InuBootContext + 0x20], eax
    mov eax, [rdx + 0x0C]
    mov [rel InuBootContext + 0x24], eax
    mov eax, [rdx + 0x10]
    mov [rel InuBootContext + 0x28], eax
    mov eax, [rdx + 0x14]
    mov [rel InuBootContext + 0x2C], eax
    mov eax, [rdx + 0x18]
    mov [rel InuBootContext + 0x30], eax
    mov eax, [rdx + 0x1C]
    mov [rel InuBootContext + 0x34], eax

    mov al, 1
    add rsp, 48
    pop rbx
    ret

.failed:
    xor eax, eax
    add rsp, 48
    pop rbx
    ret
