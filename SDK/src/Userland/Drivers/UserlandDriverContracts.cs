using System;
namespace Inu.Userland.Drivers;
/// <summary>Provides userland-visible driver identity without exposing privileged driver implementation.</summary>
public readonly struct UserlandDriverInfo
{
    public UserlandDriverInfo(String name,Boolean active){Name=name;Active=active;}
    public String Name { get; } public Boolean Active { get; }
}
