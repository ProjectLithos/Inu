bits 64
default rel
extern InuManagedInterruptDispatch
extern InuX64SyscallTraceByte
extern InuX64NmiDiagnosticState

section .data align=8
InuX64InterruptDispatcher: dq 0
global InuX64InterruptStackSwitch
InuX64InterruptStackSwitch: times 256 db 0

section .text
global InuX64LoadInterruptDescriptorTable
global InuX64InstallManagedInterruptDispatcher
InuX64InstallManagedInterruptDispatcher:
    lea rax, [rel InuManagedInterruptDispatch]
    mov [rel InuX64InterruptDispatcher], rax
    mov al, 1
    ret

global InuX64SetInterruptDispatcher
global InuX64GetInterruptStub
global InuX64SetInterruptStackSwitch
global InuX64StopProcessor
global InuX64InterruptCommon
global InuX64InterruptStubTable

InuX64LoadInterruptDescriptorTable:
    sub rsp, 16
    mov [rsp], dx
    mov [rsp + 2], rcx
    lidt [rsp]
    add rsp, 16
    mov al, 1
    ret

InuX64SetInterruptDispatcher:
    mov [rel InuX64InterruptDispatcher], rcx
    mov al, 1
    ret

InuX64SetInterruptStackSwitch:
    movzx eax, cl
    lea r8, [rel InuX64InterruptStackSwitch]
    mov [r8 + rax], dl
    mov al, 1
    ret

InuX64GetInterruptStub:
    movzx eax, cl
    lea rdx, [rel InuX64InterruptStubTable]
    mov rax, [rdx + rax * 8]
    ret

InuX64StopProcessor:
    cli
.halt:
    hlt
    jmp .halt

global InuX64InterruptStub0
InuX64InterruptStub0:
    push qword 0
    push qword 0
global InuX64InterruptDebugFrame0
InuX64InterruptDebugFrame0:
    jmp InuX64InterruptCommon

global InuX64InterruptStub1
InuX64InterruptStub1:
    push qword 0
    push qword 1
global InuX64InterruptDebugFrame1
InuX64InterruptDebugFrame1:
    jmp InuX64InterruptCommon

global InuX64InterruptStub2
InuX64InterruptStub2:
    push qword 0
    push qword 2
global InuX64InterruptDebugFrame2
InuX64InterruptDebugFrame2:
    jmp InuX64InterruptCommon

global InuX64InterruptStub3
InuX64InterruptStub3:
    push qword 0
    push qword 3
global InuX64InterruptDebugFrame3
InuX64InterruptDebugFrame3:
    jmp InuX64InterruptCommon

global InuX64InterruptStub4
InuX64InterruptStub4:
    push qword 0
    push qword 4
global InuX64InterruptDebugFrame4
InuX64InterruptDebugFrame4:
    jmp InuX64InterruptCommon

global InuX64InterruptStub5
InuX64InterruptStub5:
    push qword 0
    push qword 5
global InuX64InterruptDebugFrame5
InuX64InterruptDebugFrame5:
    jmp InuX64InterruptCommon

global InuX64InterruptStub6
InuX64InterruptStub6:
    push qword 0
    push qword 6
global InuX64InterruptDebugFrame6
InuX64InterruptDebugFrame6:
    jmp InuX64InterruptCommon

global InuX64InterruptStub7
InuX64InterruptStub7:
    push qword 0
    push qword 7
global InuX64InterruptDebugFrame7
InuX64InterruptDebugFrame7:
    jmp InuX64InterruptCommon

global InuX64InterruptStub8
InuX64InterruptStub8:
    push qword 8
global InuX64InterruptDebugFrame8
InuX64InterruptDebugFrame8:
    jmp InuX64InterruptCommon

global InuX64InterruptStub9
InuX64InterruptStub9:
    push qword 0
    push qword 9
global InuX64InterruptDebugFrame9
InuX64InterruptDebugFrame9:
    jmp InuX64InterruptCommon

global InuX64InterruptStub10
InuX64InterruptStub10:
    push qword 10
global InuX64InterruptDebugFrame10
InuX64InterruptDebugFrame10:
    jmp InuX64InterruptCommon

global InuX64InterruptStub11
InuX64InterruptStub11:
    push qword 11
global InuX64InterruptDebugFrame11
InuX64InterruptDebugFrame11:
    jmp InuX64InterruptCommon

global InuX64InterruptStub12
InuX64InterruptStub12:
    push qword 12
global InuX64InterruptDebugFrame12
InuX64InterruptDebugFrame12:
    jmp InuX64InterruptCommon

global InuX64InterruptStub13
InuX64InterruptStub13:
    push qword 13
global InuX64InterruptDebugFrame13
InuX64InterruptDebugFrame13:
    jmp InuX64InterruptCommon

global InuX64InterruptStub14
InuX64InterruptStub14:
    push qword 14
global InuX64InterruptDebugFrame14
InuX64InterruptDebugFrame14:
    jmp InuX64InterruptCommon

global InuX64InterruptStub15
InuX64InterruptStub15:
    push qword 0
    push qword 15
global InuX64InterruptDebugFrame15
InuX64InterruptDebugFrame15:
    jmp InuX64InterruptCommon

global InuX64InterruptStub16
InuX64InterruptStub16:
    push qword 0
    push qword 16
global InuX64InterruptDebugFrame16
InuX64InterruptDebugFrame16:
    jmp InuX64InterruptCommon

global InuX64InterruptStub17
InuX64InterruptStub17:
    push qword 17
global InuX64InterruptDebugFrame17
InuX64InterruptDebugFrame17:
    jmp InuX64InterruptCommon

global InuX64InterruptStub18
InuX64InterruptStub18:
    push qword 0
    push qword 18
global InuX64InterruptDebugFrame18
InuX64InterruptDebugFrame18:
    jmp InuX64InterruptCommon

global InuX64InterruptStub19
InuX64InterruptStub19:
    push qword 0
    push qword 19
global InuX64InterruptDebugFrame19
InuX64InterruptDebugFrame19:
    jmp InuX64InterruptCommon

global InuX64InterruptStub20
InuX64InterruptStub20:
    push qword 0
    push qword 20
global InuX64InterruptDebugFrame20
InuX64InterruptDebugFrame20:
    jmp InuX64InterruptCommon

global InuX64InterruptStub21
InuX64InterruptStub21:
    push qword 21
global InuX64InterruptDebugFrame21
InuX64InterruptDebugFrame21:
    jmp InuX64InterruptCommon

global InuX64InterruptStub22
InuX64InterruptStub22:
    push qword 0
    push qword 22
global InuX64InterruptDebugFrame22
InuX64InterruptDebugFrame22:
    jmp InuX64InterruptCommon

global InuX64InterruptStub23
InuX64InterruptStub23:
    push qword 0
    push qword 23
global InuX64InterruptDebugFrame23
InuX64InterruptDebugFrame23:
    jmp InuX64InterruptCommon

global InuX64InterruptStub24
InuX64InterruptStub24:
    push qword 0
    push qword 24
global InuX64InterruptDebugFrame24
InuX64InterruptDebugFrame24:
    jmp InuX64InterruptCommon

global InuX64InterruptStub25
InuX64InterruptStub25:
    push qword 0
    push qword 25
global InuX64InterruptDebugFrame25
InuX64InterruptDebugFrame25:
    jmp InuX64InterruptCommon

global InuX64InterruptStub26
InuX64InterruptStub26:
    push qword 0
    push qword 26
global InuX64InterruptDebugFrame26
InuX64InterruptDebugFrame26:
    jmp InuX64InterruptCommon

global InuX64InterruptStub27
InuX64InterruptStub27:
    push qword 0
    push qword 27
global InuX64InterruptDebugFrame27
InuX64InterruptDebugFrame27:
    jmp InuX64InterruptCommon

global InuX64InterruptStub28
InuX64InterruptStub28:
    push qword 0
    push qword 28
global InuX64InterruptDebugFrame28
InuX64InterruptDebugFrame28:
    jmp InuX64InterruptCommon

global InuX64InterruptStub29
InuX64InterruptStub29:
    push qword 29
global InuX64InterruptDebugFrame29
InuX64InterruptDebugFrame29:
    jmp InuX64InterruptCommon

global InuX64InterruptStub30
InuX64InterruptStub30:
    push qword 30
global InuX64InterruptDebugFrame30
InuX64InterruptDebugFrame30:
    jmp InuX64InterruptCommon

global InuX64InterruptStub31
InuX64InterruptStub31:
    push qword 0
    push qword 31
global InuX64InterruptDebugFrame31
InuX64InterruptDebugFrame31:
    jmp InuX64InterruptCommon

global InuX64InterruptStub32
InuX64InterruptStub32:
    push qword 0
    push qword 32
global InuX64InterruptDebugFrame32
InuX64InterruptDebugFrame32:
    jmp InuX64InterruptCommon

global InuX64InterruptStub33
InuX64InterruptStub33:
    push qword 0
    push qword 33
global InuX64InterruptDebugFrame33
InuX64InterruptDebugFrame33:
    jmp InuX64InterruptCommon

global InuX64InterruptStub34
InuX64InterruptStub34:
    push qword 0
    push qword 34
global InuX64InterruptDebugFrame34
InuX64InterruptDebugFrame34:
    jmp InuX64InterruptCommon

global InuX64InterruptStub35
InuX64InterruptStub35:
    push qword 0
    push qword 35
global InuX64InterruptDebugFrame35
InuX64InterruptDebugFrame35:
    jmp InuX64InterruptCommon

global InuX64InterruptStub36
InuX64InterruptStub36:
    push qword 0
    push qword 36
global InuX64InterruptDebugFrame36
InuX64InterruptDebugFrame36:
    jmp InuX64InterruptCommon

