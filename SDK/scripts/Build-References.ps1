param(
    [Parameter(Mandatory=$true)][string]$SdkRoot,
    [Parameter(Mandatory=$true)][string]$DotNet,
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$SdkRoot = [IO.Path]::GetFullPath($SdkRoot)
$DotNet = [IO.Path]::GetFullPath($DotNet)
$sourceRoot = Join-Path $SdkRoot 'src'
$store = Join-Path $SdkRoot 'References\All'
$work = Join-Path $SdkRoot 'Artifacts\ReferenceBuild'
$legacyUsings = Join-Path $SdkRoot 'Usings'
if(Test-Path -LiteralPath $legacyUsings){Remove-Item -LiteralPath $legacyUsings -Recurse -Force}
New-Item -ItemType Directory -Force -Path $store,$work | Out-Null
Get-ChildItem -LiteralPath $store -File -Filter '*.dll' -ErrorAction SilentlyContinue | Remove-Item -Force

function Get-NodeText([System.Xml.XmlElement]$project,[string]$name){
    foreach($group in @($project.PropertyGroup)){
        $node=$group.SelectSingleNode($name)
        if($null -ne $node -and -not [string]::IsNullOrWhiteSpace([string]$node.InnerText)){return [string]$node.InnerText}
    }
    return ''
}
function Is-AutoReferenceLibrary([string]$projectName,[string]$noStdLib){
    if($noStdLib -notmatch '^(?i:true)$'){return $false}
    if($projectName -eq 'Inu.Freestanding.CoreLib'){return $false}
    if($projectName -match '^Inu\.(Kernel|Arch|Bus|Usb|Filesystem)\.'){return $false}
    if($projectName -in @('Inu.Runtime.NativeAot','Inu.Runtime.Conformance','Inu.ApplicationFormat','Inu.String')){return $false}
    return $true
}
function Get-PublicSymbols([string]$projectDirectory){
    $result=[System.Collections.Generic.List[object]]::new()
    $seen=[System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($file in @(Get-ChildItem -LiteralPath $projectDirectory -Recurse -File -Filter '*.cs' | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'})){
        $text=Get-Content -LiteralPath $file.FullName -Raw
        $nsMatch=[regex]::Match($text,'(?m)^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]')
        if(-not $nsMatch.Success){continue}
        $ns=$nsMatch.Groups[1].Value
        foreach($m in [regex]::Matches($text,'(?m)^\s*public\s+(?:(?:static|sealed|abstract|unsafe|readonly|partial|ref)\s+)*(?:class|struct|interface|enum|record)\s+([A-Za-z_][A-Za-z0-9_]*)')){
            $name=$m.Groups[1].Value
            $key="$ns`n$name"
            if($seen.Add($key)){$result.Add([pscustomobject]@{name=$name;namespace=$ns})}
        }
    }
    return $result
}

$assemblies=[System.Collections.Generic.List[object]]::new()
$symbols=[System.Collections.Generic.List[object]]::new()
$projects=@(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.csproj' | Sort-Object FullName)
foreach($project in $projects){
    [xml]$xml=Get-Content -LiteralPath $project.FullName -Raw
    $outputType=Get-NodeText $xml.Project 'OutputType'
    if($outputType -match '^(Exe|WinExe)$'){continue}
    $assemblyName=Get-NodeText $xml.Project 'AssemblyName'
    if([string]::IsNullOrWhiteSpace($assemblyName)){$assemblyName=[IO.Path]::GetFileNameWithoutExtension($project.Name)}
    $projectName=[IO.Path]::GetFileNameWithoutExtension($project.Name)
    $noStdLib=Get-NodeText $xml.Project 'NoStdLib'
    $autoReference=Is-AutoReferenceLibrary $projectName $noStdLib
    if(-not $autoReference){continue}
    $out=Join-Path $work $projectName
    if(Test-Path -LiteralPath $out){Remove-Item -LiteralPath $out -Recurse -Force}
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    Write-Host "[INFO] SDK Reference: $projectName"
    & $DotNet build $project.FullName --configuration $Configuration --output $out --nologo -p:PublishAot=false -p:SelfContained=false
    if($LASTEXITCODE -ne 0){throw "[FAIL] SDK Reference compilation failed: $($project.FullName)"}
    $dll=Join-Path $out ($assemblyName+'.dll')
    if(-not(Test-Path -LiteralPath $dll -PathType Leaf)){throw "[FAIL] SDK Reference DLL was not produced: $dll"}
    $destination=Join-Path $store ($projectName+'.dll')
    Copy-Item -LiteralPath $dll -Destination $destination -Force
    $assemblies.Add([pscustomobject]@{project=$projectName;assembly=$assemblyName;file=('All/'+$projectName+'.dll');autoReference=$autoReference})
    if($autoReference){
        foreach($symbol in @(Get-PublicSymbols $project.DirectoryName)){
            $symbols.Add([pscustomobject]@{name=$symbol.name;namespace=$symbol.namespace;project=$projectName;assembly=$assemblyName;file=('All/'+$projectName+'.dll')})
        }
    }
}
$manifestPath=Join-Path $SdkRoot 'References\manifest.json'
@{schemaVersion=2;configuration=$Configuration;assemblies=$assemblies;symbols=$symbols} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Host "[ OK ] SDK central references compiled: $($assemblies.Count) DLL(s), $($symbols.Count) auto-resolvable public symbol(s) -> $store"
