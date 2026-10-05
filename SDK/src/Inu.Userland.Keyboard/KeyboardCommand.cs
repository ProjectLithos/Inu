using System;
namespace Inu.Userland.Keyboard;
/// <summary>Compatibility facade for the canonical System/Commands keyboard command.</summary>
public static class KeyboardCommand
{
 public const String KeyboardLayoutMessage=Inu.Userland.Commands.KeyboardCommand.KeyboardLayoutMessage;
 public static Boolean TryParseLayout(String value,out UInt32 layout)=>Inu.Userland.Commands.KeyboardCommand.TryParseLayout(value,out layout);
 public static String GetLayoutName(UInt32 layout)=>Inu.Userland.Commands.KeyboardCommand.GetLayoutName(layout);
 public static String GetUsage()=>Inu.Userland.Commands.KeyboardCommand.GetUsage();
 public static String GetInstalledLayouts()=>Inu.Userland.Commands.KeyboardCommand.GetInstalledLayouts();
 public static Int32 Run(String[] args,Func<String,Int64> get,Func<String,UInt64,Int64> set,Action<String> write)=>Inu.Userland.Commands.KeyboardCommand.Run(args,get,set,write);
}