global InuX64InterruptStub37
InuX64InterruptStub37:
    push qword 0
    push qword 37
global InuX64InterruptDebugFrame37
InuX64InterruptDebugFrame37:
    jmp InuX64InterruptCommon

global InuX64InterruptStub38
InuX64InterruptStub38:
    push qword 0
    push qword 38
global InuX64InterruptDebugFrame38
InuX64InterruptDebugFrame38:
    jmp InuX64InterruptCommon

global InuX64InterruptStub39
InuX64InterruptStub39:
    push qword 0
    push qword 39
global InuX64InterruptDebugFrame39
InuX64InterruptDebugFrame39:
    jmp InuX64InterruptCommon

global InuX64InterruptStub40
InuX64InterruptStub40:
    push qword 0
    push qword 40
global InuX64InterruptDebugFrame40
InuX64InterruptDebugFrame40:
    jmp InuX64InterruptCommon

global InuX64InterruptStub41
InuX64InterruptStub41:
    push qword 0
    push qword 41
global InuX64InterruptDebugFrame41
InuX64InterruptDebugFrame41:
    jmp InuX64InterruptCommon

global InuX64InterruptStub42
InuX64InterruptStub42:
    push qword 0
    push qword 42
global InuX64InterruptDebugFrame42
InuX64InterruptDebugFrame42:
    jmp InuX64InterruptCommon

global InuX64InterruptStub43
InuX64InterruptStub43:
    push qword 0
    push qword 43
global InuX64InterruptDebugFrame43
InuX64InterruptDebugFrame43:
    jmp InuX64InterruptCommon

global InuX64InterruptStub44
InuX64InterruptStub44:
    push qword 0
    push qword 44
global InuX64InterruptDebugFrame44
InuX64InterruptDebugFrame44:
    jmp InuX64InterruptCommon

global InuX64InterruptStub45
InuX64InterruptStub45:
    push qword 0
    push qword 45
global InuX64InterruptDebugFrame45
InuX64InterruptDebugFrame45:
    jmp InuX64InterruptCommon

global InuX64InterruptStub46
InuX64InterruptStub46:
    push qword 0
    push qword 46
global InuX64InterruptDebugFrame46
InuX64InterruptDebugFrame46:
    jmp InuX64InterruptCommon

global InuX64InterruptStub47
InuX64InterruptStub47:
    push qword 0
    push qword 47
global InuX64InterruptDebugFrame47
InuX64InterruptDebugFrame47:
    jmp InuX64InterruptCommon

global InuX64InterruptStub48
InuX64InterruptStub48:
    push qword 0
    push qword 48
global InuX64InterruptDebugFrame48
InuX64InterruptDebugFrame48:
    jmp InuX64InterruptCommon

global InuX64InterruptStub49
InuX64InterruptStub49:
    push qword 0
    push qword 49
global InuX64InterruptDebugFrame49
InuX64InterruptDebugFrame49:
    jmp InuX64InterruptCommon

global InuX64InterruptStub50
InuX64InterruptStub50:
    push qword 0
    push qword 50
global InuX64InterruptDebugFrame50
InuX64InterruptDebugFrame50:
    jmp InuX64InterruptCommon

global InuX64InterruptStub51
InuX64InterruptStub51:
    push qword 0
    push qword 51
global InuX64InterruptDebugFrame51
InuX64InterruptDebugFrame51:
    jmp InuX64InterruptCommon

global InuX64InterruptStub52
InuX64InterruptStub52:
    push qword 0
    push qword 52
global InuX64InterruptDebugFrame52
InuX64InterruptDebugFrame52:
    jmp InuX64InterruptCommon

global InuX64InterruptStub53
InuX64InterruptStub53:
    push qword 0
    push qword 53
global InuX64InterruptDebugFrame53
InuX64InterruptDebugFrame53:
    jmp InuX64InterruptCommon

global InuX64InterruptStub54
InuX64InterruptStub54:
    push qword 0
    push qword 54
global InuX64InterruptDebugFrame54
InuX64InterruptDebugFrame54:
    jmp InuX64InterruptCommon

global InuX64InterruptStub55
InuX64InterruptStub55:
    push qword 0
    push qword 55
global InuX64InterruptDebugFrame55
InuX64InterruptDebugFrame55:
    jmp InuX64InterruptCommon

global InuX64InterruptStub56
InuX64InterruptStub56:
    push qword 0
    push qword 56
global InuX64InterruptDebugFrame56
InuX64InterruptDebugFrame56:
    jmp InuX64InterruptCommon

global InuX64InterruptStub57
InuX64InterruptStub57:
    push qword 0
    push qword 57
global InuX64InterruptDebugFrame57
InuX64InterruptDebugFrame57:
    jmp InuX64InterruptCommon

global InuX64InterruptStub58
InuX64InterruptStub58:
    push qword 0
    push qword 58
global InuX64InterruptDebugFrame58
InuX64InterruptDebugFrame58:
    jmp InuX64InterruptCommon

global InuX64InterruptStub59
InuX64InterruptStub59:
    push qword 0
    push qword 59
global InuX64InterruptDebugFrame59
InuX64InterruptDebugFrame59:
    jmp InuX64InterruptCommon

global InuX64InterruptStub60
InuX64InterruptStub60:
    push qword 0
    push qword 60
global InuX64InterruptDebugFrame60
InuX64InterruptDebugFrame60:
    jmp InuX64InterruptCommon

global InuX64InterruptStub61
InuX64InterruptStub61:
    push qword 0
    push qword 61
global InuX64InterruptDebugFrame61
InuX64InterruptDebugFrame61:
    jmp InuX64InterruptCommon

global InuX64InterruptStub62
InuX64InterruptStub62:
    push qword 0
    push qword 62
global InuX64InterruptDebugFrame62
InuX64InterruptDebugFrame62:
    jmp InuX64InterruptCommon

global InuX64InterruptStub63
InuX64InterruptStub63:
    push qword 0
    push qword 63
global InuX64InterruptDebugFrame63
InuX64InterruptDebugFrame63:
    jmp InuX64InterruptCommon

global InuX64InterruptStub64
InuX64InterruptStub64:
    push qword 0
    push qword 64
global InuX64InterruptDebugFrame64
InuX64InterruptDebugFrame64:
    jmp InuX64InterruptCommon

global InuX64InterruptStub65
InuX64InterruptStub65:
    push qword 0
    push qword 65
global InuX64InterruptDebugFrame65
InuX64InterruptDebugFrame65:
    jmp InuX64InterruptCommon

global InuX64InterruptStub66
InuX64InterruptStub66:
    push qword 0
    push qword 66
global InuX64InterruptDebugFrame66
InuX64InterruptDebugFrame66:
    jmp InuX64InterruptCommon

global InuX64InterruptStub67
InuX64InterruptStub67:
    push qword 0
    push qword 67
global InuX64InterruptDebugFrame67
InuX64InterruptDebugFrame67:
    jmp InuX64InterruptCommon

global InuX64InterruptStub68
InuX64InterruptStub68:
    push qword 0
    push qword 68
global InuX64InterruptDebugFrame68
InuX64InterruptDebugFrame68:
    jmp InuX64InterruptCommon

global InuX64InterruptStub69
InuX64InterruptStub69:
    push qword 0
    push qword 69
global InuX64InterruptDebugFrame69
InuX64InterruptDebugFrame69:
    jmp InuX64InterruptCommon

global InuX64InterruptStub70
InuX64InterruptStub70:
    push qword 0
    push qword 70
global InuX64InterruptDebugFrame70
InuX64InterruptDebugFrame70:
    jmp InuX64InterruptCommon

global InuX64InterruptStub71
InuX64InterruptStub71:
    push qword 0
    push qword 71
global InuX64InterruptDebugFrame71
InuX64InterruptDebugFrame71:
    jmp InuX64InterruptCommon

global InuX64InterruptStub72
InuX64InterruptStub72:
    push qword 0
    push qword 72
global InuX64InterruptDebugFrame72
InuX64InterruptDebugFrame72:
    jmp InuX64InterruptCommon

global InuX64InterruptStub73
InuX64InterruptStub73:
    push qword 0
    push qword 73
global InuX64InterruptDebugFrame73
InuX64InterruptDebugFrame73:
    jmp InuX64InterruptCommon

global InuX64InterruptStub74
InuX64InterruptStub74:
    push qword 0
    push qword 74
global InuX64InterruptDebugFrame74
InuX64InterruptDebugFrame74:
    jmp InuX64InterruptCommon

global InuX64InterruptStub75
InuX64InterruptStub75:
    push qword 0
    push qword 75
global InuX64InterruptDebugFrame75
InuX64InterruptDebugFrame75:
    jmp InuX64InterruptCommon

global InuX64InterruptStub76
InuX64InterruptStub76:
    push qword 0
    push qword 76
global InuX64InterruptDebugFrame76
InuX64InterruptDebugFrame76:
    jmp InuX64InterruptCommon

global InuX64InterruptStub77
InuX64InterruptStub77:
    push qword 0
    push qword 77
global InuX64InterruptDebugFrame77
InuX64InterruptDebugFrame77:
    jmp InuX64InterruptCommon

global InuX64InterruptStub78
InuX64InterruptStub78:
    push qword 0
    push qword 78
global InuX64InterruptDebugFrame78
InuX64InterruptDebugFrame78:
    jmp InuX64InterruptCommon

global InuX64InterruptStub79
InuX64InterruptStub79:
    push qword 0
    push qword 79
global InuX64InterruptDebugFrame79
InuX64InterruptDebugFrame79:
    jmp InuX64InterruptCommon

global InuX64InterruptStub80
InuX64InterruptStub80:
    push qword 0
    push qword 80
global InuX64InterruptDebugFrame80
InuX64InterruptDebugFrame80:
    jmp InuX64InterruptCommon

