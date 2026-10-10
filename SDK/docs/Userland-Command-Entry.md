# Userland command entry model

Inu commands are ordinary C# programs. Stock commands use the stable `Inu.Userland.Commands` namespace and are not coupled to the generated OS name.

A command may expose any of the normal entry shapes:

```csharp
public static int Main()
public static void Main()
public static int Main(string[] args)
public static void Main(string[] args)
```

`Inu.UserlandCompiler` discovers the selected `Main` method and generates only a tiny build-time binding under the build artifacts. Every command and ring-3 application uses the same SDK-owned `templates/Userland/UserApplicationEntry.cs` NativeAOT process-entry wrapper. The wrapper initializes the freestanding NativeAOT runtime, configures the image base, calls the generated binding, and exits the userland process. It is runtime plumbing and is never copied into coder-owned command source.

For `Main(string[] args)`, `Inu.Userland.Runtime.CommandLine.GetArguments()` converts the process's raw argument payload into an ordinary C# string array. Double quotes group whitespace into one argument and are removed from the returned value. The raw form remains available through `CommandLine.GetRawArguments()` when an application explicitly needs it.
