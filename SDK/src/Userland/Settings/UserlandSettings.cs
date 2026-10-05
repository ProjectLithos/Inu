using System;
namespace Inu.Userland.Settings;
/// <summary>Defines user-visible Inu settings categories independently of kernel implementation.</summary>
public readonly struct UserlandSettingCategory
{
    public UserlandSettingCategory(String name){Name=name;}
    public String Name { get; }
}
public static class UserlandSettings
{
    public static UserlandSettingCategory Console => new UserlandSettingCategory("Console");
    public static UserlandSettingCategory Keyboard => new UserlandSettingCategory("Keyboard");
    public static UserlandSettingCategory Display => new UserlandSettingCategory("Display");
}