global InuX64InterruptStub81
InuX64InterruptStub81:
    push qword 0
    push qword 81
global InuX64InterruptDebugFrame81
InuX64InterruptDebugFrame81:
    jmp InuX64InterruptCommon

global InuX64InterruptStub82
InuX64InterruptStub82:
    push qword 0
    push qword 82
global InuX64InterruptDebugFrame82
InuX64InterruptDebugFrame82:
    jmp InuX64InterruptCommon

global InuX64InterruptStub83
InuX64InterruptStub83:
    push qword 0
    push qword 83
global InuX64InterruptDebugFrame83
InuX64InterruptDebugFrame83:
    jmp InuX64InterruptCommon

global InuX64InterruptStub84
InuX64InterruptStub84:
    push qword 0
    push qword 84
global InuX64InterruptDebugFrame84
InuX64InterruptDebugFrame84:
    jmp InuX64InterruptCommon

global InuX64InterruptStub85
InuX64InterruptStub85:
    push qword 0
    push qword 85
global InuX64InterruptDebugFrame85
InuX64InterruptDebugFrame85:
    jmp InuX64InterruptCommon

global InuX64InterruptStub86
InuX64InterruptStub86:
    push qword 0
    push qword 86
global InuX64InterruptDebugFrame86
InuX64InterruptDebugFrame86:
    jmp InuX64InterruptCommon

global InuX64InterruptStub87
InuX64InterruptStub87:
    push qword 0
    push qword 87
global InuX64InterruptDebugFrame87
InuX64InterruptDebugFrame87:
    jmp InuX64InterruptCommon

global InuX64InterruptStub88
InuX64InterruptStub88:
    push qword 0
    push qword 88
global InuX64InterruptDebugFrame88
InuX64InterruptDebugFrame88:
    jmp InuX64InterruptCommon

global InuX64InterruptStub89
InuX64InterruptStub89:
    push qword 0
    push qword 89
global InuX64InterruptDebugFrame89
InuX64InterruptDebugFrame89:
    jmp InuX64InterruptCommon

global InuX64InterruptStub90
InuX64InterruptStub90:
    push qword 0
    push qword 90
global InuX64InterruptDebugFrame90
InuX64InterruptDebugFrame90:
    jmp InuX64InterruptCommon

global InuX64InterruptStub91
InuX64InterruptStub91:
    push qword 0
    push qword 91
global InuX64InterruptDebugFrame91
InuX64InterruptDebugFrame91:
    jmp InuX64InterruptCommon

global InuX64InterruptStub92
InuX64InterruptStub92:
    push qword 0
    push qword 92
global InuX64InterruptDebugFrame92
InuX64InterruptDebugFrame92:
    jmp InuX64InterruptCommon

global InuX64InterruptStub93
InuX64InterruptStub93:
    push qword 0
    push qword 93
global InuX64InterruptDebugFrame93
InuX64InterruptDebugFrame93:
    jmp InuX64InterruptCommon

global InuX64InterruptStub94
InuX64InterruptStub94:
    push qword 0
    push qword 94
global InuX64InterruptDebugFrame94
InuX64InterruptDebugFrame94:
    jmp InuX64InterruptCommon

global InuX64InterruptStub95
InuX64InterruptStub95:
    push qword 0
    push qword 95
global InuX64InterruptDebugFrame95
InuX64InterruptDebugFrame95:
    jmp InuX64InterruptCommon

global InuX64InterruptStub96
InuX64InterruptStub96:
    push qword 0
    push qword 96
global InuX64InterruptDebugFrame96
InuX64InterruptDebugFrame96:
    jmp InuX64InterruptCommon

global InuX64InterruptStub97
InuX64InterruptStub97:
    push qword 0
    push qword 97
global InuX64InterruptDebugFrame97
InuX64InterruptDebugFrame97:
    jmp InuX64InterruptCommon

global InuX64InterruptStub98
InuX64InterruptStub98:
    push qword 0
    push qword 98
global InuX64InterruptDebugFrame98
InuX64InterruptDebugFrame98:
    jmp InuX64InterruptCommon

global InuX64InterruptStub99
InuX64InterruptStub99:
    push qword 0
    push qword 99
global InuX64InterruptDebugFrame99
InuX64InterruptDebugFrame99:
    jmp InuX64InterruptCommon

global InuX64InterruptStub100
InuX64InterruptStub100:
    push qword 0
    push qword 100
global InuX64InterruptDebugFrame100
InuX64InterruptDebugFrame100:
    jmp InuX64InterruptCommon

global InuX64InterruptStub101
InuX64InterruptStub101:
    push qword 0
    push qword 101
global InuX64InterruptDebugFrame101
InuX64InterruptDebugFrame101:
    jmp InuX64InterruptCommon

global InuX64InterruptStub102
InuX64InterruptStub102:
    push qword 0
    push qword 102
global InuX64InterruptDebugFrame102
InuX64InterruptDebugFrame102:
    jmp InuX64InterruptCommon

global InuX64InterruptStub103
InuX64InterruptStub103:
    push qword 0
    push qword 103
global InuX64InterruptDebugFrame103
InuX64InterruptDebugFrame103:
    jmp InuX64InterruptCommon

global InuX64InterruptStub104
InuX64InterruptStub104:
    push qword 0
    push qword 104
global InuX64InterruptDebugFrame104
InuX64InterruptDebugFrame104:
    jmp InuX64InterruptCommon

global InuX64InterruptStub105
InuX64InterruptStub105:
    push qword 0
    push qword 105
global InuX64InterruptDebugFrame105
InuX64InterruptDebugFrame105:
    jmp InuX64InterruptCommon

global InuX64InterruptStub106
InuX64InterruptStub106:
    push qword 0
    push qword 106
global InuX64InterruptDebugFrame106
InuX64InterruptDebugFrame106:
    jmp InuX64InterruptCommon

global InuX64InterruptStub107
InuX64InterruptStub107:
    push qword 0
    push qword 107
global InuX64InterruptDebugFrame107
InuX64InterruptDebugFrame107:
    jmp InuX64InterruptCommon

global InuX64InterruptStub108
InuX64InterruptStub108:
    push qword 0
    push qword 108
global InuX64InterruptDebugFrame108
InuX64InterruptDebugFrame108:
    jmp InuX64InterruptCommon

global InuX64InterruptStub109
InuX64InterruptStub109:
    push qword 0
    push qword 109
global InuX64InterruptDebugFrame109
InuX64InterruptDebugFrame109:
    jmp InuX64InterruptCommon

global InuX64InterruptStub110
InuX64InterruptStub110:
    push qword 0
    push qword 110
global InuX64InterruptDebugFrame110
InuX64InterruptDebugFrame110:
    jmp InuX64InterruptCommon

global InuX64InterruptStub111
InuX64InterruptStub111:
    push qword 0
    push qword 111
global InuX64InterruptDebugFrame111
InuX64InterruptDebugFrame111:
    jmp InuX64InterruptCommon

global InuX64InterruptStub112
InuX64InterruptStub112:
    push qword 0
    push qword 112
global InuX64InterruptDebugFrame112
InuX64InterruptDebugFrame112:
    jmp InuX64InterruptCommon

global InuX64InterruptStub113
InuX64InterruptStub113:
    push qword 0
    push qword 113
global InuX64InterruptDebugFrame113
InuX64InterruptDebugFrame113:
    jmp InuX64InterruptCommon

global InuX64InterruptStub114
InuX64InterruptStub114:
    push qword 0
    push qword 114
global InuX64InterruptDebugFrame114
InuX64InterruptDebugFrame114:
    jmp InuX64InterruptCommon

global InuX64InterruptStub115
InuX64InterruptStub115:
    push qword 0
    push qword 115
global InuX64InterruptDebugFrame115
InuX64InterruptDebugFrame115:
    jmp InuX64InterruptCommon

global InuX64InterruptStub116
InuX64InterruptStub116:
    push qword 0
    push qword 116
global InuX64InterruptDebugFrame116
InuX64InterruptDebugFrame116:
    jmp InuX64InterruptCommon

global InuX64InterruptStub117
InuX64InterruptStub117:
    push qword 0
    push qword 117
global InuX64InterruptDebugFrame117
InuX64InterruptDebugFrame117:
    jmp InuX64InterruptCommon

global InuX64InterruptStub118
InuX64InterruptStub118:
    push qword 0
    push qword 118
global InuX64InterruptDebugFrame118
InuX64InterruptDebugFrame118:
    jmp InuX64InterruptCommon

global InuX64InterruptStub119
InuX64InterruptStub119:
    push qword 0
    push qword 119
global InuX64InterruptDebugFrame119
InuX64InterruptDebugFrame119:
    jmp InuX64InterruptCommon

global InuX64InterruptStub120
InuX64InterruptStub120:
    push qword 0
    push qword 120
global InuX64InterruptDebugFrame120
InuX64InterruptDebugFrame120:
    jmp InuX64InterruptCommon

global InuX64InterruptStub121
InuX64InterruptStub121:
    push qword 0
    push qword 121
global InuX64InterruptDebugFrame121
InuX64InterruptDebugFrame121:
    jmp InuX64InterruptCommon

global InuX64InterruptStub122
InuX64InterruptStub122:
    push qword 0
    push qword 122
global InuX64InterruptDebugFrame122
InuX64InterruptDebugFrame122:
    jmp InuX64InterruptCommon

global InuX64InterruptStub123
InuX64InterruptStub123:
    push qword 0
    push qword 123
global InuX64InterruptDebugFrame123
InuX64InterruptDebugFrame123:
    jmp InuX64InterruptCommon

global InuX64InterruptStub124
InuX64InterruptStub124:
    push qword 0
    push qword 124
global InuX64InterruptDebugFrame124
InuX64InterruptDebugFrame124:
    jmp InuX64InterruptCommon

