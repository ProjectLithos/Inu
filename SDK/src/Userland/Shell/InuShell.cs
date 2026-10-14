using System;

namespace Inu.Userland.Shell;

/// <summary>
/// Provides only the lifetime runner for a coder-owned shell. Shell policy, prompt,
/// input, parsing, command resolution and command execution belong in Shell.cs.
/// </summary>
public static unsafe class InuShell
{
    private static Boolean _configured;

    /// <summary>
    /// Configures the coder-owned shell once, then repeatedly asks Shell.Run to perform
    /// one shell iteration for the lifetime of the ring-3 shell process.
    /// </summary>
    public static void Run(delegate* managed<void> configure, delegate* managed<void> run)
    {
        if (configure == null) throw new ArgumentNullException("configure");
        if (run == null) throw new ArgumentNullException("run");

        if (!_configured)
        {
            configure();
            _configured = true;
        }

        while (true)
            run();
    }
}
