default rel
bits 64

global InuLoginEntry

%define SYS_GET   0x4E4F000000000000
%define SYS_SET   0x4E4F000100000000
%define SYS_EVENT 0x4E4F000200000000

section .text
InuLoginEntry:
    mov rdi, pid_env
    mov rax, SYS_GET
    syscall
    test rax, rax
    js .hang
    mov r12, rax
    lea rbx, [rel env_table]
    mov ecx, env_count
.set_pid:
    mov [rbx + 32], r12
    add rbx, 144
    loop .set_pid

    ; Display dimensions for centring.
    mov rdi, caps_env
    mov rax, SYS_GET
    syscall
    mov r13, rax

    mov rdi, create_env
    mov rax, SYS_SET
    syscall
    test rax, rax
    jle .hang
    mov r14, rax
    mov [move_env + 104], r14
    mov [present_env + 104], r14
    mov [show_env + 104], r14
    mov [hide_env + 104], r14
    mov [focus_env + 104], r14

    ; x=(width-440)/2, y=(height-420)/2
    mov eax, r13d
    sub eax, 440
    sar eax, 1
    mov eax, eax
    mov [move_env + 112], rax
    mov rax, r13
    shr rax, 32
    sub eax, 420
    sar eax, 1
    mov eax, eax
    mov [move_env + 120], rax

    mov rdi, move_env
    mov rax, SYS_SET
    syscall
    mov rdi, present_env
    mov rax, SYS_EVENT
    syscall
    mov rdi, show_env
    mov rax, SYS_SET
    syscall
    mov rdi, focus_env
    mov rax, SYS_SET
    syscall

.event_loop:
    mov rdi, event_env
    mov rax, SYS_GET
    syscall
    cmp rax, 1
    jne .idle
    cmp dword [event_buf], 3       ; KeyDown
    jne .event_loop
    mov al, byte [event_buf + 40]  ; Value1 = translated character
    cmp al, 13
    je .authenticate
    cmp al, 9
    je .toggle
    cmp al, 8
    je .backspace
    cmp al, 32
    jb .event_loop
    cmp al, 126
    ja .event_loop
    cmp byte [field], 0
    jne .append_password
.append_user:
    movzx ecx, byte [auth_payload]
    cmp ecx, 32
    jae .event_loop
    lea rdx, [rel auth_payload + 2]
    mov [rdx + rcx], al
    inc byte [auth_payload]
    jmp .event_loop
.append_password:
    movzx ecx, byte [auth_payload + 1]
    cmp ecx, 128
    jae .event_loop
    lea rdx, [rel auth_payload + 34]
    mov [rdx + rcx], al
    inc byte [auth_payload + 1]
    jmp .event_loop
.toggle:
    xor byte [field], 1
    jmp .event_loop
.backspace:
    cmp byte [field], 0
    jne .backspace_password
    cmp byte [auth_payload], 0
    je .event_loop
    dec byte [auth_payload]
    jmp .event_loop
.backspace_password:
    cmp byte [auth_payload + 1], 0
    je .event_loop
    dec byte [auth_payload + 1]
    jmp .event_loop
.authenticate:
    cmp byte [auth_payload], 0
    je .event_loop
    cmp byte [auth_payload + 1], 0
    je .event_loop
    mov rdi, auth_env
    mov rax, SYS_EVENT
    syscall
    test rax, rax
    jne .auth_failed
    mov rdi, hide_env
    mov rax, SYS_SET
    syscall
    jmp .hang
.auth_failed:
    mov byte [auth_payload + 1], 0
    jmp .event_loop
.idle:
    pause
    jmp .event_loop
.hang:
    pause
    jmp .hang

section .data
align 16
app_name: db 'Inu-Login'
app_name_len equ $-app_name
msg_pid: db 'process.id.current'
msg_pid_len equ $-msg_pid
msg_caps: db 'gui.capabilities'
msg_caps_len equ $-msg_caps
msg_create: db 'gui.surface.create'
msg_create_len equ $-msg_create
msg_move: db 'gui.surface.geometry'
msg_move_len equ $-msg_move
msg_present: db 'gui.surface.present'
msg_present_len equ $-msg_present
msg_visibility: db 'gui.surface.visibility'
msg_visibility_len equ $-msg_visibility
msg_focus: db 'gui.focus'
msg_focus_len equ $-msg_focus
msg_event: db 'gui.event.next'
msg_event_len equ $-msg_event
msg_auth: db 'session.login'
msg_auth_len equ $-msg_auth

env_table:
pid_env:      dq 1,144,app_name,app_name_len,0,msg_pid,msg_pid_len,0,0,0,0,1,0,0,0,0,0,0
caps_env:     dq 1,144,app_name,app_name_len,0,msg_caps,msg_caps_len,0,0,0,0,2,0,0,0,0,0,0
create_env:   dq 1,144,app_name,app_name_len,0,msg_create,msg_create_len,0,0,0,0,3,0,440,420,3,0,0
move_env:     dq 1,144,app_name,app_name_len,0,msg_move,msg_move_len,0,0,0,0,4,0,0,0,0,0,0
present_env:  dq 1,144,app_name,app_name_len,0,msg_present,msg_present_len,login_pixels,login_pixel_bytes,0,0,5,0,0,0,0,0,0
show_env:     dq 1,144,app_name,app_name_len,0,msg_visibility,msg_visibility_len,0,0,0,0,6,0,0,1,0,0,0
hide_env:     dq 1,144,app_name,app_name_len,0,msg_visibility,msg_visibility_len,0,0,0,0,7,0,0,0,0,0,0
focus_env:    dq 1,144,app_name,app_name_len,0,msg_focus,msg_focus_len,0,0,0,0,8,0,0,0,0,0,0
event_env:    dq 1,144,app_name,app_name_len,0,msg_event,msg_event_len,0,0,event_buf,64,9,0,0,0,0,0,0
auth_env:     dq 1,144,app_name,app_name_len,0,msg_auth,msg_auth_len,auth_payload,162,0,0,10,0,0,0,0,0,0
env_count equ 10

align 16
event_buf: times 64 db 0
field: db 0
align 16
auth_payload:
    db 0,0
    times 32 db 0
    times 128 db 0
align 16
login_pixels:
    incbin 'LOGIN.BGRA'
login_pixel_bytes equ $-login_pixels