global InuX64InterruptStub125
InuX64InterruptStub125:
    push qword 0
    push qword 125
global InuX64InterruptDebugFrame125
InuX64InterruptDebugFrame125:
    jmp InuX64InterruptCommon

global InuX64InterruptStub126
InuX64InterruptStub126:
    push qword 0
    push qword 126
global InuX64InterruptDebugFrame126
InuX64InterruptDebugFrame126:
    jmp InuX64InterruptCommon

global InuX64InterruptStub127
InuX64InterruptStub127:
    push qword 0
    push qword 127
global InuX64InterruptDebugFrame127
InuX64InterruptDebugFrame127:
    jmp InuX64InterruptCommon

global InuX64InterruptStub128
InuX64InterruptStub128:
    push qword 0
    push qword 128
global InuX64InterruptDebugFrame128
InuX64InterruptDebugFrame128:
    jmp InuX64InterruptCommon

global InuX64InterruptStub129
InuX64InterruptStub129:
    push qword 0
    push qword 129
global InuX64InterruptDebugFrame129
InuX64InterruptDebugFrame129:
    jmp InuX64InterruptCommon

global InuX64InterruptStub130
InuX64InterruptStub130:
    push qword 0
    push qword 130
global InuX64InterruptDebugFrame130
InuX64InterruptDebugFrame130:
    jmp InuX64InterruptCommon

global InuX64InterruptStub131
InuX64InterruptStub131:
    push qword 0
    push qword 131
global InuX64InterruptDebugFrame131
InuX64InterruptDebugFrame131:
    jmp InuX64InterruptCommon

global InuX64InterruptStub132
InuX64InterruptStub132:
    push qword 0
    push qword 132
global InuX64InterruptDebugFrame132
InuX64InterruptDebugFrame132:
    jmp InuX64InterruptCommon

global InuX64InterruptStub133
InuX64InterruptStub133:
    push qword 0
    push qword 133
global InuX64InterruptDebugFrame133
InuX64InterruptDebugFrame133:
    jmp InuX64InterruptCommon

global InuX64InterruptStub134
InuX64InterruptStub134:
    push qword 0
    push qword 134
global InuX64InterruptDebugFrame134
InuX64InterruptDebugFrame134:
    jmp InuX64InterruptCommon

global InuX64InterruptStub135
InuX64InterruptStub135:
    push qword 0
    push qword 135
global InuX64InterruptDebugFrame135
InuX64InterruptDebugFrame135:
    jmp InuX64InterruptCommon

global InuX64InterruptStub136
InuX64InterruptStub136:
    push qword 0
    push qword 136
global InuX64InterruptDebugFrame136
InuX64InterruptDebugFrame136:
    jmp InuX64InterruptCommon

global InuX64InterruptStub137
InuX64InterruptStub137:
    push qword 0
    push qword 137
global InuX64InterruptDebugFrame137
InuX64InterruptDebugFrame137:
    jmp InuX64InterruptCommon

global InuX64InterruptStub138
InuX64InterruptStub138:
    push qword 0
    push qword 138
global InuX64InterruptDebugFrame138
InuX64InterruptDebugFrame138:
    jmp InuX64InterruptCommon

global InuX64InterruptStub139
InuX64InterruptStub139:
    push qword 0
    push qword 139
global InuX64InterruptDebugFrame139
InuX64InterruptDebugFrame139:
    jmp InuX64InterruptCommon

global InuX64InterruptStub140
InuX64InterruptStub140:
    push qword 0
    push qword 140
global InuX64InterruptDebugFrame140
InuX64InterruptDebugFrame140:
    jmp InuX64InterruptCommon

global InuX64InterruptStub141
InuX64InterruptStub141:
    push qword 0
    push qword 141
global InuX64InterruptDebugFrame141
InuX64InterruptDebugFrame141:
    jmp InuX64InterruptCommon

global InuX64InterruptStub142
InuX64InterruptStub142:
    push qword 0
    push qword 142
global InuX64InterruptDebugFrame142
InuX64InterruptDebugFrame142:
    jmp InuX64InterruptCommon

global InuX64InterruptStub143
InuX64InterruptStub143:
    push qword 0
    push qword 143
global InuX64InterruptDebugFrame143
InuX64InterruptDebugFrame143:
    jmp InuX64InterruptCommon

global InuX64InterruptStub144
InuX64InterruptStub144:
    push qword 0
    push qword 144
global InuX64InterruptDebugFrame144
InuX64InterruptDebugFrame144:
    jmp InuX64InterruptCommon

global InuX64InterruptStub145
InuX64InterruptStub145:
    push qword 0
    push qword 145
global InuX64InterruptDebugFrame145
InuX64InterruptDebugFrame145:
    jmp InuX64InterruptCommon

global InuX64InterruptStub146
InuX64InterruptStub146:
    push qword 0
    push qword 146
global InuX64InterruptDebugFrame146
InuX64InterruptDebugFrame146:
    jmp InuX64InterruptCommon

global InuX64InterruptStub147
InuX64InterruptStub147:
    push qword 0
    push qword 147
global InuX64InterruptDebugFrame147
InuX64InterruptDebugFrame147:
    jmp InuX64InterruptCommon

global InuX64InterruptStub148
InuX64InterruptStub148:
    push qword 0
    push qword 148
global InuX64InterruptDebugFrame148
InuX64InterruptDebugFrame148:
    jmp InuX64InterruptCommon

global InuX64InterruptStub149
InuX64InterruptStub149:
    push qword 0
    push qword 149
global InuX64InterruptDebugFrame149
InuX64InterruptDebugFrame149:
    jmp InuX64InterruptCommon

global InuX64InterruptStub150
InuX64InterruptStub150:
    push qword 0
    push qword 150
global InuX64InterruptDebugFrame150
InuX64InterruptDebugFrame150:
    jmp InuX64InterruptCommon

global InuX64InterruptStub151
InuX64InterruptStub151:
    push qword 0
    push qword 151
global InuX64InterruptDebugFrame151
InuX64InterruptDebugFrame151:
    jmp InuX64InterruptCommon

global InuX64InterruptStub152
InuX64InterruptStub152:
    push qword 0
    push qword 152
global InuX64InterruptDebugFrame152
InuX64InterruptDebugFrame152:
    jmp InuX64InterruptCommon

global InuX64InterruptStub153
InuX64InterruptStub153:
    push qword 0
    push qword 153
global InuX64InterruptDebugFrame153
InuX64InterruptDebugFrame153:
    jmp InuX64InterruptCommon

global InuX64InterruptStub154
InuX64InterruptStub154:
    push qword 0
    push qword 154
global InuX64InterruptDebugFrame154
InuX64InterruptDebugFrame154:
    jmp InuX64InterruptCommon

global InuX64InterruptStub155
InuX64InterruptStub155:
    push qword 0
    push qword 155
global InuX64InterruptDebugFrame155
InuX64InterruptDebugFrame155:
    jmp InuX64InterruptCommon

global InuX64InterruptStub156
InuX64InterruptStub156:
    push qword 0
    push qword 156
global InuX64InterruptDebugFrame156
InuX64InterruptDebugFrame156:
    jmp InuX64InterruptCommon

global InuX64InterruptStub157
InuX64InterruptStub157:
    push qword 0
    push qword 157
global InuX64InterruptDebugFrame157
InuX64InterruptDebugFrame157:
    jmp InuX64InterruptCommon

global InuX64InterruptStub158
InuX64InterruptStub158:
    push qword 0
    push qword 158
global InuX64InterruptDebugFrame158
InuX64InterruptDebugFrame158:
    jmp InuX64InterruptCommon

global InuX64InterruptStub159
InuX64InterruptStub159:
    push qword 0
    push qword 159
global InuX64InterruptDebugFrame159
InuX64InterruptDebugFrame159:
    jmp InuX64InterruptCommon

global InuX64InterruptStub160
InuX64InterruptStub160:
    push qword 0
    push qword 160
global InuX64InterruptDebugFrame160
InuX64InterruptDebugFrame160:
    jmp InuX64InterruptCommon

global InuX64InterruptStub161
InuX64InterruptStub161:
    push qword 0
    push qword 161
global InuX64InterruptDebugFrame161
InuX64InterruptDebugFrame161:
    jmp InuX64InterruptCommon

global InuX64InterruptStub162
InuX64InterruptStub162:
    push qword 0
    push qword 162
global InuX64InterruptDebugFrame162
InuX64InterruptDebugFrame162:
    jmp InuX64InterruptCommon

global InuX64InterruptStub163
InuX64InterruptStub163:
    push qword 0
    push qword 163
global InuX64InterruptDebugFrame163
InuX64InterruptDebugFrame163:
    jmp InuX64InterruptCommon

global InuX64InterruptStub164
InuX64InterruptStub164:
    push qword 0
    push qword 164
global InuX64InterruptDebugFrame164
InuX64InterruptDebugFrame164:
    jmp InuX64InterruptCommon

global InuX64InterruptStub165
InuX64InterruptStub165:
    push qword 0
    push qword 165
global InuX64InterruptDebugFrame165
InuX64InterruptDebugFrame165:
    jmp InuX64InterruptCommon

global InuX64InterruptStub166
InuX64InterruptStub166:
    push qword 0
    push qword 166
global InuX64InterruptDebugFrame166
InuX64InterruptDebugFrame166:
    jmp InuX64InterruptCommon

global InuX64InterruptStub167
InuX64InterruptStub167:
    push qword 0
    push qword 167
global InuX64InterruptDebugFrame167
InuX64InterruptDebugFrame167:
    jmp InuX64InterruptCommon

global InuX64InterruptStub168
InuX64InterruptStub168:
    push qword 0
    push qword 168
global InuX64InterruptDebugFrame168
InuX64InterruptDebugFrame168:
    jmp InuX64InterruptCommon

