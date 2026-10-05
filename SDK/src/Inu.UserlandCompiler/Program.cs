using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Inu.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if(args.Length<2||!string.Equals(args[0],"compile",StringComparison.OrdinalIgnoreCase))
        return Fail("Usage: Inu.UserlandCompiler compile <InuProject.json> --dotnet <path> --ilc <path> --lld-link <path> --nasm <path> --native-root <path> --sdk-root <path> [--configuration Debug|Release]");
    if(!InuProject.TryLoad(args[1],out InuProject? project,out string error)||project is null)return Fail(error);
    string dotnet=GetOption(args,"--dotnet")??"dotnet";
    string ilc=GetOption(args,"--ilc")??returnFail("--ilc is required");
    string lld=GetOption(args,"--lld-link")??returnFail("--lld-link is required");
    string nasm=GetOption(args,"--nasm")??returnFail("--nasm is required");
    string nativeRoot=Path.GetFullPath(GetOption(args,"--native-root")??returnFail("--native-root is required"));
    string sdkRoot=Path.GetFullPath(GetOption(args,"--sdk-root")??returnFail("--sdk-root is required"));
    string configuration=GetOption(args,"--configuration")??"Release";
    string projectRoot=Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
    string osName=project.Name;
    string coderRoot=FindCoderUserlandRoot(projectRoot,osName);
    string outputRoot=Path.Combine(Path.GetFullPath(project.OutputDirectory),"UserlandApps");
    if(!Directory.Exists(coderRoot))
    {
        foreach(string folder in new[]{"Shell","Commands"}){string old=Path.Combine(outputRoot,folder);if(Directory.Exists(old))Directory.Delete(old,true);}
        Console.WriteLine("[INFO] No coder userland source selected; no ring-3 executables to build.");return 0;
    }
    Directory.CreateDirectory(outputRoot);

    string userEntrySource=Path.Combine(sdkRoot,"native","x64","UserEntry.asm");
    string userEntryObject=Path.Combine(nativeRoot,"UserEntry.obj");
    string userExceptionSource=Path.Combine(sdkRoot,"native","x64","ExceptionHandling.asm");
    string userExceptionObject=Path.Combine(nativeRoot,"UserExceptionHandling.obj");
    if(!File.Exists(userEntrySource)||!File.Exists(Path.Combine(nativeRoot,"Runtime.obj"))||!File.Exists(userExceptionSource))return Fail("Userland native runtime objects are missing. Build the Inu x64 native substrate first.");
    bool force=HasOption(args,"--force-rebuild");
    string cacheDirectory=Path.Combine(project.OutputDirectory,"StageCache","Userland");
    int rc=UserlandStageCache.Run("Userland entry assembly",cacheDirectory,[userEntrySource,nasm],[userEntryObject],["-f","win64"],force,()=>Run(nasm,["-f","win64",userEntrySource,"-o",userEntryObject],sdkRoot));
    if(rc!=0)return Fail($"NASM failed for UserEntry.asm with exit code {rc}.");
    rc=UserlandStageCache.Run("Userland exception assembly",cacheDirectory,[userExceptionSource,nasm],[userExceptionObject],["-f","win64","-DINU_USERLAND=1"],force,()=>Run(nasm,["-f","win64","-DINU_USERLAND=1",userExceptionSource,"-o",userExceptionObject],sdkRoot));
    if(rc!=0)return Fail($"NASM failed for ring-3 exception helpers with exit code {rc}.");
    List<string> sharedInputs=[typeof(UserlandStageCache).Assembly.Location,dotnet,Path.Combine(sdkRoot,"Directory.Build.props")];
    foreach(string library in new[]{"Inu.Freestanding.CoreLib","Inu.Userland.RuntimeSupport","Inu.Runtime.UserlandNativeAot","Inu.Userland.Runtime"})sharedInputs.AddRange(UserlandStageCache.ProjectInputs(Path.Combine(sdkRoot,"src",library,library+".csproj")));
    string sdkDirectory=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dotnet))!,"sdk");
    if(Directory.Exists(sdkDirectory))sharedInputs.Add(sdkDirectory);
    string sharedIdentity=UserlandStageCache.Fingerprint(sharedInputs,[configuration]);
    string ilcIdentity=UserlandStageCache.Fingerprint([Path.GetDirectoryName(Path.GetFullPath(ilc))!],[]);

    string shellSource=Path.Combine(coderRoot,"Shell.cs");
    if(File.Exists(shellSource))
    {
        string shellRuntime=Path.Combine(projectRoot,"Userland","Provided","Shell","InuShell.cs");
        if(!File.Exists(shellRuntime))shellRuntime=Path.Combine(sdkRoot,"src","Userland","Shell","InuShell.cs");
        string shellType=FindTypeWithMethod(shellSource,"Configure")??returnFail($"Could not find Shell.Configure in {shellSource}");
        if(!HasPromptMember(shellSource))return Fail($"Coder-owned shell source must define a Prompt string: {shellSource}");
        rc=CompileApp("Shell",[shellRuntime,shellSource],$"global::{shellType}.Configure(); return global::Inu.Userland.Shell.InuShell.Run(global::{shellType}.Prompt);",Path.Combine(outputRoot,"Shell","SHELL.EXE"),dotnet,ilc,lld,userEntryObject,userExceptionObject,nativeRoot,sdkRoot,configuration,cacheDirectory,sharedIdentity,ilcIdentity,force);
        if(rc!=0)return rc;
    }

    string commands=Path.Combine(coderRoot,"Commands");
    if(Directory.Exists(commands))
    {
        foreach(string source in Directory.GetFiles(commands,"*.cs",SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase))
        {
            string? type=FindTypeWithMethod(source,"Main");if(type is null)return Fail($"Command source has no public static Main: {source}");
            string body=HasStringArrayMain(source,type)?$"return global::{type}.Main(global::System.Array.Empty<string>());":$"return global::{type}.Main();"; // Raw argv/environment remain available through Inu.Userland.Runtime.UserlandArguments.
            List<string> sources=[source];
            // The generated GUI command is a normal executable and may include the coder GUI entry source.
            if(string.Equals(Path.GetFileNameWithoutExtension(source),"Gui",StringComparison.OrdinalIgnoreCase))
            {
                string gui=Path.Combine(coderRoot,"Gui.cs");if(File.Exists(gui))sources.Add(gui);
            }
            string name=Sanitize(Path.GetFileNameWithoutExtension(source));
            rc=CompileApp(name,sources,body,Path.Combine(outputRoot,"Commands",name.ToUpperInvariant()+".EXE"),dotnet,ilc,lld,userEntryObject,userExceptionObject,nativeRoot,sdkRoot,configuration,cacheDirectory,sharedIdentity,ilcIdentity,force);
            if(rc!=0)return rc;
        }
    }
    string commandsOutput=Path.Combine(outputRoot,"Commands");
    HashSet<string> expectedCommands=Directory.Exists(commands)?Directory.GetFiles(commands,"*.cs").Select(p=>Sanitize(Path.GetFileNameWithoutExtension(p)).ToUpperInvariant()+".EXE").ToHashSet(StringComparer.OrdinalIgnoreCase):new(StringComparer.OrdinalIgnoreCase);
    if(Directory.Exists(commandsOutput))foreach(string executable in Directory.GetFiles(commandsOutput,"*.EXE"))if(!expectedCommands.Contains(Path.GetFileName(executable)))File.Delete(executable);
    if(!File.Exists(shellSource)){string oldShell=Path.Combine(outputRoot,"Shell","SHELL.EXE");if(File.Exists(oldShell))File.Delete(oldShell);}
    Console.WriteLine($"[ OK ] Ring-3 userland executables built under: {outputRoot}");
    return 0;

    static string returnFail(string message)=>throw new ArgumentException(message);
}

