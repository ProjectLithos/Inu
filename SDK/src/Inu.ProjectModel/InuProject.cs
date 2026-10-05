using System.Text.Json;

namespace Inu.ProjectModel;

public sealed record InuProject(
    string Name,
    string ProjectFile,
    string TargetArchitecture,
    string BootProtocol,
    string KernelEntry,
    string RuntimePack,
    string OutputDirectory,
    string KernelModel = "Monolithic",
    string ConfigurationFile = "Inu.Configuration.json",
    string[]? WorkAreas = null,
    int QemuCpuCount = 4)
{
    public static bool TryLoad(string path, out InuProject? project, out string error)
    {
        project=null;error=string.Empty;
        if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("Project path is required.",nameof(path));
        string manifestPath=Path.GetFullPath(path);
        if(!File.Exists(manifestPath)){error=$"Project manifest not found: {manifestPath}";return false;}
        try
        {
            project=JsonSerializer.Deserialize<InuProject>(File.ReadAllText(manifestPath));
            if(project is null){error="Project manifest was empty.";return false;}
            string root=Path.GetDirectoryName(manifestPath)??Environment.CurrentDirectory;
            project=project with
            {
                ProjectFile=Resolve(root,project.ProjectFile),
                OutputDirectory=Resolve(root,project.OutputDirectory),
                ConfigurationFile=string.IsNullOrWhiteSpace(project.ConfigurationFile)?Path.Combine(root,"Inu.Configuration.json"):Resolve(root,project.ConfigurationFile),
                WorkAreas=project.WorkAreas??Array.Empty<string>()
            };
            return project.Validate(out error);
        }
        catch(Exception exception) when(exception is IOException or JsonException){error=exception.Message;return false;}
    }
    public bool Validate(out string error)
    {
        if(string.IsNullOrWhiteSpace(Name)){error="Name is required.";return false;}
        if(!File.Exists(ProjectFile)){error=$"Kernel project was not found: {ProjectFile}";return false;}
        if(!IsKnownArchitecture(TargetArchitecture)){error=$"Unknown target architecture '{TargetArchitecture}'. Select x64, arm64 or riscv64.";return false;}
        if(!string.Equals(BootProtocol,"Uefi",StringComparison.OrdinalIgnoreCase)){error="Inu currently supports UEFI boot.";return false;}
        if(!string.Equals(KernelEntry,"KMain",StringComparison.Ordinal)){error="KernelEntry must be KMain.";return false;}
        if(!IsKnownKernelModel(KernelModel)){error=$"Unknown kernel model '{KernelModel}'. Select Monolithic, Microkernel or Hybrid.";return false;}
        if(QemuCpuCount<1||QemuCpuCount>256){error="QemuCpuCount must be between 1 and 256.";return false;}
        error=string.Empty;return true;
    }
    public static bool IsKnownArchitecture(string value)=>string.Equals(value,"x64",StringComparison.OrdinalIgnoreCase)||string.Equals(value,"arm64",StringComparison.OrdinalIgnoreCase)||string.Equals(value,"riscv64",StringComparison.OrdinalIgnoreCase);
    public static bool IsKnownKernelModel(string value)=>string.Equals(value,"Monolithic",StringComparison.OrdinalIgnoreCase)||string.Equals(value,"Microkernel",StringComparison.OrdinalIgnoreCase)||string.Equals(value,"Hybrid",StringComparison.OrdinalIgnoreCase);
    private static string Resolve(string root,string value)=>Path.IsPathRooted(value)?Path.GetFullPath(value):Path.GetFullPath(Path.Combine(root,value));
}