global InuX64InterruptStub169
InuX64InterruptStub169:
    push qword 0
    push qword 169
global InuX64InterruptDebugFrame169
InuX64InterruptDebugFrame169:
    jmp InuX64InterruptCommon

global InuX64InterruptStub170
InuX64InterruptStub170:
    push qword 0
    push qword 170
global InuX64InterruptDebugFrame170
InuX64InterruptDebugFrame170:
    jmp InuX64InterruptCommon

global InuX64InterruptStub171
InuX64InterruptStub171:
    push qword 0
    push qword 171
global InuX64InterruptDebugFrame171
InuX64InterruptDebugFrame171:
    jmp InuX64InterruptCommon

global InuX64InterruptStub172
InuX64InterruptStub172:
    push qword 0
    push qword 172
global InuX64InterruptDebugFrame172
InuX64InterruptDebugFrame172:
    jmp InuX64InterruptCommon

global InuX64InterruptStub173
InuX64InterruptStub173:
    push qword 0
    push qword 173
global InuX64InterruptDebugFrame173
InuX64InterruptDebugFrame173:
    jmp InuX64InterruptCommon

global InuX64InterruptStub174
InuX64InterruptStub174:
    push qword 0
    push qword 174
global InuX64InterruptDebugFrame174
InuX64InterruptDebugFrame174:
    jmp InuX64InterruptCommon

global InuX64InterruptStub175
InuX64InterruptStub175:
    push qword 0
    push qword 175
global InuX64InterruptDebugFrame175
InuX64InterruptDebugFrame175:
    jmp InuX64InterruptCommon

global InuX64InterruptStub176
InuX64InterruptStub176:
    push qword 0
    push qword 176
global InuX64InterruptDebugFrame176
InuX64InterruptDebugFrame176:
    jmp InuX64InterruptCommon

global InuX64InterruptStub177
InuX64InterruptStub177:
    push qword 0
    push qword 177
global InuX64InterruptDebugFrame177
InuX64InterruptDebugFrame177:
    jmp InuX64InterruptCommon

global InuX64InterruptStub178
InuX64InterruptStub178:
    push qword 0
    push qword 178
global InuX64InterruptDebugFrame178
InuX64InterruptDebugFrame178:
    jmp InuX64InterruptCommon

global InuX64InterruptStub179
InuX64InterruptStub179:
    push qword 0
    push qword 179
global InuX64InterruptDebugFrame179
InuX64InterruptDebugFrame179:
    jmp InuX64InterruptCommon

global InuX64InterruptStub180
InuX64InterruptStub180:
    push qword 0
    push qword 180
global InuX64InterruptDebugFrame180
InuX64InterruptDebugFrame180:
    jmp InuX64InterruptCommon

global InuX64InterruptStub181
InuX64InterruptStub181:
    push qword 0
    push qword 181
global InuX64InterruptDebugFrame181
InuX64InterruptDebugFrame181:
    jmp InuX64InterruptCommon

global InuX64InterruptStub182
InuX64InterruptStub182:
    push qword 0
    push qword 182
global InuX64InterruptDebugFrame182
InuX64InterruptDebugFrame182:
    jmp InuX64InterruptCommon

global InuX64InterruptStub183
InuX64InterruptStub183:
    push qword 0
    push qword 183
global InuX64InterruptDebugFrame183
InuX64InterruptDebugFrame183:
    jmp InuX64InterruptCommon

global InuX64InterruptStub184
InuX64InterruptStub184:
    push qword 0
    push qword 184
global InuX64InterruptDebugFrame184
InuX64InterruptDebugFrame184:
    jmp InuX64InterruptCommon

global InuX64InterruptStub185
InuX64InterruptStub185:
    push qword 0
    push qword 185
global InuX64InterruptDebugFrame185
InuX64InterruptDebugFrame185:
    jmp InuX64InterruptCommon

global InuX64InterruptStub186
InuX64InterruptStub186:
    push qword 0
    push qword 186
global InuX64InterruptDebugFrame186
InuX64InterruptDebugFrame186:
    jmp InuX64InterruptCommon

global InuX64InterruptStub187
InuX64InterruptStub187:
    push qword 0
    push qword 187
global InuX64InterruptDebugFrame187
InuX64InterruptDebugFrame187:
    jmp InuX64InterruptCommon

global InuX64InterruptStub188
InuX64InterruptStub188:
    push qword 0
    push qword 188
global InuX64InterruptDebugFrame188
InuX64InterruptDebugFrame188:
    jmp InuX64InterruptCommon

global InuX64InterruptStub189
InuX64InterruptStub189:
    push qword 0
    push qword 189
global InuX64InterruptDebugFrame189
InuX64InterruptDebugFrame189:
    jmp InuX64InterruptCommon

global InuX64InterruptStub190
InuX64InterruptStub190:
    push qword 0
    push qword 190
global InuX64InterruptDebugFrame190
InuX64InterruptDebugFrame190:
    jmp InuX64InterruptCommon

global InuX64InterruptStub191
InuX64InterruptStub191:
    push qword 0
    push qword 191
global InuX64InterruptDebugFrame191
InuX64InterruptDebugFrame191:
    jmp InuX64InterruptCommon

global InuX64InterruptStub192
InuX64InterruptStub192:
    push qword 0
    push qword 192
global InuX64InterruptDebugFrame192
InuX64InterruptDebugFrame192:
    jmp InuX64InterruptCommon

global InuX64InterruptStub193
InuX64InterruptStub193:
    push qword 0
    push qword 193
global InuX64InterruptDebugFrame193
InuX64InterruptDebugFrame193:
    jmp InuX64InterruptCommon

global InuX64InterruptStub194
InuX64InterruptStub194:
    push qword 0
    push qword 194
global InuX64InterruptDebugFrame194
InuX64InterruptDebugFrame194:
    jmp InuX64InterruptCommon

global InuX64InterruptStub195
InuX64InterruptStub195:
    push qword 0
    push qword 195
global InuX64InterruptDebugFrame195
InuX64InterruptDebugFrame195:
    jmp InuX64InterruptCommon

global InuX64InterruptStub196
InuX64InterruptStub196:
    push qword 0
    push qword 196
global InuX64InterruptDebugFrame196
InuX64InterruptDebugFrame196:
    jmp InuX64InterruptCommon

global InuX64InterruptStub197
InuX64InterruptStub197:
    push qword 0
    push qword 197
global InuX64InterruptDebugFrame197
InuX64InterruptDebugFrame197:
    jmp InuX64InterruptCommon

global InuX64InterruptStub198
InuX64InterruptStub198:
    push qword 0
    push qword 198
global InuX64InterruptDebugFrame198
InuX64InterruptDebugFrame198:
    jmp InuX64InterruptCommon

global InuX64InterruptStub199
InuX64InterruptStub199:
    push qword 0
    push qword 199
global InuX64InterruptDebugFrame199
InuX64InterruptDebugFrame199:
    jmp InuX64InterruptCommon

global InuX64InterruptStub200
InuX64InterruptStub200:
    push qword 0
    push qword 200
global InuX64InterruptDebugFrame200
InuX64InterruptDebugFrame200:
    jmp InuX64InterruptCommon

global InuX64InterruptStub201
InuX64InterruptStub201:
    push qword 0
    push qword 201
global InuX64InterruptDebugFrame201
InuX64InterruptDebugFrame201:
    jmp InuX64InterruptCommon

global InuX64InterruptStub202
InuX64InterruptStub202:
    push qword 0
    push qword 202
global InuX64InterruptDebugFrame202
InuX64InterruptDebugFrame202:
    jmp InuX64InterruptCommon

global InuX64InterruptStub203
InuX64InterruptStub203:
    push qword 0
    push qword 203
global InuX64InterruptDebugFrame203
InuX64InterruptDebugFrame203:
    jmp InuX64InterruptCommon

global InuX64InterruptStub204
InuX64InterruptStub204:
    push qword 0
    push qword 204
global InuX64InterruptDebugFrame204
InuX64InterruptDebugFrame204:
    jmp InuX64InterruptCommon

global InuX64InterruptStub205
InuX64InterruptStub205:
    push qword 0
    push qword 205
global InuX64InterruptDebugFrame205
InuX64InterruptDebugFrame205:
    jmp InuX64InterruptCommon

global InuX64InterruptStub206
InuX64InterruptStub206:
    push qword 0
    push qword 206
global InuX64InterruptDebugFrame206
InuX64InterruptDebugFrame206:
    jmp InuX64InterruptCommon

global InuX64InterruptStub207
InuX64InterruptStub207:
    push qword 0
    push qword 207
global InuX64InterruptDebugFrame207
InuX64InterruptDebugFrame207:
    jmp InuX64InterruptCommon

global InuX64InterruptStub208
InuX64InterruptStub208:
    push qword 0
    push qword 208
global InuX64InterruptDebugFrame208
InuX64InterruptDebugFrame208:
    jmp InuX64InterruptCommon

global InuX64InterruptStub209
InuX64InterruptStub209:
    push qword 0
    push qword 209
global InuX64InterruptDebugFrame209
InuX64InterruptDebugFrame209:
    jmp InuX64InterruptCommon

global InuX64InterruptStub210
InuX64InterruptStub210:
    push qword 0
    push qword 210
global InuX64InterruptDebugFrame210
InuX64InterruptDebugFrame210:
    jmp InuX64InterruptCommon

global InuX64InterruptStub211
InuX64InterruptStub211:
    push qword 0
    push qword 211
global InuX64InterruptDebugFrame211
InuX64InterruptDebugFrame211:
    jmp InuX64InterruptCommon

global InuX64InterruptStub212
InuX64InterruptStub212:
    push qword 0
    push qword 212
global InuX64InterruptDebugFrame212
InuX64InterruptDebugFrame212:
    jmp InuX64InterruptCommon

