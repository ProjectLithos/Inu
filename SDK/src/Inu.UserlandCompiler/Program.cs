using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Inu.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if(args.Length<2||!string.Equals(args[0],"compile",StringComparison.OrdinalIgnoreCase))
        throw Fail("Usage: Inu.UserlandCompiler compile <InuProject.json> --dotnet <path> --ilc <path> --lld-link <path> --nasm <path> --native-root <path> --sdk-root <path> [--configuration Debug|Release]");
    if(!InuProject.TryLoad(args[1],out InuProject? project,out string error)||project is null)throw Fail(error);
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
    Console.WriteLine($"[INFO] Coder userland source: {coderRoot}");
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
    if(!File.Exists(userEntrySource)||!File.Exists(Path.Combine(nativeRoot,"Runtime.obj"))||!File.Exists(userExceptionSource))throw Fail("Userland native runtime objects are missing. Build the Inu x64 native substrate first.");
    bool force=HasOption(args,"--force-rebuild");
    string cacheDirectory=Path.Combine(project.OutputDirectory,"StageCache","Userland");
    UserlandStageCache.Run("Userland entry assembly",cacheDirectory,[userEntrySource,nasm],[userEntryObject],["-f","win64"],force,()=>Run(nasm,["-f","win64",userEntrySource,"-o",userEntryObject],sdkRoot));
    UserlandStageCache.Run("Userland exception assembly",cacheDirectory,[userExceptionSource,nasm],[userExceptionObject],["-f","win64","-DINU_USERLAND=1"],force,()=>Run(nasm,["-f","win64","-DINU_USERLAND=1",userExceptionSource,"-o",userExceptionObject],sdkRoot));
    string directoryBuildProps=Path.Combine(sdkRoot,"Directory.Build.props");
    // Cache against the compiler source/project rather than this freshly rebuilt host DLL.
    // Rebuilding Inu.UserlandCompiler can produce a different binary identity even when its
    // source is unchanged; using that DLL as an input caused every unchanged command to miss
    // the cache on the next Kath run.
    List<string> sharedInputs=[dotnet];
    sharedInputs.AddRange(UserlandStageCache.ProjectInputs(Path.Combine(sdkRoot,"src","Inu.UserlandCompiler","Inu.UserlandCompiler.csproj")));
    foreach(string library in new[]{"Inu.Freestanding.CoreLib","Inu.Userland.RuntimeSupport","Inu.Runtime.UserlandNativeAot","Inu.Userland.Runtime"})sharedInputs.AddRange(UserlandStageCache.ProjectInputs(Path.Combine(sdkRoot,"src",library,library+".csproj")));
    string sharedIdentity=UserlandStageCache.Fingerprint(sharedInputs,[configuration,CompilationRelevantBuildProps(directoryBuildProps)]);
    string ilcIdentity=UserlandStageCache.Fingerprint([Path.GetDirectoryName(Path.GetFullPath(ilc))!],[]);

    string shellSource=Path.Combine(coderRoot,"Shell.cs");
    string commands=Path.Combine(coderRoot,"Commands");
    int commandCount=Directory.Exists(commands)?Directory.GetFiles(commands,"*.cs",SearchOption.TopDirectoryOnly).Length:0;
    int executableCount=(File.Exists(shellSource)?1:0)+commandCount;
    Console.WriteLine($"[INFO] Ring-3 build is checking {executableCount} userland executable(s) ({(File.Exists(shellSource)?"Shell + ":String.Empty)}{commandCount} command(s)); only stale inputs will be recompiled.");
    if(File.Exists(shellSource))
    {
        ValidateLiteralPathPolicy(shellSource);
        string shellType=FindTypeWithMethod(shellSource,"Configure")??returnFail($"Could not find Shell.Configure in {shellSource}");
        string? runType=FindTypeWithMethod(shellSource,"Run");
        if(runType is null||!string.Equals(runType,shellType,StringComparison.Ordinal))throw Fail($"Coder-owned shell source must define public static Run() on the same type as Configure(): {shellSource}");
        string shellRuntime=Path.Combine(projectRoot,"Userland","Provided","Shell","InuShell.cs");
        if(!File.Exists(shellRuntime))shellRuntime=Path.Combine(sdkRoot,"src","Userland","Shell","InuShell.cs");
        CompileApp("Shell",[shellRuntime,shellSource],$"global::Inu.Userland.Shell.InuShell.Run(&global::{shellType}.Configure, &global::{shellType}.Run); return 0;",true,Path.Combine(outputRoot,"Shell","SHELL.EXE"),dotnet,ilc,lld,userEntryObject,userExceptionObject,nativeRoot,sdkRoot,configuration,cacheDirectory,sharedIdentity,ilcIdentity,force);
    }

    if(Directory.Exists(commands))
    {
        foreach(string source in Directory.GetFiles(commands,"*.cs",SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase))
        {
            ValidateLiteralPathPolicy(source);
            string? type=FindTypeWithMethod(source,"Main");if(type is null)throw Fail($"Command source has no public static Main: {source}");
            Boolean withArguments=HasStringArrayMain(source,type),returnsVoid=MainReturnsVoid(source);
            string invocation=withArguments?$"global::{type}.Main(global::Inu.Userland.Runtime.CommandLine.GetArguments())":$"global::{type}.Main()";
            string body=returnsVoid?$"{invocation}; return 0;":$"return {invocation};";
            List<string> sources=[source];
            // The generated GUI command is a normal executable and may include the coder GUI entry source.
            if(string.Equals(Path.GetFileNameWithoutExtension(source),"Gui",StringComparison.OrdinalIgnoreCase))
            {
                string gui=Path.Combine(coderRoot,"Gui.cs");if(File.Exists(gui)){ValidateLiteralPathPolicy(gui);sources.Add(gui);}
            }
            string name=Sanitize(Path.GetFileNameWithoutExtension(source));
            CompileApp(name,sources,body,false,Path.Combine(outputRoot,"Commands",name.ToUpperInvariant()+".EXE"),dotnet,ilc,lld,userEntryObject,userExceptionObject,nativeRoot,sdkRoot,configuration,cacheDirectory,sharedIdentity,ilcIdentity,force);
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

static void ValidateLiteralPathPolicy(string sourcePath)
{
    string text=File.ReadAllText(sourcePath);
    MatchCollection separatorMatches=Regex.Matches(text,@"\bFileSystemPaths\.SetPathSeparator\s*\(\s*'(\\.|[^'\\])'\s*\)");
    List<(int Index,char Separator)> separators=[];
    foreach(Match match in separatorMatches)
    {
        string token=match.Groups[1].Value;
        char value=token==@"\\"?'\\':token==@"\'"?'\'':token.Length==1?token[0]:'\0';
        if(value!='\0')separators.Add((match.Index,value));
    }

    bool valid=true;
    foreach(Match call in Regex.Matches(text,@"\bFileSystemPaths\.SetCommandsPaths?\s*\("))
    {
        char separator='\0';
        foreach((int index,char candidate) in separators){if(index>=call.Index)break;separator=candidate;}
        if(separator=='\0')continue;
        int end=text.IndexOf(';',call.Index);
        if(end<0)end=text.Length;
        bool inString=false,verbatim=false;
        for(int i=call.Index;i<end;i++)
        {
            char c=text[i];
            if(!inString)
            {
                if(c=='@'&&i+1<end&&text[i+1]=='\"'){inString=true;verbatim=true;i++;continue;}
                if(c=='\"'){inString=true;verbatim=false;continue;}
                continue;
            }
            if(verbatim)
            {
                if(c=='\"')
                {
                    if(i+1<end&&text[i+1]=='\"'){i++;continue;}
                    inString=false;verbatim=false;continue;
                }
            }
            else
            {
                if(c=='\\'&&i+1<end)
                {
                    if(text[i+1]=='\\')
                    {
                        if(IsSeparatorLike('\\')&&separator!='\\')ReportInvalidSeparator(sourcePath,text,i,separator,'\\',ref valid);
                        i++;continue;
                    }
                    i++;continue;
                }
                if(c=='\"'){inString=false;continue;}
            }
            if(IsSeparatorLike(c)&&c!=separator)ReportInvalidSeparator(sourcePath,text,i,separator,c,ref valid);
        }
    }
    if(!valid) throw new InvalidOperationException($"Literal path policy validation failed: {sourcePath}");
}


static bool IsSeparatorLike(char value)
{
    if(value==0||value>0x7F)return false;
    if((value>='A'&&value<='Z')||(value>='a'&&value<='z')||(value>='0'&&value<='9'))return false;
    return value!='.'&&value!='_'&&value!='-'&&value!=' ';
}

static void ReportInvalidSeparator(string sourcePath,string text,int index,char separator,char invalid,ref bool valid)
{
    (int line,int column)=SourceLocation(text,index);
    string shown=invalid=='\\'?@"\\":invalid.ToString();
    Console.Error.WriteLine($"{sourcePath}({line},{column}): error INU1007: Path uses '{shown}' as a separator, but FileSystemPaths.SetPathSeparator('{separator}') selected '{separator}' as the OS path separator.");
    valid=false;
}

static (int Line,int Column) SourceLocation(string text,int index)
{
    int line=1,column=1;
    for(int i=0;i<index&&i<text.Length;i++){if(text[i]=='\n'){line++;column=1;}else column++;}
    return (line,column);
}

static void CompileApp(string name,IReadOnlyList<string> sources,string callBody,bool shellLifecycle,string output,string dotnet,string ilc,string lld,string userEntry,string userException,string nativeRoot,string sdkRoot,string configuration,string cacheDirectory,string sharedIdentity,string ilcIdentity,bool force)
{
    string work=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(output)!)!,"Build",Sanitize(name));
    Directory.CreateDirectory(work);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    string entry=Path.Combine(sdkRoot,"templates","Userland","UserApplicationEntry.cs");
    if(!File.Exists(entry))throw Fail($"Shared userland entry wrapper is missing: {entry}");
    string binding=Path.Combine(work,"ApplicationEntryBinding.cs");
    File.WriteAllText(binding,$$"""
using System;
using Inu.Userland.Runtime;

internal static class InuUserApplicationBinding
{
    internal static unsafe Int32 Invoke()
    {
        {{callBody}}
    }
}
""");
    string project=Path.Combine(work,"App.csproj");
    StringBuilder xml=new();xml.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Library</OutputType><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable><DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences><NoStdLib>true</NoStdLib><NoConfig>true</NoConfig><RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion><GenerateAssemblyInfo>false</GenerateAssemblyInfo><GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute><DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences><EnableDefaultItems>false</EnableDefaultItems><AssemblyName>Inu.UserApp."+Sanitize(name)+"</AssemblyName></PropertyGroup><ItemGroup>");
    xml.AppendLine($"<Compile Include=\"{Esc(entry)}\" Link=\"Runtime/UserApplicationEntry.cs\" />");xml.AppendLine($"<Compile Include=\"{Esc(binding)}\" Link=\"Generated/ApplicationEntryBinding.cs\" />");int index=0;foreach(string source in sources)xml.AppendLine($"<Compile Include=\"{Esc(Path.GetFullPath(source))}\" Link=\"Source{index++}.cs\" />");
    foreach(string reference in new[]{"Inu.Freestanding.CoreLib","Inu.Userland.RuntimeSupport","Inu.Runtime.UserlandNativeAot","Inu.Userland.Runtime"})xml.AppendLine($"<ProjectReference Include=\"{Esc(Path.Combine(sdkRoot,"src",reference,reference+".csproj"))}\" />");
    xml.AppendLine("</ItemGroup></Project>");File.WriteAllText(project,xml.ToString());
    string managed=Path.Combine(work,"Managed");Directory.CreateDirectory(managed);
    string[] buildArgs=["build",project,"--configuration",configuration,"--output",managed,"--nologo","-p:PublishAot=false","-p:SelfContained=false"];
    UserlandStageCache.Run(name+" managed IL",cacheDirectory,sources.Concat([entry,binding,project]),[managed],buildArgs.Concat([sharedIdentity]),force,()=>{if(Directory.Exists(managed))Directory.Delete(managed,true);Directory.CreateDirectory(managed);return Run(dotnet,buildArgs,work);});
    string[] dlls=Directory.GetFiles(managed,"*.dll").OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();if(dlls.Length==0)throw Fail($"Managed userland build produced no assemblies for {name}.");
    string native=Path.Combine(work,name+".obj");List<string> ilcArgs=[..dlls,$"-o:{native}","--systemmodule","System.Private.CoreLib","--targetos:win","--targetarch:x64","--nativelib","--directpinvoke:*","--noscan","--root","Inu.Runtime.UserlandNativeAot","--root","Inu.Userland.Runtime","--scanreflection","--nopreinitstatics"];
    UserlandStageCache.Run(name+" NativeAOT",cacheDirectory,dlls,[native],ilcArgs.Concat([ilcIdentity]),force,()=>Run(ilc,ilcArgs,sdkRoot));
    List<string> link=["/nologo","/subsystem:native","/machine:x64","/nodefaultlib","/entry:InuUserEntry","/base:0x0000400000100000","/fixed","/merge:.modules=.rdata","/errorlimit:64",$"/out:{output}",userEntry,Path.Combine(nativeRoot,"Runtime.obj"),userException,native];
    UserlandStageCache.Run(name+" native link",cacheDirectory,[lld,userEntry,userException,native,Path.Combine(nativeRoot,"Runtime.obj")],[output],link,force,()=>Run(lld,link,work));if(!File.Exists(output))throw Fail($"Userland linker did not produce {output}");Console.WriteLine($"[ OK ] Ring-3 executable: {output}");return;
}

