using System;
namespace Inu.Kernel.Console;
public interface ISystemAssetBundleContext : IBootContext
{ UInt64 GetSystemAssetBundleAddress(); UInt64 GetSystemAssetBundleLength(); Boolean HasSystemAssetBundle(); }