global InuX64InterruptStub213
InuX64InterruptStub213:
    push qword 0
    push qword 213
global InuX64InterruptDebugFrame213
InuX64InterruptDebugFrame213:
    jmp InuX64InterruptCommon

global InuX64InterruptStub214
InuX64InterruptStub214:
    push qword 0
    push qword 214
global InuX64InterruptDebugFrame214
InuX64InterruptDebugFrame214:
    jmp InuX64InterruptCommon

global InuX64InterruptStub215
InuX64InterruptStub215:
    push qword 0
    push qword 215
global InuX64InterruptDebugFrame215
InuX64InterruptDebugFrame215:
    jmp InuX64InterruptCommon

global InuX64InterruptStub216
InuX64InterruptStub216:
    push qword 0
    push qword 216
global InuX64InterruptDebugFrame216
InuX64InterruptDebugFrame216:
    jmp InuX64InterruptCommon

global InuX64InterruptStub217
InuX64InterruptStub217:
    push qword 0
    push qword 217
global InuX64InterruptDebugFrame217
InuX64InterruptDebugFrame217:
    jmp InuX64InterruptCommon

global InuX64InterruptStub218
InuX64InterruptStub218:
    push qword 0
    push qword 218
global InuX64InterruptDebugFrame218
InuX64InterruptDebugFrame218:
    jmp InuX64InterruptCommon

global InuX64InterruptStub219
InuX64InterruptStub219:
    push qword 0
    push qword 219
global InuX64InterruptDebugFrame219
InuX64InterruptDebugFrame219:
    jmp InuX64InterruptCommon

global InuX64InterruptStub220
InuX64InterruptStub220:
    push qword 0
    push qword 220
global InuX64InterruptDebugFrame220
InuX64InterruptDebugFrame220:
    jmp InuX64InterruptCommon

global InuX64InterruptStub221
InuX64InterruptStub221:
    push qword 0
    push qword 221
global InuX64InterruptDebugFrame221
InuX64InterruptDebugFrame221:
    jmp InuX64InterruptCommon

global InuX64InterruptStub222
InuX64InterruptStub222:
    push qword 0
    push qword 222
global InuX64InterruptDebugFrame222
InuX64InterruptDebugFrame222:
    jmp InuX64InterruptCommon

global InuX64InterruptStub223
InuX64InterruptStub223:
    push qword 0
    push qword 223
global InuX64InterruptDebugFrame223
InuX64InterruptDebugFrame223:
    jmp InuX64InterruptCommon

global InuX64InterruptStub224
InuX64InterruptStub224:
    push qword 0
    push qword 224
global InuX64InterruptDebugFrame224
InuX64InterruptDebugFrame224:
    jmp InuX64InterruptCommon

global InuX64InterruptStub225
InuX64InterruptStub225:
    push qword 0
    push qword 225
global InuX64InterruptDebugFrame225
InuX64InterruptDebugFrame225:
    jmp InuX64InterruptCommon

global InuX64InterruptStub226
InuX64InterruptStub226:
    push qword 0
    push qword 226
global InuX64InterruptDebugFrame226
InuX64InterruptDebugFrame226:
    jmp InuX64InterruptCommon

global InuX64InterruptStub227
InuX64InterruptStub227:
    push qword 0
    push qword 227
global InuX64InterruptDebugFrame227
InuX64InterruptDebugFrame227:
    jmp InuX64InterruptCommon

global InuX64InterruptStub228
InuX64InterruptStub228:
    push qword 0
    push qword 228
global InuX64InterruptDebugFrame228
InuX64InterruptDebugFrame228:
    jmp InuX64InterruptCommon

global InuX64InterruptStub229
InuX64InterruptStub229:
    push qword 0
    push qword 229
global InuX64InterruptDebugFrame229
InuX64InterruptDebugFrame229:
    jmp InuX64InterruptCommon

global InuX64InterruptStub230
InuX64InterruptStub230:
    push qword 0
    push qword 230
global InuX64InterruptDebugFrame230
InuX64InterruptDebugFrame230:
    jmp InuX64InterruptCommon

global InuX64InterruptStub231
InuX64InterruptStub231:
    push qword 0
    push qword 231
global InuX64InterruptDebugFrame231
InuX64InterruptDebugFrame231:
    jmp InuX64InterruptCommon

global InuX64InterruptStub232
InuX64InterruptStub232:
    push qword 0
    push qword 232
global InuX64InterruptDebugFrame232
InuX64InterruptDebugFrame232:
    jmp InuX64InterruptCommon

global InuX64InterruptStub233
InuX64InterruptStub233:
    push qword 0
    push qword 233
global InuX64InterruptDebugFrame233
InuX64InterruptDebugFrame233:
    jmp InuX64InterruptCommon

global InuX64InterruptStub234
InuX64InterruptStub234:
    push qword 0
    push qword 234
global InuX64InterruptDebugFrame234
InuX64InterruptDebugFrame234:
    jmp InuX64InterruptCommon

global InuX64InterruptStub235
InuX64InterruptStub235:
    push qword 0
    push qword 235
global InuX64InterruptDebugFrame235
InuX64InterruptDebugFrame235:
    jmp InuX64InterruptCommon

global InuX64InterruptStub236
InuX64InterruptStub236:
    push qword 0
    push qword 236
global InuX64InterruptDebugFrame236
InuX64InterruptDebugFrame236:
    jmp InuX64InterruptCommon

global InuX64InterruptStub237
InuX64InterruptStub237:
    push qword 0
    push qword 237
global InuX64InterruptDebugFrame237
InuX64InterruptDebugFrame237:
    jmp InuX64InterruptCommon

global InuX64InterruptStub238
InuX64InterruptStub238:
    push qword 0
    push qword 238
global InuX64InterruptDebugFrame238
InuX64InterruptDebugFrame238:
    jmp InuX64InterruptCommon

global InuX64InterruptStub239
InuX64InterruptStub239:
    push qword 0
    push qword 239
global InuX64InterruptDebugFrame239
InuX64InterruptDebugFrame239:
    jmp InuX64InterruptCommon

global InuX64InterruptStub240
InuX64InterruptStub240:
    push qword 0
    push qword 240
global InuX64InterruptDebugFrame240
InuX64InterruptDebugFrame240:
    jmp InuX64InterruptCommon

global InuX64InterruptStub241
InuX64InterruptStub241:
    push qword 0
    push qword 241
global InuX64InterruptDebugFrame241
InuX64InterruptDebugFrame241:
    jmp InuX64InterruptCommon

global InuX64InterruptStub242
InuX64InterruptStub242:
    push qword 0
    push qword 242
global InuX64InterruptDebugFrame242
InuX64InterruptDebugFrame242:
    jmp InuX64InterruptCommon

global InuX64InterruptStub243
InuX64InterruptStub243:
    push qword 0
    push qword 243
global InuX64InterruptDebugFrame243
InuX64InterruptDebugFrame243:
    jmp InuX64InterruptCommon

global InuX64InterruptStub244
InuX64InterruptStub244:
    push qword 0
    push qword 244
global InuX64InterruptDebugFrame244
InuX64InterruptDebugFrame244:
    jmp InuX64InterruptCommon

global InuX64InterruptStub245
InuX64InterruptStub245:
    push qword 0
    push qword 245
global InuX64InterruptDebugFrame245
InuX64InterruptDebugFrame245:
    jmp InuX64InterruptCommon

global InuX64InterruptStub246
InuX64InterruptStub246:
    push qword 0
    push qword 246
global InuX64InterruptDebugFrame246
InuX64InterruptDebugFrame246:
    jmp InuX64InterruptCommon

global InuX64InterruptStub247
InuX64InterruptStub247:
    push qword 0
    push qword 247
global InuX64InterruptDebugFrame247
InuX64InterruptDebugFrame247:
    jmp InuX64InterruptCommon

global InuX64InterruptStub248
InuX64InterruptStub248:
    push qword 0
    push qword 248
global InuX64InterruptDebugFrame248
InuX64InterruptDebugFrame248:
    jmp InuX64InterruptCommon

global InuX64InterruptStub249
InuX64InterruptStub249:
    push qword 0
    push qword 249
global InuX64InterruptDebugFrame249
InuX64InterruptDebugFrame249:
    jmp InuX64InterruptCommon

global InuX64InterruptStub250
InuX64InterruptStub250:
    push qword 0
    push qword 250
global InuX64InterruptDebugFrame250
InuX64InterruptDebugFrame250:
    jmp InuX64InterruptCommon

global InuX64InterruptStub251
InuX64InterruptStub251:
    push qword 0
    push qword 251
global InuX64InterruptDebugFrame251
InuX64InterruptDebugFrame251:
    jmp InuX64InterruptCommon

global InuX64InterruptStub252
InuX64InterruptStub252:
    push qword 0
    push qword 252
global InuX64InterruptDebugFrame252
InuX64InterruptDebugFrame252:
    jmp InuX64InterruptCommon

global InuX64InterruptStub253
InuX64InterruptStub253:
    push qword 0
    push qword 253
global InuX64InterruptDebugFrame253
InuX64InterruptDebugFrame253:
    jmp InuX64InterruptCommon

global InuX64InterruptStub254
InuX64InterruptStub254:
    push qword 0
    push qword 254
global InuX64InterruptDebugFrame254
InuX64InterruptDebugFrame254:
    jmp InuX64InterruptCommon

global InuX64InterruptStub255
InuX64InterruptStub255:
    push qword 0
    push qword 255
global InuX64InterruptDebugFrame255
InuX64InterruptDebugFrame255:
    jmp InuX64InterruptCommon

