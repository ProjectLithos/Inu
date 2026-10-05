using System;
namespace Inu.Kernel.Console;
public interface IBootstrapPageTableWorkspaceContext : IBootContext
{ UInt64 GetBootstrapPageTableWorkspaceAddress(); UInt64 GetBootstrapPageTableWorkspacePages(); }
