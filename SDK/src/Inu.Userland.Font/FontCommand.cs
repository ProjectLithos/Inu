using System;
namespace Inu.Userland.Font;
/// <summary>Compatibility facade for the canonical System/Commands font command.</summary>
public static class FontCommand
{
 public const String FontPresetMessage=Inu.Userland.Commands.FontCommand.FontPresetMessage;
 public static String GetUsage()=>Inu.Userland.Commands.FontCommand.GetUsage();
 public static String GetInstalledPresets()=>Inu.Userland.Commands.FontCommand.GetInstalledPresets();
 public static Int32 Run(String[] args,Func<String,Int64> get,Func<String,UInt64,Int64> set,Action<String> write)=>Inu.Userland.Commands.FontCommand.Run(args,get,set,write);
 public static Boolean TryParsePreset(String value,out UInt32 preset)=>Inu.Userland.Commands.FontCommand.TryParsePreset(value,out preset);
}