InuX64InterruptCommon:
    sub rsp, 224
    mov [rsp + 104], rax
    mov [rsp + 112], rbx
    mov [rsp + 120], rcx
    mov [rsp + 128], rdx
    mov [rsp + 136], rsi
    mov [rsp + 144], rdi
    mov [rsp + 152], rbp
    mov [rsp + 160], r8
    mov [rsp + 168], r9
    mov [rsp + 176], r10
    mov [rsp + 184], r11
    mov [rsp + 192], r12
    mov [rsp + 200], r13
    mov [rsp + 208], r14
    mov [rsp + 216], r15
    lea r10, [rsp + 224]
    mov rax, [r10 + 0]
    mov [rsp + 0], rax
    mov rax, [r10 + 8]
    mov [rsp + 8], rax
    mov rax, [r10 + 16]
    mov [rsp + 16], rax
    mov rax, [r10 + 24]
    mov [rsp + 24], rax
    mov rax, [r10 + 32]
    mov [rsp + 32], rax
    mov rax, cr0
    mov [rsp + 56], rax
    mov rax, cr2
    mov [rsp + 64], rax
    mov rax, cr3
    mov [rsp + 72], rax
    mov rax, cr4
    mov [rsp + 80], rax
    mov rax, [rsp + 24]
    and eax, 3
    setnz al
    movzx eax, al
    mov rdx, [rsp + 0]
    lea r8, [rel InuX64InterruptStackSwitch]
    cmp byte [r8 + rdx], 0
    je .stack_policy_ready
    or eax, 2
.stack_policy_ready:
    mov [rsp + 96], rax
    test eax, eax
    jz .same_privilege
    mov rax, [r10 + 40]
    mov [rsp + 40], rax
    mov rax, [r10 + 48]
    mov [rsp + 48], rax
    jmp .stack_done
.same_privilege:
    lea rax, [r10 + 40]
    mov [rsp + 40], rax
    xor eax, eax
    mov ax, ss
    mov [rsp + 48], rax
.stack_done:
    ; Hardware does not perform SWAPGS for interrupts/exceptions.  When the frame came
    ; from CPL3, expose the per-CPU kernel state before entering managed interrupt code.
    ; The normal resume path swaps back immediately before IRETQ.
    test qword [rsp + 96], 1
    jz .interrupt_gs_ready
    swapgs
.interrupt_gs_ready:
    mov eax, 1
    cpuid
    shr ebx, 24
    mov [rsp + 88], rbx
    cmp qword [rsp + 0], 2
    jne .nmi_snapshot_done
    call InuX64RecordNmiDiagnostics
.nmi_snapshot_done:
    mov rax, [rel InuX64InterruptDispatcher]
    test rax, rax
    jnz .dispatcher_ready
    ; NMI is asynchronous and may arrive after the bootstrap IDT is installed but
    ; before managed interrupt dispatch is published.  It must not turn that normal
    ; bootstrap window into a terminal halt.  All other vectors remain fail-closed.
    cmp qword [rsp + 0], 2
    je .resume
    jmp InuX64StopProcessor
.dispatcher_ready:
    mov rcx, rsp
    sub rsp, 40
    call rax
    add rsp, 40
    cmp eax, 4
    je .abort_user_fault
    cmp eax, 5
    je .abort_user_cancel
    cmp eax, 1
    je .resume
    cmp eax, 2
    je .resume
    ; If this was a user-origin interrupt, GS currently exposes the per-CPU kernel
    ; state.  Do not leave it swapped when taking the legacy processor-fatal path.
    test qword [rsp + 96], 1
    jz InuX64StopProcessor
    swapgs
    jmp InuX64StopProcessor

.abort_user_cancel:
    ; Managed dispatch return code 5 means Ctrl-C requested a controlled process
    ; termination while this interrupt originated at CPL3.  RequestUserModeExit
    ; has already stored the exit code and transition flag in the per-CPU state.
    test qword [rsp + 96], 1
    jz InuX64StopProcessor
    cmp qword [gs:0x68], 1
    jne InuX64StopProcessor
    mov r12, [gs:0x58]
    mov r13, [gs:0x60]
    mov qword [gs:0x78], 0
    mov al, 'C'
    call InuX64SyscallTraceByte
    swapgs
    mov rsp, r12
    mov eax, 1
    jmp r13

.abort_user_fault:
    ; Managed dispatch uses return code 4 only for an unhandled CPL3 exception.
    ; Contain the fault to the active process instead of halting the whole processor.
    test qword [rsp + 96], 1
    jz InuX64StopProcessor
    mov r12, [gs:0x58]
    mov r13, [gs:0x60]
    mov rax, [rsp + 0]
    inc rax
    neg rax
    mov [gs:0x70], rax
    mov qword [gs:0x68], 2
    mov qword [gs:0x78], 0
    mov al, 'F'
    call InuX64SyscallTraceByte
    swapgs
    mov rsp, r12
    mov eax, 2
    jmp r13

.resume:
    lea r10, [rsp + 224]
    mov rax, [rsp + 16]
    mov [r10 + 16], rax
    mov rax, [rsp + 24]
    mov [r10 + 24], rax
    mov rax, [rsp + 32]
    mov [r10 + 32], rax
    cmp qword [rsp + 96], 0
    je .restore_registers
    mov rax, [rsp + 40]
    mov [r10 + 40], rax
    mov rax, [rsp + 48]
    mov [r10 + 48], rax
    ; Managed kernel code has finished.  Restore the user GS base before restoring
    ; the interrupted register set and executing IRETQ.
    test qword [rsp + 96], 1
    jz .restore_registers
    swapgs
.restore_registers:
    mov rax, [rsp + 104]
    mov rbx, [rsp + 112]
    mov rcx, [rsp + 120]
    mov rdx, [rsp + 128]
    mov rsi, [rsp + 136]
    mov rdi, [rsp + 144]
    mov rbp, [rsp + 152]
    mov r8, [rsp + 160]
    mov r9, [rsp + 168]
    mov r10, [rsp + 176]
    mov r11, [rsp + 184]
    mov r12, [rsp + 192]
    mov r13, [rsp + 200]
    mov r14, [rsp + 208]
    mov r15, [rsp + 216]
    add rsp, 224
    add rsp, 16
    iretq




; Records the machine-visible state when a real NMI reaches the common entry.
; The vector-specific debugger breakpoint is intentionally outside this routine;
; with the default 0.0.189 policy vector 2 is not armed, so real NMIs reach this
; recorder and then resume through the managed/asynchronous NMI path.
InuX64RecordNmiDiagnostics:
    lock inc qword [rel InuX64NmiDiagnosticState + 8]
    or qword [rel InuX64NmiDiagnosticState + 120], 4
    in al, 0x61
    movzx eax, al
    mov [rel InuX64NmiDiagnosticState + 112], rax
    mov ecx, 0x1B
    rdmsr
    mov r8d, eax
    mov r9d, edx
    mov eax, r8d
    mov edx, r9d
    shl rdx, 32
    or rax, rdx
    mov [rel InuX64NmiDiagnosticState + 64], rax
    test r8d, (1 << 11)
    jz .record_done
    test r8d, (1 << 10)
    jnz .record_x2apic
    mov eax, r8d
    and eax, 0xFFFFF000
    mov edx, r9d
    and edx, 0xF
    shl rdx, 32
    or rax, rdx
    mov r10, rax
    mov eax, [r10 + 0x350]
    mov [rel InuX64NmiDiagnosticState + 72], rax
    mov eax, [r10 + 0x360]
    mov [rel InuX64NmiDiagnosticState + 80], rax
    mov eax, [r10 + 0x330]
    mov [rel InuX64NmiDiagnosticState + 88], rax
    mov eax, [r10 + 0x340]
    mov [rel InuX64NmiDiagnosticState + 96], rax
    mov eax, [r10 + 0x280]
    mov [rel InuX64NmiDiagnosticState + 104], rax
    ret
.record_x2apic:
    mov ecx, 0x835
    rdmsr
    mov [rel InuX64NmiDiagnosticState + 72], rax
    mov ecx, 0x836
    rdmsr
    mov [rel InuX64NmiDiagnosticState + 80], rax
    mov ecx, 0x833
    rdmsr
    mov [rel InuX64NmiDiagnosticState + 88], rax
    mov ecx, 0x834
    rdmsr
    mov [rel InuX64NmiDiagnosticState + 96], rax
    mov ecx, 0x828
    rdmsr
    mov [rel InuX64NmiDiagnosticState + 104], rax
.record_done:
    ret


section .bss align=16
InuX64BootstrapIdt: resb 4096

section .text
global InuX64InitializeBootstrapInterrupts

; Builds all 256 gates and installs the bootstrap processor IDT.
InuX64InitializeBootstrapInterrupts:
    lea r10, [rel InuX64BootstrapIdt]
    lea r11, [rel InuX64InterruptStubTable]
    lea r9, [rel InuX64InterruptStackSwitch]
    xor r8d, r8d
.bootstrap_idt_loop:
    mov rax, [r11 + r8 * 8]
    mov rdx, r8
    shl rdx, 4
    add rdx, r10
    mov [rdx + 0], ax
    mov word [rdx + 2], 0x08
    mov byte [rdx + 4], 0
    cmp r8d, 8
    jne .check_nmi
    mov byte [rdx + 4], 1
    mov byte [r9 + r8], 1
    jmp .ist_done
.check_nmi:
    cmp r8d, 2
    jne .check_machine_check
    mov byte [rdx + 4], 2
    mov byte [r9 + r8], 1
    jmp .ist_done
.check_machine_check:
    cmp r8d, 18
    jne .ist_done
    mov byte [rdx + 4], 3
    mov byte [r9 + r8], 1
.ist_done:
    mov byte [rdx + 5], 0x8E
    shr rax, 16
    mov [rdx + 6], ax
    shr rax, 16
    mov [rdx + 8], eax
    mov dword [rdx + 12], 0
    inc r8d
    cmp r8d, 256
    jne .bootstrap_idt_loop
    sub rsp, 16
    mov word [rsp], 4095
    mov [rsp + 2], r10
    lidt [rsp]
    add rsp, 16
    mov eax, 1
    ret

