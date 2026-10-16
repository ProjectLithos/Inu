param(
    [Parameter(Mandatory=$true)][string]$SdkRoot,
    [Parameter(Mandatory=$true)][string]$DotNet,
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'

# Shared conformance files are linked into the host-side reference project. Validate them
# explicitly so an incomplete incremental tree fails with a direct packaging diagnostic.
$sharedConformanceRoot = Join-Path $SdkRoot 'src\Inu.Runtime.Conformance\Shared'
foreach ($sharedName in @('ArrayEnumerationConformance.cs','ListConformance.cs','CoreCollectionsConformance.cs','TupleConformance.cs','LinqConformance.cs')) {
    $sharedPath = Join-Path $sharedConformanceRoot $sharedName
    if (-not (Test-Path -LiteralPath $sharedPath -PathType Leaf)) {
        throw "[FAIL] SDK shared conformance source is missing: $sharedPath. Apply the matching FullSource or self-contained ChangedFiles release."
    }
}
Set-StrictMode -Version Latest
$SdkRoot = [IO.Path]::GetFullPath($SdkRoot)
$DotNet = [IO.Path]::GetFullPath($DotNet)
$sourceRoot = Join-Path $SdkRoot 'src'
$store = Join-Path $SdkRoot 'References\All'
$work = Join-Path $SdkRoot 'Artifacts\ReferenceBuild'
$legacyUsings = Join-Path $SdkRoot 'Usings'
if(Test-Path -LiteralPath $legacyUsings){Remove-Item -LiteralPath $legacyUsings -Recurse -Force}
New-Item -ItemType Directory -Force -Path $store,$work | Out-Null
$cacheRoot = Join-Path $work '.cache'
New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null

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

function Get-ProjectInputFiles([string]$projectPath,[System.Collections.Generic.HashSet[string]]$seenProjects){
    $projectPath=[IO.Path]::GetFullPath($projectPath)
    $result=[System.Collections.Generic.List[string]]::new()
    if(-not $seenProjects.Add($projectPath)){return @()}
    $result.Add($projectPath)
    $projectDir=Split-Path -Parent $projectPath
    foreach($file in @(Get-ChildItem -LiteralPath $projectDir -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'})){
        $result.Add($file.FullName)
    }
    [xml]$xml=Get-Content -LiteralPath $projectPath -Raw
    foreach($compile in @($xml.SelectNodes('//Compile[@Include]'))){
        $include=[string]$compile.Include
        if([string]::IsNullOrWhiteSpace($include)){continue}
        $candidate=Join-Path $projectDir $include
        foreach($resolved in @(Get-Item -Path $candidate -ErrorAction SilentlyContinue | Where-Object {-not $_.PSIsContainer})){$result.Add($resolved.FullName)}
    }
    foreach($reference in @($xml.SelectNodes('//ProjectReference[@Include]'))){
        $include=[string]$reference.Include
        if([string]::IsNullOrWhiteSpace($include)){continue}
        $referenced=[IO.Path]::GetFullPath((Join-Path $projectDir $include))
        foreach($input in @(Get-ProjectInputFiles $referenced $seenProjects)){$result.Add($input)}
    }
    return @($result)
}
function Get-CompositeHash([string[]]$files,[string[]]$values){
    $unique=@($files | Where-Object {Test-Path -LiteralPath $_ -PathType Leaf} | ForEach-Object {[IO.Path]::GetFullPath($_)} | Sort-Object -Unique)
    $builder=[Text.StringBuilder]::new()
    foreach($value in $values){[void]$builder.Append('VALUE|').Append($value).Append("`n")}
    foreach($file in $unique){
        $hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
        [void]$builder.Append('FILE|').Append($file).Append('|').Append($hash).Append("`n")
    }
    $sha=[Security.Cryptography.SHA256]::Create()
    try{$bytes=[Text.Encoding]::UTF8.GetBytes($builder.ToString());$digest=$sha.ComputeHash($bytes)}finally{$sha.Dispose()}
    return [pscustomobject]@{Hash=(([BitConverter]::ToString($digest)).Replace('-','').ToLowerInvariant());Count=$unique.Count}
}
function Get-FileSha256([string]$path){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}

$dotnetHash=Get-FileSha256 $DotNet
$globalInputs=[System.Collections.Generic.List[string]]::new()
foreach($name in @('Directory.Build.props','Directory.Build.targets')){
    $candidate=Join-Path $SdkRoot $name
    if(Test-Path -LiteralPath $candidate -PathType Leaf){$globalInputs.Add($candidate)}
}
$assemblies=[System.Collections.Generic.List[object]]::new()
$symbols=[System.Collections.Generic.List[object]]::new()
$expectedDestinations=[System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
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
    $destination=Join-Path $store ($projectName+'.dll')
    [void]$expectedDestinations.Add([IO.Path]::GetFullPath($destination))
    $seen=[System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $inputFiles=[System.Collections.Generic.List[string]]::new()
    foreach($input in @(Get-ProjectInputFiles $project.FullName $seen)){$inputFiles.Add($input)}
    foreach($input in $globalInputs){$inputFiles.Add($input)}
    $fingerprint=Get-CompositeHash @($inputFiles) @('SdkReferenceCache:1',$projectName,$assemblyName,$Configuration,$dotnetHash)
    $cachePath=Join-Path $cacheRoot ($projectName+'.json')
    $cache=$null
    if(Test-Path -LiteralPath $cachePath -PathType Leaf){try{$cache=Get-Content -LiteralPath $cachePath -Raw | ConvertFrom-Json}catch{$cache=$null}}
    $cached=$false
    if($null -ne $cache -and (Test-Path -LiteralPath $destination -PathType Leaf)){
        $outputHash=Get-FileSha256 $destination
        $cached=([string]$cache.inputHash -eq $fingerprint.Hash -and [string]$cache.outputHash -eq $outputHash -and [string]$cache.configuration -eq $Configuration)
    }
    if($cached){
        Write-Host "[ OK ] SDK Reference: $projectName - SHA-256 checked $($fingerprint.Count) source/build input file(s); cached DLL is current."
    }else{
        Write-Host "[INFO] SDK Reference: $projectName - SHA-256 checked $($fingerprint.Count) source/build input file(s); cache miss requires this project to compile."
        if(Test-Path -LiteralPath $out){Remove-Item -LiteralPath $out -Recurse -Force}
        New-Item -ItemType Directory -Force -Path $out | Out-Null
        & $DotNet build $project.FullName --configuration $Configuration --output $out --nologo -p:PublishAot=false -p:SelfContained=false
        if($LASTEXITCODE -ne 0){throw "[FAIL] SDK Reference compilation failed: $($project.FullName)"}
        $dll=Join-Path $out ($assemblyName+'.dll')
        if(-not(Test-Path -LiteralPath $dll -PathType Leaf)){throw "[FAIL] SDK Reference DLL was not produced: $dll"}
        Copy-Item -LiteralPath $dll -Destination $destination -Force
        $outputHash=Get-FileSha256 $destination
        @{schemaVersion=1;project=$projectName;configuration=$Configuration;inputHash=$fingerprint.Hash;outputHash=$outputHash} | ConvertTo-Json | Set-Content -LiteralPath $cachePath -Encoding UTF8
        Write-Host "[ OK ] SDK Reference: $projectName - compiled and SHA-256 cache updated."
    }
    $assemblies.Add([pscustomobject]@{project=$projectName;assembly=$assemblyName;file=('All/'+$projectName+'.dll');autoReference=$autoReference})
    if($autoReference){
        foreach($symbol in @(Get-PublicSymbols $project.DirectoryName)){
            $symbols.Add([pscustomobject]@{name=$symbol.name;namespace=$symbol.namespace;project=$projectName;assembly=$assemblyName;file=('All/'+$projectName+'.dll')})
        }
    }
}
foreach($dll in @(Get-ChildItem -LiteralPath $store -File -Filter '*.dll' -ErrorAction SilentlyContinue)){
    if(-not $expectedDestinations.Contains($dll.FullName)){Remove-Item -LiteralPath $dll.FullName -Force}
}
$manifestPath=Join-Path $SdkRoot 'References\manifest.json'
@{schemaVersion=2;configuration=$Configuration;assemblies=$assemblies;symbols=$symbols} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Host "[ OK ] SDK central references ready: $($assemblies.Count) DLL(s), $($symbols.Count) auto-resolvable public symbol(s) -> $store"
