param(
  [Parameter(Mandatory=$true)][string]$Project,
  [Parameter(Mandatory=$true)][string]$Image,
  [string]$Qemu = $env:INU_QEMU_X64,
  [string]$OvmfCode = $env:INU_OVMF_CODE,
  [string]$OvmfVars = $env:INU_OVMF_VARS,
  [string]$Matrix = (Join-Path $PSScriptRoot 'Inu.QemuTestMatrix.json'),
  [ValidateSet('pairwise','exhaustive')][string]$Mode,
  [int]$TimeoutSeconds = 20,
  [string]$OutputDirectory,
  [switch]$List,
  [switch]$FailFast
)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$releaseVersion=([IO.File]::ReadAllText((Join-Path (Split-Path -Parent $root) 'VERSION'))).Trim()
$dotnet=Join-Path $root '.toolchain\DotNet\dotnet.exe'
$launcher=Join-Path $root 'src\Inu.QemuLauncher\bin\x64\Release\net10.0\Inu.QemuLauncher.dll'
if(!(Test-Path -LiteralPath $dotnet -PathType Leaf)){throw "Private Inu .NET host not found: $dotnet"}
if(!(Test-Path -LiteralPath $launcher -PathType Leaf)){throw "Inu.QemuLauncher has not been built: $launcher"}
foreach($path in @($Project,$Image,$Matrix)){if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw "Required file not found: $path"}}
if([string]::IsNullOrWhiteSpace($Qemu)){throw 'QEMU path is required. Pass -Qemu or set INU_QEMU_X64.'}
if(!(Test-Path -LiteralPath $Qemu -PathType Leaf)){throw "QEMU executable not found: $Qemu"}

$matrixDoc=Get-Content -LiteralPath $Matrix -Raw | ConvertFrom-Json
if([int]$matrixDoc.schemaVersion -ne 2){throw 'Inu.QemuTestMatrix.json schemaVersion must be 2.'}
if(!$Mode){$Mode=[string]$matrixDoc.strategy}
$dims=[ordered]@{
  cpus=@($matrixDoc.dimensions.cpus)
  memoryMiB=@($matrixDoc.dimensions.memoryMiB)
  storage=@($matrixDoc.dimensions.storage)
  network=@($matrixDoc.dimensions.network)
  graphics=@($matrixDoc.dimensions.graphics)
  usb=@($matrixDoc.dimensions.usb)
}
foreach($entry in $dims.GetEnumerator()){if($entry.Value.Count -eq 0){throw "QEMU matrix dimension '$($entry.Key)' is empty."}}

function New-CartesianCases([Collections.Specialized.OrderedDictionary]$Dimensions){
  $names=@($Dimensions.Keys);$cases=@()
  function Expand([int]$Index,[hashtable]$Current){
    if($Index -ge $names.Count){$copy=[ordered]@{};foreach($n in $names){$copy[$n]=$Current[$n]};$script:generatedCases+=,[pscustomobject]$copy;return}
    $name=$names[$Index]
    foreach($value in @($Dimensions[$name])){$next=@{};foreach($k in $Current.Keys){$next[$k]=$Current[$k]};$next[$name]=$value;Expand ($Index+1) $next}
  }
  $script:generatedCases=@();Expand 0 @{};return @($script:generatedCases)
}
function Get-PairKeys($Case,[string[]]$Names){
  $keys=@();for($i=0;$i -lt $Names.Count;$i++){for($j=$i+1;$j -lt $Names.Count;$j++){$a=$Names[$i];$b=$Names[$j];$keys+="$a=$($Case.$a)|$b=$($Case.$b)"}};return $keys
}
function Select-PairwiseCases($AllCases,[string[]]$Names){
  $uncovered=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
  foreach($c in $AllCases){foreach($k in (Get-PairKeys $c $Names)){$null=$uncovered.Add($k)}}
  $selected=@();$remaining=[Collections.Generic.List[object]]::new();foreach($c in $AllCases){$remaining.Add($c)}
  while($uncovered.Count -gt 0){
    $best=$null;$bestScore=-1;$bestIndex=-1
    for($i=0;$i -lt $remaining.Count;$i++){$score=0;foreach($k in (Get-PairKeys $remaining[$i] $Names)){if($uncovered.Contains($k)){$score++}};if($score -gt $bestScore){$best=$remaining[$i];$bestScore=$score;$bestIndex=$i}}
    if($null -eq $best -or $bestScore -le 0){throw 'Pairwise generator could not cover the remaining dimension pairs.'}
    $selected+=,$best;foreach($k in (Get-PairKeys $best $Names)){$null=$uncovered.Remove($k)};$remaining.RemoveAt($bestIndex)
  }
  return @($selected)
}