static int CompileApp(string name,IReadOnlyList<string> sources,string callBody,string output,string dotnet,string ilc,string lld,string userEntry,string userException,string nativeRoot,string sdkRoot,string configuration,string cacheDirectory,string sharedIdentity,string ilcIdentity,bool force)
{
    string work=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(output)!)!,"Build",Sanitize(name));
    Directory.CreateDirectory(work);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    string entry=Path.Combine(work,"AppEntry.cs");
    File.WriteAllText(entry,$$"""
using System;
using System.Runtime;
using Inu.Runtime.NativeAot;
using Inu.Userland.Runtime;

internal static unsafe class InuUserApplicationEntry
{
    [RuntimeExport("InuUserManagedEntry")]
    private static Int32 Entry(UInt64 imageBase, UInt64 readyToRunHeader)
    {
        if(!NativeAotRuntime.Initialize()||imageBase==0UL||readyToRunHeader==0UL)return -100;
        IntPtr* modules=stackalloc IntPtr[1];modules[0]=(IntPtr)(void*)(nuint)readyToRunHeader;
        global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.InitializeModules((IntPtr)(void*)(nuint)imageBase,modules,1,null,0);
        if(!NativeAotExceptionRuntime.ConfigureImageBase(imageBase))return -101;
        Int32 code=Run();UserlandProcess.Exit(code);return code;
    }
    private static Int32 Run(){ {{callBody}} }
}
""");
    string project=Path.Combine(work,"App.csproj");
    StringBuilder xml=new();xml.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Library</OutputType><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable><DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences><NoStdLib>true</NoStdLib><NoConfig>true</NoConfig><RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo><GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute><DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences><EnableDefaultItems>false</EnableDefaultItems><AssemblyName>Inu.UserApp."+Sanitize(name)+"</AssemblyName></PropertyGroup><ItemGroup>");
    xml.AppendLine($"<Compile Include=\"{Esc(entry)}\" Link=\"AppEntry.cs\" />");int index=0;foreach(string source in sources)xml.AppendLine($"<Compile Include=\"{Esc(Path.GetFullPath(source))}\" Link=\"Source{index++}.cs\" />");
    foreach(string reference in new[]{"Inu.Freestanding.CoreLib","Inu.Userland.RuntimeSupport","Inu.Runtime.UserlandNativeAot","Inu.Userland.Runtime"})xml.AppendLine($"<ProjectReference Include=\"{Esc(Path.Combine(sdkRoot,"src",reference,reference+".csproj"))}\" />");
    xml.AppendLine("</ItemGroup></Project>");File.WriteAllText(project,xml.ToString());
    string managed=Path.Combine(work,"Managed");Directory.CreateDirectory(managed);
    string[] buildArgs=["build",project,"--configuration",configuration,"--output",managed,"--nologo","-p:PublishAot=false","-p:SelfContained=false"];
    int rc=UserlandStageCache.Run(name+" managed IL",cacheDirectory,sources.Concat([entry,project]),[managed],buildArgs.Concat([sharedIdentity]),force,()=>{if(Directory.Exists(managed))Directory.Delete(managed,true);Directory.CreateDirectory(managed);return Run(dotnet,buildArgs,work);});if(rc!=0)return Fail($"Managed userland build failed for {name} with exit code {rc}.");
    string[] dlls=Directory.GetFiles(managed,"*.dll").OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();if(dlls.Length==0)return Fail($"Managed userland build produced no assemblies for {name}.");
    string native=Path.Combine(work,name+".obj");List<string> ilcArgs=[..dlls,$"-o:{native}","--systemmodule","System.Private.CoreLib","--targetos:win","--targetarch:x64","--nativelib","--directpinvoke:*","--noscan","--root","Inu.Runtime.UserlandNativeAot","--root","Inu.Userland.Runtime","--scanreflection","--nopreinitstatics"];
    rc=UserlandStageCache.Run(name+" NativeAOT",cacheDirectory,dlls,[native],ilcArgs.Concat([ilcIdentity]),force,()=>Run(ilc,ilcArgs,sdkRoot));if(rc!=0)return Fail($"NativeAOT userland compilation failed for {name} with exit code {rc}.");
    List<string> link=["/nologo","/subsystem:native","/machine:x64","/nodefaultlib","/entry:InuUserEntry","/base:0x0000400000100000","/fixed","/merge:.modules=.rdata","/errorlimit:64",$"/out:{output}",userEntry,Path.Combine(nativeRoot,"Runtime.obj"),userException,native];
    rc=UserlandStageCache.Run(name+" native link",cacheDirectory,[lld,userEntry,userException,native,Path.Combine(nativeRoot,"Runtime.obj")],[output],link,force,()=>Run(lld,link,work));if(rc!=0)return Fail($"Userland link failed for {name} with exit code {rc}.");if(!File.Exists(output))return Fail($"Userland linker did not produce {output}");Console.WriteLine($"[ OK ] Ring-3 executable: {output}");return 0;
}

