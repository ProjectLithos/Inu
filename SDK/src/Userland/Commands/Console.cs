using System;
using Inu.Userland.Runtime;

namespace Inu.Userland.Commands;

/// <summary>Lets the end user inspect and change text-console colours and caret presentation.</summary>
public static class ConsoleSettings
{
    public static int Main()
    {
        String raw=CommandLine.GetRawArguments();
        if(String.IsNullOrWhiteSpace(raw)){Show();return 0;}
        String[] args=Tokenize(raw);
        if(args.Length==0){Show();return 0;}

        Byte a,b,c,d,e,f;
        if(Equal(args[0],"fg")&&args.Length==4&&TryByte(args[1],out a)&&TryByte(args[2],out b)&&TryByte(args[3],out c))
            return ConsolePresentation.SetForegroundRgb(a,b,c)?0:1;
        if(Equal(args[0],"bg")&&args.Length==4&&TryByte(args[1],out a)&&TryByte(args[2],out b)&&TryByte(args[3],out c))
            return ConsolePresentation.SetBackgroundRgb(a,b,c)?0:1;
        if(Equal(args[0],"colors")&&args.Length==7&&TryByte(args[1],out a)&&TryByte(args[2],out b)&&TryByte(args[3],out c)&&TryByte(args[4],out d)&&TryByte(args[5],out e)&&TryByte(args[6],out f))
            return ConsolePresentation.SetForegroundRgb(a,b,c)&&ConsolePresentation.SetBackgroundRgb(d,e,f)?0:1;
        if(Equal(args[0],"caret")&&args.Length==2)
        {
            if(Equal(args[1],"blink")||Equal(args[1],"blinking"))return ConsolePresentation.SetCaretMode(ConsoleCaretMode.Blinking)?0:1;
            if(Equal(args[1],"visible")||Equal(args[1],"on"))return ConsolePresentation.SetCaretMode(ConsoleCaretMode.Visible)?0:1;
            if(Equal(args[1],"off"))return ConsolePresentation.SetCaretMode(ConsoleCaretMode.Off)?0:1;
        }
        UInt32 percent;
        if(Equal(args[0],"caret")&&args.Length==3&&Equal(args[1],"size")&&TryUInt(args[2],out percent)&&percent>=1U&&percent<=100U)
            return ConsolePresentation.SetCaretHeightPercent(percent)?0:1;
        Usage();return 1;
    }

    private static void Show()
    {
        UInt32 fore=ConsolePresentation.GetForegroundRgb(),back=ConsolePresentation.GetBackgroundRgb();
        Console.Write("Foreground RGB: ");WriteRgb(fore);Console.WriteLine();
        Console.Write("Background RGB: ");WriteRgb(back);Console.WriteLine();
        Console.Write("Caret mode: ");Console.WriteLine(ModeName(ConsolePresentation.GetCaretMode()));
        Console.Write("Caret height: ");Console.Write(ConsolePresentation.GetCaretHeightPercent());Console.WriteLine("%");
    }

    private static void Usage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  console fg <red> <green> <blue>");
        Console.WriteLine("  console bg <red> <green> <blue>");
        Console.WriteLine("  console colors <fr> <fg> <fb> <br> <bg> <bb>");
        Console.WriteLine("  console caret <blink|visible|off>");
        Console.WriteLine("  console caret size <1-100>");
    }

    private static String ModeName(ConsoleCaretMode mode)=>mode==ConsoleCaretMode.Blinking?"Blinking":mode==ConsoleCaretMode.Visible?"Visible":"Off";
    private static void WriteRgb(UInt32 rgb){Console.Write((rgb>>16)&255U);Console.Write(" ");Console.Write((rgb>>8)&255U);Console.Write(" ");Console.Write(rgb&255U);}
    private static Boolean Equal(String a,String b)=>String.Equals(a,b)||AsciiLowerEquals(a,b);
    private static Boolean AsciiLowerEquals(String a,String b){if(a==null||b==null||a.Length!=b.Length)return false;for(Int32 i=0;i<a.Length;i++){Char x=a[i],y=b[i];if(x>='A'&&x<='Z')x=(Char)(x+32);if(y>='A'&&y<='Z')y=(Char)(y+32);if(x!=y)return false;}return true;}
    private static Boolean TryByte(String text,out Byte value){value=0;UInt32 parsed;if(!TryUInt(text,out parsed)||parsed>255U)return false;value=(Byte)parsed;return true;}
    private static Boolean TryUInt(String text,out UInt32 value){value=0U;if(String.IsNullOrEmpty(text))return false;for(Int32 i=0;i<text.Length;i++){Char c=text[i];if(c<'0'||c>'9')return false;UInt32 digit=(UInt32)(c-'0');if(value>(UInt32.MaxValue-digit)/10U)return false;value=value*10U+digit;}return true;}
    private static String[] Tokenize(String text)
    {
        String[] temp=new String[8];Int32 count=0,index=0;
        while(index<text.Length&&count<temp.Length){while(index<text.Length&&text[index]==' ')index++;if(index>=text.Length)break;Int32 start=index;while(index<text.Length&&text[index]!=' ')index++;temp[count++]=text.Substring(start,index-start);}
        String[] result=new String[count];for(Int32 i=0;i<count;i++)result[i]=temp[i];return result;
    }
}