$all=New-CartesianCases $dims
$names=@($dims.Keys)
$cases=if($Mode -eq 'exhaustive'){$all}else{Select-PairwiseCases $all $names}
$stamp=(Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
if([string]::IsNullOrWhiteSpace($OutputDirectory)){$OutputDirectory=Join-Path (Split-Path -Parent (Resolve-Path -LiteralPath $Project)) "Artifacts\QemuMatrix\$stamp"}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$null=New-Item -ItemType Directory -Force -Path $OutputDirectory
Write-Host "[INFO] Inu QEMU reliability matrix $releaseVersion"
Write-Host "[INFO] Mode: $Mode; cases: $($cases.Count); full Cartesian size: $($all.Count)"
Write-Host "[INFO] Artifacts: $OutputDirectory"

if($List){$i=0;foreach($c in $cases){$i++;Write-Host ("{0,3}: cpu={1} ram={2}MiB storage={3} network={4} graphics={5} usb={6}" -f $i,$c.cpus,$c.memoryMiB,$c.storage,$c.network,$c.graphics,$c.usb)};exit 0}

$results=@();$failed=0;$caseNumber=0
foreach($c in $cases){
  $caseNumber++;$caseId=('case-{0:D3}-cpu{1}-ram{2}-{3}-{4}-{5}-{6}' -f $caseNumber,$c.cpus,$c.memoryMiB,$c.storage,$c.network,$c.graphics,$c.usb)
  $caseDir=Join-Path $OutputDirectory $caseId;$null=New-Item -ItemType Directory -Force -Path $caseDir
  $caseImage=Join-Path $caseDir 'boot.img';Copy-Item -LiteralPath $Image -Destination $caseImage -Force
  $stdout=Join-Path $caseDir 'matrix.stdout.log';$stderr=Join-Path $caseDir 'matrix.stderr.log'
  $arguments=@($launcher,'run',$Project,'--qemu',$Qemu,'--image',$caseImage,'--timeout-seconds',[string]$TimeoutSeconds,'--cpus',[string]$c.cpus,'--memory-mib',[string]$c.memoryMiB,'--storage',[string]$c.storage,'--network',[string]$c.network,'--graphics',[string]$c.graphics,'--usb',[string]$c.usb,'--run-directory',$caseDir,'--accept-and-stop')
  if($OvmfCode){$arguments+=@('--ovmf-code',$OvmfCode)};if($OvmfVars){$arguments+=@('--ovmf-vars',$OvmfVars)}
  foreach($marker in @($matrixDoc.requiredMarkers)){$arguments+=@('--require-marker',[string]$marker)}
  Write-Host "[CASE] $caseId"
  $sw=[Diagnostics.Stopwatch]::StartNew()
  & $dotnet @arguments 1> $stdout 2> $stderr
  $exitCode=$LASTEXITCODE;$sw.Stop()
  $ok=$exitCode -eq 0;if(!$ok){$failed++}
  $results+=[ordered]@{id=$caseId;result=if($ok){'passed'}else{'failed'};exitCode=$exitCode;durationMilliseconds=$sw.ElapsedMilliseconds;cpus=[int]$c.cpus;memoryMiB=[int]$c.memoryMiB;storage=[string]$c.storage;network=[string]$c.network;graphics=[string]$c.graphics;usb=[string]$c.usb;artifactDirectory=$caseDir;stdout=$stdout;stderr=$stderr}
  if($ok){Write-Host "[ OK ] $caseId"}else{Write-Host "[FAIL] $caseId (exit $exitCode)"}
  if(-not [bool]$matrixDoc.artifactPolicy.keepCaseImage){Remove-Item -LiteralPath $caseImage -Force -ErrorAction SilentlyContinue}
  if($FailFast -and !$ok){break}
}
$report=[ordered]@{format='inu-qemu-matrix-report-v1';productVersion=$releaseVersion;generatedUtc=(Get-Date).ToUniversalTime().ToString('o');mode=$Mode;matrix=$Matrix;project=(Resolve-Path -LiteralPath $Project).Path;sourceImage=(Resolve-Path -LiteralPath $Image).Path;fullCartesianCount=$all.Count;selectedCount=$cases.Count;runCount=$results.Count;passed=@($results|Where-Object result -eq 'passed').Count;failed=@($results|Where-Object result -eq 'failed').Count;requiredMarkers=@($matrixDoc.requiredMarkers);results=$results}
$reportPath=Join-Path $OutputDirectory 'Inu.QemuMatrixReport.json';$report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $reportPath -Encoding UTF8
$md=@("# Inu QEMU Matrix Report","","- Version: $releaseVersion","- Mode: $Mode","- Selected: $($cases.Count) of $($all.Count) Cartesian cases","- Passed: $(@($results|Where-Object result -eq 'passed').Count)","- Failed: $(@($results|Where-Object result -eq 'failed').Count)","","| Case | CPU | RAM MiB | Storage | Network | Graphics | USB | Result |","|---|---:|---:|---|---|---|---|---|")
foreach($r in $results){$md+="| $($r.id) | $($r.cpus) | $($r.memoryMiB) | $($r.storage) | $($r.network) | $($r.graphics) | $($r.usb) | $($r.result) |"}
$md|Set-Content -LiteralPath (Join-Path $OutputDirectory 'Inu.QemuMatrixReport.md') -Encoding UTF8
Write-Host "[INFO] Matrix report: $reportPath"
if($failed -gt 0){Write-Host "[FAIL] QEMU matrix failures: $failed";exit 1};Write-Host "[ OK ] QEMU matrix passed: $($results.Count)";exit 0
