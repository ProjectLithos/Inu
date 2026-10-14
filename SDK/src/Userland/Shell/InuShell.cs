using System;

namespace Inu.Userland.Shell;

/// <summary>
/// Provides only the lifetime runner for a coder-owned shell. Shell policy, prompt,
/// input, parsing, command resolution and command execution belong in Shell.cs.
/// </summary>
public static class InuShell
{
    /// <summary>
    /// Configures the coder-owned shell once, then repeatedly asks Shell.Run to perform
    /// one shell iteration for the lifetime of the ring-3 shell process.
    /// </summary>
    public static void Run(Action configure, Action run)
    {
        if (configure is null) throw new ArgumentNullException("configure");
        if (run is null) throw new ArgumentNullException("run");

        configure();
        while (true)
            run();
    }
}
