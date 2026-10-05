using System;

namespace Inu.Kernel.Ps2;

/// <summary>Complete printable Set-1 US and UK keyboard layout translation.</summary>
public static class KeyboardLayouts
{
    /// <summary>Returns the stable display name for one layout.</summary>
    public static String GetName(KeyboardLayout layout) => layout==KeyboardLayout.English_UK ? "English_UK" : layout==KeyboardLayout.English_USA ? "English_USA" : "Unknown";

    /// <summary>Gets whether the supplied layout is installed.</summary>
    public static Boolean IsInstalled(KeyboardLayout layout) => layout==KeyboardLayout.English_UK || layout==KeyboardLayout.English_USA;

    /// <summary>Translates one logical key using the selected full layout and modifier state.</summary>
    public static Char Translate(KeyboardLayout layout, Ps2Key key, Boolean shift, Boolean capsLock, Boolean altGr)
    {
        if (!IsInstalled(layout)) return '\0';
        Boolean upper=shift^capsLock;
        Char letter=Letter(key);
        if(letter!='\0') return upper ? (Char)(letter-32) : letter;
        if (layout==KeyboardLayout.English_UK && altGr && key==Ps2Key.D4) return '€';
        return layout==KeyboardLayout.English_UK ? TranslateUk(key,shift) : TranslateUs(key,shift);
    }

    private static Char Letter(Ps2Key k)
    {
        switch(k){case Ps2Key.A:return 'a';case Ps2Key.B:return 'b';case Ps2Key.C:return 'c';case Ps2Key.D:return 'd';case Ps2Key.E:return 'e';case Ps2Key.F:return 'f';case Ps2Key.G:return 'g';case Ps2Key.H:return 'h';case Ps2Key.I:return 'i';case Ps2Key.J:return 'j';case Ps2Key.K:return 'k';case Ps2Key.L:return 'l';case Ps2Key.M:return 'm';case Ps2Key.N:return 'n';case Ps2Key.O:return 'o';case Ps2Key.P:return 'p';case Ps2Key.Q:return 'q';case Ps2Key.R:return 'r';case Ps2Key.S:return 's';case Ps2Key.T:return 't';case Ps2Key.U:return 'u';case Ps2Key.V:return 'v';case Ps2Key.W:return 'w';case Ps2Key.X:return 'x';case Ps2Key.Y:return 'y';case Ps2Key.Z:return 'z';default:return '\0';}
    }
    private static Char TranslateUs(Ps2Key k,Boolean s)
    {
        switch(k){
            case Ps2Key.Space:return ' ';case Ps2Key.Tab:return '\t';case Ps2Key.Enter:return '\n';case Ps2Key.Backspace:return '\b';
            case Ps2Key.D1:return s?'!':'1';case Ps2Key.D2:return s?'@':'2';case Ps2Key.D3:return s?'#':'3';case Ps2Key.D4:return s?'$':'4';case Ps2Key.D5:return s?'%':'5';case Ps2Key.D6:return s?'^':'6';case Ps2Key.D7:return s?'&':'7';case Ps2Key.D8:return s?'*':'8';case Ps2Key.D9:return s?'(':'9';case Ps2Key.D0:return s?')':'0';
            case Ps2Key.Minus:return s?'_':'-';case Ps2Key.Equals:return s?'+':'=';case Ps2Key.LeftBracket:return s?'{':'[';case Ps2Key.RightBracket:return s?'}':']';case Ps2Key.Backslash:return s?'|':'\\';case Ps2Key.Semicolon:return s?':':';';case Ps2Key.Apostrophe:return s?'"':'\'';case Ps2Key.Grave:return s?'~':'`';case Ps2Key.Comma:return s?'<':',';case Ps2Key.Period:return s?'>':'.';case Ps2Key.Slash:return s?'?':'/';
            case Ps2Key.KeypadMultiply:return '*';case Ps2Key.KeypadMinus:return '-';case Ps2Key.KeypadPlus:return '+';case Ps2Key.KeypadDivide:return '/';case Ps2Key.KeypadEnter:return '\n';case Ps2Key.KeypadDecimal:return '.';case Ps2Key.Keypad0:return '0';case Ps2Key.Keypad1:return '1';case Ps2Key.Keypad2:return '2';case Ps2Key.Keypad3:return '3';case Ps2Key.Keypad4:return '4';case Ps2Key.Keypad5:return '5';case Ps2Key.Keypad6:return '6';case Ps2Key.Keypad7:return '7';case Ps2Key.Keypad8:return '8';case Ps2Key.Keypad9:return '9';default:return '\0';}
    }
    private static Char TranslateUk(Ps2Key k,Boolean s)
    {
        switch(k){
            case Ps2Key.D2:return s?'"':'2';case Ps2Key.D3:return s?'£':'3';case Ps2Key.Apostrophe:return s?'@':'\'';case Ps2Key.Grave:return s?'¬':'`';case Ps2Key.Backslash:return s?'~':'#';case Ps2Key.Oem102:return s?'|':'\\';
            default:return TranslateUs(k,s);
        }
    }
}