static string FindCoderUserlandRoot(string projectRoot,string osName)
{
    string root=Path.Combine(projectRoot,"Userland");
    string safeName=SafeProjectSegment(osName);
    string canonical=Path.Combine(root,safeName);
    if(Directory.Exists(canonical))return canonical;

    // Compatibility with projects created before Kath and Inu shared the same
    // filesystem-safe OS-name rule. Never silently select an arbitrary userland
    // tree: doing so can compile stale/empty commands from another generated OS.
    string legacy=Path.Combine(root,osName);
    if(!string.Equals(legacy,canonical,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(legacy))return legacy;
    if(!Directory.Exists(root))return canonical;

    string[] candidates=Directory.GetDirectories(root)
        .Where(d=>!string.Equals(Path.GetFileName(d),"Provided",StringComparison.OrdinalIgnoreCase))
        .ToArray();
    if(candidates.Length==0)return canonical;
    if(candidates.Length==1)return candidates[0];
    throw new InvalidOperationException($"Could not select coder userland source for OS '{osName}'. Expected '{canonical}', but found multiple OS userland directories: {string.Join(", ",candidates.Select(Path.GetFileName))}");
}
static string SafeProjectSegment(string value)
{
    char[] chars=value.Select(c=>char.IsLetterOrDigit(c)||c=='_'?c:'_').ToArray();
    return chars.Length==0?"OS":new string(chars);
}
static string? FindTypeWithMethod(string file,string method)
{
    string s=File.ReadAllText(file);Match ns=Regex.Match(s,@"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]");MatchCollection types=Regex.Matches(s,@"\b(?:public\s+)?static\s+(?:(?:unsafe|partial)\s+)*class\s+([A-Za-z_][A-Za-z0-9_]*)");foreach(Match type in types){int pos=type.Index+type.Length;if(Regex.IsMatch(s[pos..],$@"\b{Regex.Escape(method)}\s*\("))return (ns.Success?ns.Groups[1].Value+".":"")+type.Groups[1].Value;}return null;
}
static bool HasStringArrayMain(string file,string type)=>Regex.IsMatch(File.ReadAllText(file),@"\bMain\s*\(\s*(?:string|String)\s*\[\s*\]\s+[A-Za-z_]");
static bool MainReturnsVoid(string file)=>Regex.IsMatch(File.ReadAllText(file),@"\b(?:public\s+)?static\s+(?:void|Void)\s+Main\s*\(");
static string Sanitize(string value)=>Regex.Replace(value,@"[^A-Za-z0-9_.-]","_");
static string Esc(string value)=>value.Replace("&","&amp;").Replace("\"","&quot;");

static string CompilationRelevantBuildProps(string path)
{
    if(!File.Exists(path))return String.Empty;
    string text=File.ReadAllText(path);
    // Product/release identity does not alter generated userland IL or native code.
    // Keep all other Directory.Build.props content in the cache identity so a real
    // compiler/build-policy change still invalidates every affected application.
    foreach(string element in new[]{"Version","AssemblyVersion","FileVersion","PackageVersion"})
        text=Regex.Replace(text,$@"(<{element}>)[^<]*(</{element}>)","$1<release-version>$2",RegexOptions.CultureInvariant);
    return text;
}
static string? GetOption(string[] args,string name){for(int i=0;i+1<args.Length;i++)if(string.Equals(args[i],name,StringComparison.OrdinalIgnoreCase))return args[i+1];return null;}
static int Run(string file,IEnumerable<string> args,string cwd){ProcessStartInfo psi=new(file){WorkingDirectory=cwd,UseShellExecute=false};foreach(string arg in args)psi.ArgumentList.Add(arg);using Process p=Process.Start(psi)!;p.WaitForExit();return p.ExitCode;}
static Exception Fail(string message) => new InvalidOperationException(message);

static bool HasOption(string[] args,string name)=>args.Any(a=>string.Equals(a,name,StringComparison.OrdinalIgnoreCase));
