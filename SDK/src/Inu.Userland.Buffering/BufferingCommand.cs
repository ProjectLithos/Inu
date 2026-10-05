using System;
namespace Inu.Userland.Buffering;
/// <summary>Compatibility facade for the canonical System/Commands buffering command.</summary>
public static class BufferingCommand
{
 public const String BufferingPresetMessage=Inu.Userland.Commands.BufferingCommand.BufferingPresetMessage;
 public static String GetUsage()=>Inu.Userland.Commands.BufferingCommand.GetUsage();
 public static String GetInstalledPresets()=>Inu.Userland.Commands.BufferingCommand.GetInstalledPresets();
 public static Int32 Run(String[] args,Func<String,Int64> get,Func<String,UInt64,Int64> set,Action<String> write)=>Inu.Userland.Commands.BufferingCommand.Run(args,get,set,write);
 public static Boolean TryParsePreset(String value,out UInt32 preset)=>Inu.Userland.Commands.BufferingCommand.TryParsePreset(value,out preset);
}
