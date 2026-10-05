using System;

namespace Inu.Kernel.Ps2;

/// <summary>Keyboard layouts supplied by the Inu PS/2 keyboard service.</summary>
public enum KeyboardLayout : UInt32
{
    /// <summary>United Kingdom English keyboard.</summary>
    English_UK = 1U,
    /// <summary>United States English keyboard.</summary>
    English_USA = 2U
}

/// <summary>Logical keys reported by the Set-1 PS/2 keyboard decoder.</summary>
public enum Ps2Key : UInt16
{
    None=0, Escape, D1,D2,D3,D4,D5,D6,D7,D8,D9,D0, Minus, Equals, Backspace, Tab,
    Q,W,E,R,T,Y,U,I,O,P, LeftBracket,RightBracket,Enter,LeftControl,A,S,D,F,G,H,J,K,L,
    Semicolon,Apostrophe,Grave,LeftShift,Backslash,Z,X,C,V,B,N,M,Comma,Period,Slash,RightShift,
    KeypadMultiply,LeftAlt,Space,CapsLock,F1,F2,F3,F4,F5,F6,F7,F8,F9,F10,NumLock,ScrollLock,
    Keypad7,Keypad8,Keypad9,KeypadMinus,Keypad4,Keypad5,Keypad6,KeypadPlus,Keypad1,Keypad2,Keypad3,Keypad0,KeypadDecimal,Oem102,
    F11,F12, RightControl,RightAlt,KeypadEnter,KeypadDivide,Home,Up,PageUp,Left,Right,End,Down,PageDown,Insert,Delete
}

/// <summary>One decoded keyboard transition.</summary>
public readonly struct Ps2KeyboardEvent
{
    public Ps2KeyboardEvent(Ps2Key key, Boolean pressed, Char character, Boolean shift, Boolean control, Boolean alt, Boolean capsLock)
    { Key=key; Pressed=pressed; Character=character; Shift=shift; Control=control; Alt=alt; CapsLock=capsLock; }
    public Ps2Key Key { get; }
    public Boolean Pressed { get; }
    public Char Character { get; }
    public Boolean Shift { get; }
    public Boolean Control { get; }
    public Boolean Alt { get; }
    public Boolean CapsLock { get; }
}

/// <summary>Latest decoded PS/2 mouse state.</summary>
public readonly struct Ps2MouseState
{
    public Ps2MouseState(Int64 x,Int64 y,Boolean left,Boolean right,Boolean middle,Int32 wheel)
    { X=x;Y=y;LeftButton=left;RightButton=right;MiddleButton=middle;Wheel=wheel; }
    public Int64 X { get; }
    public Int64 Y { get; }
    public Boolean LeftButton { get; }
    public Boolean RightButton { get; }
    public Boolean MiddleButton { get; }
    public Int32 Wheel { get; }
}

/// <summary>PS/2 controller and device capabilities.</summary>
public readonly struct Ps2Capabilities
{
    public Ps2Capabilities(Boolean controller,Boolean keyboard,Boolean mouse,KeyboardLayout layout,UInt64 keyboardEvents,UInt64 mousePackets)
    { Controller=controller;Keyboard=keyboard;Mouse=mouse;Layout=layout;KeyboardEvents=keyboardEvents;MousePackets=mousePackets; }
    public Boolean Controller { get; }
    public Boolean Keyboard { get; }
    public Boolean Mouse { get; }
    public KeyboardLayout Layout { get; }
    public UInt64 KeyboardEvents { get; }
    public UInt64 MousePackets { get; }
}
