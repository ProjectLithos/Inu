default rel
bits 64

global InuDesktopEntry

%define SYS_GET   0x4E4F000000000000
%define SYS_SET   0x4E4F000100000000
%define SYS_EVENT 0x4E4F000200000000

section .text
InuDesktopEntry:
    ; Resolve our PID; native Inu permits process.id.current with ProcessId=0.
    mov rdi, pid_env
    mov rax, SYS_GET
    syscall
    test rax, rax
    js .hang
    mov [caps_env + 32], rax
    mov [create_env + 32], rax
    mov [present_env + 32], rax
    mov [show_env + 32], rax

    ; Query display dimensions. Return value packs height:width.
    mov rdi, caps_env
    mov rax, SYS_GET
    syscall
    test rax, rax
    js .hang
    mov rbx, rax
    mov eax, ebx
    mov [create_env + 104], rax
    shr rbx, 32
    mov [create_env + 112], rbx

    ; Create a full-display desktop surface.
    mov rdi, create_env
    mov rax, SYS_SET
    syscall
    test rax, rax
    jle .hang
    mov [present_env + 104], rax
    mov [show_env + 104], rax

    ; Present the supplied wallpaper. Value1/Value2 describe source dimensions;
    ; the compositor scales it to the desktop surface using cover semantics.
    mov rdi, present_env
    mov rax, SYS_EVENT
    syscall
    test rax, rax
    js .hang

    mov rdi, show_env
    mov rax, SYS_SET
    syscall
.hang:
    pause
    jmp .hang

section .data
align 16
app_name: db 'Inu-Desktop'
app_name_len equ $-app_name
msg_pid: db 'process.id.current'
msg_pid_len equ $-msg_pid
msg_caps: db 'gui.capabilities'
msg_caps_len equ $-msg_caps
msg_create: db 'gui.surface.create'
msg_create_len equ $-msg_create
msg_present: db 'gui.surface.present'
msg_present_len equ $-msg_present
msg_show: db 'gui.surface.visibility'
msg_show_len equ $-msg_show

align 16
pid_env:
    dq 1,144,app_name,app_name_len,0,msg_pid,msg_pid_len,0,0,0,0,1,0,0,0,0,0,0
caps_env:
    dq 1,144,app_name,app_name_len,0,msg_caps,msg_caps_len,0,0,0,0,2,0,0,0,0,0,0
create_env:
    dq 1,144,app_name,app_name_len,0,msg_create,msg_create_len,0,0,0,0,3,0,0,0,4,0,0
present_env:
    dq 1,144,app_name,app_name_len,0,msg_present,msg_present_len,wallpaper,wallpaper_bytes,0,0,4,0,0,678,452,0,0
show_env:
    dq 1,144,app_name,app_name_len,0,msg_show,msg_show_len,0,0,0,0,5,0,0,1,0,0,0

align 16
wallpaper:
    incbin 'INU-HEX.BGRA'
wallpaper_bytes equ $-wallpaper
