.section .text,"ax",@progbits
.global InuX64DisableInterrupts
.type InuX64DisableInterrupts,@function
InuX64DisableInterrupts:
    cli
    movb $1, %al
    ret

.global InuX64EnableInterrupts
.type InuX64EnableInterrupts,@function
InuX64EnableInterrupts:
    sti
    movb $1, %al
    ret

.global InuX64AreInterruptsEnabled
.type InuX64AreInterruptsEnabled,@function
InuX64AreInterruptsEnabled:
    pushfq
    popq %rax
    shrq $9, %rax
    andq $1, %rax
    ret

.global InuX64Halt
.type InuX64Halt,@function
InuX64Halt:
    cli
.LInuHaltForever:
    hlt
    jmp .LInuHaltForever
.size InuX64Halt, .-InuX64Halt

.global InuX64WaitForInterrupt
.type InuX64WaitForInterrupt,@function
InuX64WaitForInterrupt:
    hlt
    movb $1, %al
    ret
.size InuX64WaitForInterrupt, .-InuX64WaitForInterrupt

.global InuX64Pause
.type InuX64Pause,@function
InuX64Pause:
    pause
    movb $1, %al
    ret
.size InuX64Pause, .-InuX64Pause

.global InuX64AtomicCompareExchange64
.type InuX64AtomicCompareExchange64,@function
InuX64AtomicCompareExchange64:
    movq %rsi, %rax
    lock cmpxchgq %rdx, (%rdi)
    sete %r8b
    movq %rax, (%rcx)
    movb %r8b, %al
    ret
.size InuX64AtomicCompareExchange64, .-InuX64AtomicCompareExchange64

.global InuX64AtomicExchange64
.type InuX64AtomicExchange64,@function
InuX64AtomicExchange64:
    movq %rsi, %rax
    xchgq %rax, (%rdi)
    movq %rax, (%rdx)
    movb $1, %al
    ret
.size InuX64AtomicExchange64, .-InuX64AtomicExchange64

.global InuX64AtomicFetchAdd64
.type InuX64AtomicFetchAdd64,@function
InuX64AtomicFetchAdd64:
    movq %rsi, %rax
    lock xaddq %rax, (%rdi)
    movq %rax, (%rdx)
    movb $1, %al
    ret
.size InuX64AtomicFetchAdd64, .-InuX64AtomicFetchAdd64

.global InuX64AtomicLoad64
.type InuX64AtomicLoad64,@function
InuX64AtomicLoad64:
    movq (%rdi), %rax
    lfence
    movq %rax, (%rsi)
    movb $1, %al
    ret
.size InuX64AtomicLoad64, .-InuX64AtomicLoad64

.global InuX64AtomicStore64
.type InuX64AtomicStore64,@function
InuX64AtomicStore64:
    xchgq %rsi, (%rdi)
    movb $1, %al
    ret
.size InuX64AtomicStore64, .-InuX64AtomicStore64

.global InuX64MemoryBarrier
.type InuX64MemoryBarrier,@function
InuX64MemoryBarrier:
    mfence
    movb $1, %al
    ret
.size InuX64MemoryBarrier, .-InuX64MemoryBarrier


.global InuX64ReadTimestampCounter
.type InuX64ReadTimestampCounter,@function
InuX64ReadTimestampCounter:
    lfence
    rdtsc
    shlq $32, %rdx
    orq %rdx, %rax
    ret
.size InuX64ReadTimestampCounter, .-InuX64ReadTimestampCounter

.global InuX64SupportsTsc
.type InuX64SupportsTsc,@function
InuX64SupportsTsc:
    pushq %rbx
    movl $1, %eax
    cpuid
    btl $4, %edx
    setc %al
    movzbl %al, %eax
    popq %rbx
    ret
.size InuX64SupportsTsc, .-InuX64SupportsTsc

.global InuX64SupportsInvariantTsc
.type InuX64SupportsInvariantTsc,@function
InuX64SupportsInvariantTsc:
    pushq %rbx
    movl $0x80000000, %eax
    cpuid
    cmpl $0x80000007, %eax
    jb .LNoInvariantTsc
    movl $0x80000007, %eax
    cpuid
    btl $8, %edx
    setc %al
    movzbl %al, %eax
    popq %rbx
    ret
.LNoInvariantTsc:
    xorl %eax, %eax
    popq %rbx
    ret
.size InuX64SupportsInvariantTsc, .-InuX64SupportsInvariantTsc

.global InuX64WritePort8
.type InuX64WritePort8,@function
InuX64WritePort8:
    movw %di, %dx
    movb %sil, %al
    outb %al, %dx
    movb $1, %al
    ret

.global InuX64ReadPort8
.type InuX64ReadPort8,@function
InuX64ReadPort8:
    movw %di, %dx
    inb %dx, %al
    movb %al, (%rsi)
    movb $1, %al
    ret

.global InuX64WritePort16
.type InuX64WritePort16,@function
InuX64WritePort16:
    movw %di, %dx
    movw %si, %ax
    outw %ax, %dx
    movb $1, %al
    ret

.global InuX64ReadPort16
.type InuX64ReadPort16,@function
InuX64ReadPort16:
    movw %di, %dx
    inw %dx, %ax
    movw %ax, (%rsi)
    movb $1, %al
    ret

.global InuX64WritePort32
.type InuX64WritePort32,@function
InuX64WritePort32:
    movw %di, %dx
    movl %esi, %eax
    outl %eax, %dx
    movb $1, %al
    ret

.global InuX64ReadPort32
.type InuX64ReadPort32,@function
InuX64ReadPort32:
    movw %di, %dx
    inl %dx, %eax
    movl %eax, (%rsi)
    movb $1, %al
    ret