static string FindCoderUserlandRoot(string projectRoot,string osName)
{
    string exact=Path.Combine(projectRoot,"Userland",osName);if(Directory.Exists(exact))return exact;string root=Path.Combine(projectRoot,"Userland");if(!Directory.Exists(root))return exact;return Directory.GetDirectories(root).FirstOrDefault(d=>!string.Equals(Path.GetFileName(d),"Provided",StringComparison.OrdinalIgnoreCase))??exact;
}
static string? FindTypeWithMethod(string file,string method)
{
    string s=File.ReadAllText(file);Match ns=Regex.Match(s,@"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]");MatchCollection types=Regex.Matches(s,@"\b(?:public\s+)?static\s+(?:(?:unsafe|partial)\s+)*class\s+([A-Za-z_][A-Za-z0-9_]*)");foreach(Match type in types){int pos=type.Index+type.Length;if(Regex.IsMatch(s[pos..],$@"\b{Regex.Escape(method)}\s*\("))return (ns.Success?ns.Groups[1].Value+".":"")+type.Groups[1].Value;}return null;
}
static bool HasStringArrayMain(string file,string type)=>Regex.IsMatch(File.ReadAllText(file),@"\bMain\s*\(\s*(?:string|String)\s*\[\s*\]\s+[A-Za-z_]");
static bool HasPromptMember(string file)=>Regex.IsMatch(File.ReadAllText(file),@"\b(?:const\s+)?(?:string|String)\s+Prompt\b");
static string Sanitize(string value)=>Regex.Replace(value,@"[^A-Za-z0-9_.-]","_");
static string Esc(string value)=>value.Replace("&","&amp;").Replace("\"","&quot;");
static string? GetOption(string[] args,string name){for(int i=0;i+1<args.Length;i++)if(string.Equals(args[i],name,StringComparison.OrdinalIgnoreCase))return args[i+1];return null;}
static int Run(string file,IEnumerable<string> args,string cwd){ProcessStartInfo psi=new(file){WorkingDirectory=cwd,UseShellExecute=false};foreach(string arg in args)psi.ArgumentList.Add(arg);using Process p=Process.Start(psi)!;p.WaitForExit();return p.ExitCode;}
static int Fail(string message){Console.Error.WriteLine("[FAIL] "+message);return 1;}

static bool HasOption(string[] args,string name)=>args.Any(a=>string.Equals(a,name,StringComparison.OrdinalIgnoreCase));