align 8
InuX64InterruptStubTable:
    dq InuX64InterruptStub0
    dq InuX64InterruptStub1
    dq InuX64InterruptStub2
    dq InuX64InterruptStub3
    dq InuX64InterruptStub4
    dq InuX64InterruptStub5
    dq InuX64InterruptStub6
    dq InuX64InterruptStub7
    dq InuX64InterruptStub8
    dq InuX64InterruptStub9
    dq InuX64InterruptStub10
    dq InuX64InterruptStub11
    dq InuX64InterruptStub12
    dq InuX64InterruptStub13
    dq InuX64InterruptStub14
    dq InuX64InterruptStub15
    dq InuX64InterruptStub16
    dq InuX64InterruptStub17
    dq InuX64InterruptStub18
    dq InuX64InterruptStub19
    dq InuX64InterruptStub20
    dq InuX64InterruptStub21
    dq InuX64InterruptStub22
    dq InuX64InterruptStub23
    dq InuX64InterruptStub24
    dq InuX64InterruptStub25
    dq InuX64InterruptStub26
    dq InuX64InterruptStub27
    dq InuX64InterruptStub28
    dq InuX64InterruptStub29
    dq InuX64InterruptStub30
    dq InuX64InterruptStub31
    dq InuX64InterruptStub32
    dq InuX64InterruptStub33
    dq InuX64InterruptStub34
    dq InuX64InterruptStub35
    dq InuX64InterruptStub36
    dq InuX64InterruptStub37
    dq InuX64InterruptStub38
    dq InuX64InterruptStub39
    dq InuX64InterruptStub40
    dq InuX64InterruptStub41
    dq InuX64InterruptStub42
    dq InuX64InterruptStub43
    dq InuX64InterruptStub44
    dq InuX64InterruptStub45
    dq InuX64InterruptStub46
    dq InuX64InterruptStub47
    dq InuX64InterruptStub48
    dq InuX64InterruptStub49
    dq InuX64InterruptStub50
    dq InuX64InterruptStub51
    dq InuX64InterruptStub52
    dq InuX64InterruptStub53
    dq InuX64InterruptStub54
    dq InuX64InterruptStub55
    dq InuX64InterruptStub56
    dq InuX64InterruptStub57
    dq InuX64InterruptStub58
    dq InuX64InterruptStub59
    dq InuX64InterruptStub60
    dq InuX64InterruptStub61
    dq InuX64InterruptStub62
    dq InuX64InterruptStub63
    dq InuX64InterruptStub64
    dq InuX64InterruptStub65
    dq InuX64InterruptStub66
    dq InuX64InterruptStub67
    dq InuX64InterruptStub68
    dq InuX64InterruptStub69
    dq InuX64InterruptStub70
    dq InuX64InterruptStub71
    dq InuX64InterruptStub72
    dq InuX64InterruptStub73
    dq InuX64InterruptStub74
    dq InuX64InterruptStub75
    dq InuX64InterruptStub76
    dq InuX64InterruptStub77
    dq InuX64InterruptStub78
    dq InuX64InterruptStub79
    dq InuX64InterruptStub80
    dq InuX64InterruptStub81
    dq InuX64InterruptStub82
    dq InuX64InterruptStub83
    dq InuX64InterruptStub84
    dq InuX64InterruptStub85
    dq InuX64InterruptStub86
    dq InuX64InterruptStub87
    dq InuX64InterruptStub88
    dq InuX64InterruptStub89
    dq InuX64InterruptStub90
    dq InuX64InterruptStub91
    dq InuX64InterruptStub92
    dq InuX64InterruptStub93
    dq InuX64InterruptStub94
    dq InuX64InterruptStub95
    dq InuX64InterruptStub96
    dq InuX64InterruptStub97
    dq InuX64InterruptStub98
    dq InuX64InterruptStub99
    dq InuX64InterruptStub100
    dq InuX64InterruptStub101
    dq InuX64InterruptStub102
    dq InuX64InterruptStub103
    dq InuX64InterruptStub104
    dq InuX64InterruptStub105
    dq InuX64InterruptStub106
    dq InuX64InterruptStub107
    dq InuX64InterruptStub108
    dq InuX64InterruptStub109
    dq InuX64InterruptStub110
    dq InuX64InterruptStub111
    dq InuX64InterruptStub112
    dq InuX64InterruptStub113
    dq InuX64InterruptStub114
    dq InuX64InterruptStub115
    dq InuX64InterruptStub116
    dq InuX64InterruptStub117
    dq InuX64InterruptStub118
    dq InuX64InterruptStub119
    dq InuX64InterruptStub120
    dq InuX64InterruptStub121
    dq InuX64InterruptStub122
    dq InuX64InterruptStub123
    dq InuX64InterruptStub124
    dq InuX64InterruptStub125
    dq InuX64InterruptStub126
    dq InuX64InterruptStub127
    dq InuX64InterruptStub128
    dq InuX64InterruptStub129
    dq InuX64InterruptStub130
    dq InuX64InterruptStub131
    dq InuX64InterruptStub132
    dq InuX64InterruptStub133
    dq InuX64InterruptStub134
    dq InuX64InterruptStub135
    dq InuX64InterruptStub136
    dq InuX64InterruptStub137
    dq InuX64InterruptStub138
    dq InuX64InterruptStub139
    dq InuX64InterruptStub140
    dq InuX64InterruptStub141
    dq InuX64InterruptStub142
    dq InuX64InterruptStub143
    dq InuX64InterruptStub144
    dq InuX64InterruptStub145
    dq InuX64InterruptStub146
    dq InuX64InterruptStub147
    dq InuX64InterruptStub148
    dq InuX64InterruptStub149
    dq InuX64InterruptStub150
    dq InuX64InterruptStub151
    dq InuX64InterruptStub152
    dq InuX64InterruptStub153
    dq InuX64InterruptStub154
    dq InuX64InterruptStub155
    dq InuX64InterruptStub156
    dq InuX64InterruptStub157
    dq InuX64InterruptStub158
    dq InuX64InterruptStub159
    dq InuX64InterruptStub160
    dq InuX64InterruptStub161
    dq InuX64InterruptStub162
    dq InuX64InterruptStub163
    dq InuX64InterruptStub164
    dq InuX64InterruptStub165
    dq InuX64InterruptStub166
    dq InuX64InterruptStub167
    dq InuX64InterruptStub168
    dq InuX64InterruptStub169
    dq InuX64InterruptStub170
    dq InuX64InterruptStub171
    dq InuX64InterruptStub172
    dq InuX64InterruptStub173
    dq InuX64InterruptStub174
    dq InuX64InterruptStub175
    dq InuX64InterruptStub176
    dq InuX64InterruptStub177
    dq InuX64InterruptStub178
    dq InuX64InterruptStub179
    dq InuX64InterruptStub180
    dq InuX64InterruptStub181
    dq InuX64InterruptStub182
    dq InuX64InterruptStub183
    dq InuX64InterruptStub184
    dq InuX64InterruptStub185
    dq InuX64InterruptStub186
    dq InuX64InterruptStub187
    dq InuX64InterruptStub188
    dq InuX64InterruptStub189
    dq InuX64InterruptStub190
    dq InuX64InterruptStub191
    dq InuX64InterruptStub192
    dq InuX64InterruptStub193
    dq InuX64InterruptStub194
    dq InuX64InterruptStub195
    dq InuX64InterruptStub196
    dq InuX64InterruptStub197
    dq InuX64InterruptStub198
    dq InuX64InterruptStub199
    dq InuX64InterruptStub200
    dq InuX64InterruptStub201
    dq InuX64InterruptStub202
    dq InuX64InterruptStub203
    dq InuX64InterruptStub204
    dq InuX64InterruptStub205
    dq InuX64InterruptStub206
    dq InuX64InterruptStub207
    dq InuX64InterruptStub208
    dq InuX64InterruptStub209
    dq InuX64InterruptStub210
    dq InuX64InterruptStub211
    dq InuX64InterruptStub212
    dq InuX64InterruptStub213
    dq InuX64InterruptStub214
    dq InuX64InterruptStub215
    dq InuX64InterruptStub216
    dq InuX64InterruptStub217
    dq InuX64InterruptStub218
    dq InuX64InterruptStub219
    dq InuX64InterruptStub220
    dq InuX64InterruptStub221
    dq InuX64InterruptStub222
    dq InuX64InterruptStub223
    dq InuX64InterruptStub224
    dq InuX64InterruptStub225
    dq InuX64InterruptStub226
    dq InuX64InterruptStub227
    dq InuX64InterruptStub228
    dq InuX64InterruptStub229
    dq InuX64InterruptStub230
    dq InuX64InterruptStub231
    dq InuX64InterruptStub232
    dq InuX64InterruptStub233
    dq InuX64InterruptStub234
    dq InuX64InterruptStub235
    dq InuX64InterruptStub236
    dq InuX64InterruptStub237
    dq InuX64InterruptStub238
    dq InuX64InterruptStub239
    dq InuX64InterruptStub240
    dq InuX64InterruptStub241
    dq InuX64InterruptStub242
    dq InuX64InterruptStub243
    dq InuX64InterruptStub244
    dq InuX64InterruptStub245
    dq InuX64InterruptStub246
    dq InuX64InterruptStub247
    dq InuX64InterruptStub248
    dq InuX64InterruptStub249
    dq InuX64InterruptStub250
    dq InuX64InterruptStub251
    dq InuX64InterruptStub252
    dq InuX64InterruptStub253
    dq InuX64InterruptStub254
    dq InuX64InterruptStub255
