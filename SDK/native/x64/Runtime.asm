bits 64
default rel

section .data align=8

; Win64 compiler security-cookie ABI used by NativeAOT/ILC stack-protector
; instrumentation. Inu supplies the symbols freestanding instead of linking
; the Windows CRT. The cookie is reseeded before any managed code executes.
global __security_cookie
global __security_cookie_complement
__security_cookie:
    dq 0x00002B992DDFA232
__security_cookie_complement:
    dq 0xFFFFD466D2205DCD

section .text

; -----------------------------------------------------------------------------
; NativeAOT x64 large-stack-frame probe helper
; -----------------------------------------------------------------------------
; NativeAOT/ILC emits RhpStackProbe in Debug (and any other configuration that
; needs a large managed stack frame).  The compiler ABI passes the lowest
; address of the frame being allocated in R11.  Probe each intervening 4 KiB
; page without changing RSP or R11 so a mapped/guarded kernel stack is touched
; incrementally rather than skipped over by one large subtraction.
;
; On entry:
;   R11 = lowest address in the stack frame being allocated
;   RSP = an address in the most recently probed stack page
; Clobbers: RAX only

global RhpStackProbe
RhpStackProbe:
    mov rax, rsp
    and rax, -0x1000
.stack_probe_loop:
    sub rax, 0x1000
    test dword [rax], eax
    cmp rax, r11
    jg .stack_probe_loop
    ret

global InuRuntimeInitialize

InuRuntimeInitialize:
    ; Seed the compiler security cookie from values available without firmware or
    ; a CRT: TSC, the live bootstrap stack address, and the loaded image address.
    rdtsc
    shl rdx, 32
    or rax, rdx
    xor rax, rsp
    lea rcx, [rel __security_cookie]
    xor rax, rcx
    rol rax, 17
    test rax, rax
    jnz .cookie_nonzero
    mov rax, 0x00002B992DDFA232
.cookie_nonzero:
    mov [rel __security_cookie], rax
    not rax
    mov [rel __security_cookie_complement], rax
    mov al, 1
    ret

; -----------------------------------------------------------------------------
; NativeAOT x64 GC write-barrier helper ABI
; -----------------------------------------------------------------------------
; These are compiler/JIT helpers, not ordinary callable C functions. On Windows
; x64 RhpAssignRef receives destination in RCX and the object reference in RDX.
; It is a leaf helper and, critically, does not clobber R8/R9. NativeAOT delegate
; construction may keep the target and invocation thunk live in R8/R9 across a
; reference-field store performed by InitializeOpenStaticThunk.
;
; Inu's collector is non-moving and non-generational. Managed-heap field stores
; remain leaf stores; direct image GC-static destinations use the precise 0.0.77
; root-registration slow path below without changing the compiler-known ABI.

global RhpAssignRef
global RhpCheckedAssignRef
global RhpByRefAssignRef
extern InuGcReferenceWrite

; 0.0.77: direct NativeAOT GC statics in Inu live in the low-address loaded
; PE image, while managed heap object fields live in the canonical high kernel
; heap. Keep the ordinary object-field barrier a true leaf helper. Only a low
; destination takes the precise static-root registration slow path. The slow path
; preserves volatile registers that NativeAOT is known to keep live across these
; compiler helpers (especially R8/R9 during delegate construction).
%macro INU_REGISTER_LOW_STATIC 1
    test %1, %1
    js %%done
    push rax
    push r8
    push r9
    push r10
    push r11
    sub rsp, 32
    mov rcx, %1
    call InuGcReferenceWrite
    add rsp, 32
    pop r11
    pop r10
    pop r9
    pop r8
    pop rax
%%done:
%endmacro

RhpAssignRef:
    mov [rcx], rdx
    INU_REGISTER_LOW_STATIC rcx
    ret

RhpCheckedAssignRef:
    mov [rcx], rdx
    INU_REGISTER_LOW_STATIC rcx
    ret

; RhpByRefAssignRef has a separate JIT ABI: RDI is the destination-slot address,
; RSI is the source-slot address. It copies one reference and advances both by a
; pointer. RCX is scratch. Do not convert this to the normal Win64 RCX/RDX ABI.
RhpByRefAssignRef:
    mov rcx, [rsi]
    mov [rdi], rcx
    INU_REGISTER_LOW_STATIC rdi
    add rdi, 8
    add rsi, 8
    ret

; -----------------------------------------------------------------------------
; 0.0.80 NativeAOT interface dispatch
; -----------------------------------------------------------------------------
; ILC interface call sites place the dispatch-cell address in R11 and preserve
; the normal Windows x64 method arguments in RCX/RDX/R8/R9.  The full Microsoft
; NativeAOT runtime normally routes RhpInitialDynamicInterfaceDispatch through
; its cached-interface-dispatch engine.  Inu resolves the compact static cell
; metadata directly, then tail-jumps to the resolved implementation.  No
; Windows NativeAOT runtime library is linked.

global RhpInitialDynamicInterfaceDispatch
global RhpInitialInterfaceDispatch
global RhpInterfaceDispatchSlow
extern InuResolveInterfaceDispatch

global InuGetInitialInterfaceDispatch
global InuGetRhpNewFast
global InuGetRhpNewFinalizable
global InuGetMissingDefaultConstructor
extern RhpNewFast
extern RhpNewFinalizable
extern InuRhpNewFastDiagnostic
extern InuRhpNewFinalizableDiagnostic

InuGetInitialInterfaceDispatch:
    lea rax, [rel RhpInitialDynamicInterfaceDispatch]
    ret

InuGetRhpNewFast:
    lea rax, [rel InuRhpNewFastDiagnostic]
    ret

InuGetRhpNewFinalizable:
    lea rax, [rel InuRhpNewFinalizableDiagnostic]
    ret

InuGetMissingDefaultConstructor:
    lea rax, [rel InuMissingDefaultConstructor]
    ret

; Stable NativeAOT DefaultConstructorOf<T> missing-constructor marker. The normal
; path compares or transports this pointer; invoking it indicates a broken generic
; constraint/runtime contract, so fail immediately instead of silently constructing.
InuMissingDefaultConstructor:
    ud2

RhpInitialInterfaceDispatch:
RhpInitialDynamicInterfaceDispatch:
RhpInterfaceDispatchSlow:
    ; Preserve all register arguments and the dispatch-cell register while the
    ; managed resolver runs. Five pushes also restore 16-byte call alignment.
    push rcx
    push rdx
    push r8
    push r9
    push r11
    sub rsp, 32
    mov rcx, [rsp + 64]       ; original this
    mov rdx, [rsp + 32]       ; original R11 dispatch cell
    call InuResolveInterfaceDispatch
    add rsp, 32
    pop r11
    pop r9
    pop r8
    pop rdx
    pop rcx
    test rax, rax
    jz .interface_resolution_failed
    jmp rax
.interface_resolution_failed:
    ; A zero target is never a valid NativeAOT method entry. Deliberately trap
    ; rather than returning through the call site with undefined state.
    ud2
