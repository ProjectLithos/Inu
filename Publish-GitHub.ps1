[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SourceRoot,
    [Parameter(Mandatory=$true)][string]$Version
)

$ErrorActionPreference='Stop'
$SourceRoot=[IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
$RepositoryUrl='https://github.com/ProjectLithos/Inu.git'
$RepositoryName='Inu'

function Fail([string]$m){ Write-Host "[FAIL] $m"; exit 1 }
# Windows PowerShell turns native stderr into ErrorRecord objects. Expected
# nonzero probe results (missing origin/identity/HEAD) must reach our exit checks.
# Keep this preference local and capture diagnostics for real command failures.
function Quote-NativeArgument([string]$value){
    if($null -eq $value){ return '""' }
    if($value.Length -gt 0 -and $value -notmatch '[\s"]'){ return $value }
    $escaped=[regex]::Replace($value,'(\\*)"','$1$1\"')
    $escaped=[regex]::Replace($escaped,'(\\+)$','$1$1')
    return '"' + $escaped + '"'
}
function Invoke-GitResult([string[]]$a, [int]$TimeoutSeconds=30){
    $psi=New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName=$script:GitExe
    $psi.WorkingDirectory=$SourceRoot
    $psi.UseShellExecute=$false
    $psi.CreateNoWindow=$true
    $psi.RedirectStandardOutput=$true
    $psi.RedirectStandardError=$true
    $psi.Arguments=( @('-C',$SourceRoot) + $a | ForEach-Object { Quote-NativeArgument ([string]$_) } ) -join ' '
    $psi.EnvironmentVariables['GIT_TERMINAL_PROMPT']='0'
    $psi.EnvironmentVariables['GCM_INTERACTIVE']='Never'
    $process=New-Object System.Diagnostics.Process
    $process.StartInfo=$psi
    try {
        if(-not $process.Start()){
            return [pscustomobject]@{ ExitCode=-1; Lines=@('Could not start git.'); TimedOut=$false }
        }
        $stdoutTask=$process.StandardOutput.ReadToEndAsync()
        $stderrTask=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit($TimeoutSeconds * 1000)){
            try { $process.Kill() } catch {}
            try { $process.WaitForExit() } catch {}
            $stdout=''; $stderr=''
            try { $stdout=$stdoutTask.GetAwaiter().GetResult() } catch {}
            try { $stderr=$stderrTask.GetAwaiter().GetResult() } catch {}
            $lines=@(($stdout + "`n" + $stderr) -split "`r?`n" | Where-Object { $_ -ne '' })
            $lines += "Git timed out after $TimeoutSeconds second(s)."
            return [pscustomobject]@{ ExitCode=124; Lines=$lines; TimedOut=$true }
        }
        $process.WaitForExit()
        $stdout=$stdoutTask.GetAwaiter().GetResult()
        $stderr=$stderrTask.GetAwaiter().GetResult()
        $lines=@(($stdout + "`n" + $stderr) -split "`r?`n" | Where-Object { $_ -ne '' })
        return [pscustomobject]@{ ExitCode=[int]$process.ExitCode; Lines=$lines; TimedOut=$false }
    } finally { $process.Dispose() }
}
function Stop-ProcessTree([System.Diagnostics.Process]$Process){
    if($null -eq $Process){ return }
    try {
        if($Process.HasExited){ return }
    } catch { return }

    # Process.Kill() on Windows PowerShell/.NET Framework terminates only the
    # direct process. Git may have spawned Git Credential Manager or another
    # helper that inherited console/pipe handles, so terminate the whole tree.
    try {
        $taskkill=Get-Command taskkill.exe -ErrorAction SilentlyContinue
        if($taskkill){
            & $taskkill.Source /PID $Process.Id /T /F *> $null
        }
    } catch {}

    try {
        if(-not $Process.HasExited){ $Process.Kill() }
    } catch {}
    try { [void]$Process.WaitForExit(5000) } catch {}
}
function Invoke-GitLiveResult([string[]]$a){
    $psi=New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName=$script:GitExe
    $psi.WorkingDirectory=$SourceRoot
    $psi.UseShellExecute=$false
    $psi.CreateNoWindow=$false
    $psi.RedirectStandardOutput=$false
    $psi.RedirectStandardError=$false
    $psi.Arguments=( @('-C',$SourceRoot) + $a | ForEach-Object { Quote-NativeArgument ([string]$_) } ) -join ' '
    $psi.EnvironmentVariables['GIT_TERMINAL_PROMPT']='0'
    $psi.EnvironmentVariables['GCM_INTERACTIVE']='Never'
    $process=New-Object System.Diagnostics.Process
    $process.StartInfo=$psi
    try {
        if(-not $process.Start()){
            return [pscustomobject]@{ ExitCode=-1; TimedOut=$false }
        }
        # Do not kill a healthy large push on an arbitrary wall-clock deadline. Git's
        # HTTPS low-speed guard below detects a genuinely stalled transport instead.
        $process.WaitForExit()
        return [pscustomobject]@{ ExitCode=[int]$process.ExitCode; TimedOut=$false }
    } finally { $process.Dispose() }
}
function Git([string[]]$a, [int]$TimeoutSeconds=30){
    $result=Invoke-GitResult -a $a -TimeoutSeconds $TimeoutSeconds
    foreach($line in $result.Lines){ Write-Host $line }
    if($result.ExitCode -ne 0){
        if($result.TimedOut){ Fail "Git timed out for ${RepositoryName}: git $($a -join ' ')" }
        Fail "Git failed for ${RepositoryName}: git $($a -join ' ') (exit $($result.ExitCode))"
    }
}
function Remove-IgnoredTrackedFiles {
    $probe=Invoke-GitResult -a @('ls-files','-ci','--exclude-standard')
    if($probe.ExitCode -ne 0){
        foreach($line in $probe.Lines){ Write-Host $line }
        Fail "Could not inspect ignored tracked files for $RepositoryName (exit $($probe.ExitCode))."
    }
    $paths=@($probe.Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if($paths.Count -eq 0){ return }
    Write-Host "[INFO] Removing $($paths.Count) generated/ignored file(s) from the $RepositoryName Git index."
    for($i=0; $i -lt $paths.Count; $i+=50){
        $last=[Math]::Min($i+49,$paths.Count-1)
        $chunk=@($paths[$i..$last])
        Git -a (@('rm','--cached','--ignore-unmatch','--') + $chunk)
    }
}


$GitExe=$null
$cmd=Get-Command git.exe -ErrorAction SilentlyContinue
if($cmd){$GitExe=$cmd.Source}
if(-not $GitExe){
    foreach($candidate in @(
        "$env:ProgramFiles\Git\cmd\git.exe",
        "$env:ProgramFiles\Git\bin\git.exe",
        "${env:ProgramFiles(x86)}\Git\cmd\git.exe"
    )){ if($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)){ $GitExe=$candidate; break } }
}
if(-not $GitExe){ Fail "Git is required to publish the successful source build to $RepositoryUrl." }

Write-Host "[INFO] Publishing $RepositoryName $Version to $RepositoryUrl"
if(-not (Test-Path -LiteralPath (Join-Path $SourceRoot '.git') -PathType Container)){
    Git -a @('init')
    Git -a @('branch','-M','main')
}

$probe=Invoke-GitResult -a @('remote','get-url','origin')
if($probe.ExitCode -eq 2){
    Git -a @('remote','add','origin',$RepositoryUrl)
    Write-Host "[ OK ] Configured origin for $RepositoryName."
} elseif($probe.ExitCode -eq 0){
    $origin=($probe.Lines -join "`n").Trim()
    if($origin -ne $RepositoryUrl){ Git -a @('remote','set-url','origin',$RepositoryUrl) }
} else {
    foreach($line in $probe.Lines){ Write-Host $line }
    Fail "Could not inspect origin for $RepositoryName (exit $($probe.ExitCode))."
}

# Use the user's Git identity when configured.  Only create a repository-local
# fallback identity when Git has no usable identity at all.
foreach($setting in @(
    @{ Key='user.name'; Fallback='ProjectLithos' },
    @{ Key='user.email'; Fallback='ProjectLithos@users.noreply.github.com' }
)){
    $probe=Invoke-GitResult -a @('config','--get',$setting.Key)
    if($probe.ExitCode -eq 1 -or ($probe.ExitCode -eq 0 -and [string]::IsNullOrWhiteSpace(($probe.Lines -join '')))){
        Git -a @('config',$setting.Key,$setting.Fallback)
    } elseif($probe.ExitCode -ne 0){
        foreach($line in $probe.Lines){ Write-Host $line }
        Fail "Could not inspect Git identity for $RepositoryName (exit $($probe.ExitCode))."
    }
}

Remove-IgnoredTrackedFiles
Git -a @('add','-A')
$probe=Invoke-GitResult -a @('diff','--cached','--quiet')
if($probe.ExitCode -notin @(0,1)){ Fail "Could not inspect staged changes for $RepositoryName (exit $($probe.ExitCode))." }
$hasChanges=($probe.ExitCode -eq 1)
$probe=Invoke-GitResult -a @('rev-parse','--verify','--quiet','HEAD')
if($probe.ExitCode -notin @(0,1)){ Fail "Could not inspect HEAD for $RepositoryName (exit $($probe.ExitCode))." }
$hasHead=($probe.ExitCode -eq 0)
if($hasChanges -or -not $hasHead){
    Git -a @('commit','-m',"Kath&Inu $Version successful build")
} else {
    Write-Host "[INFO] $RepositoryName has no source changes to commit."
}

function Read-SingleGitLine([string[]]$Arguments, [string]$FailureMessage, [int]$TimeoutSeconds=60){
    $result=Invoke-GitResult -a $Arguments -TimeoutSeconds $TimeoutSeconds
    if($result.ExitCode -ne 0){
        foreach($line in $result.Lines){ Write-Host $line }
        Fail "$FailureMessage (exit $($result.ExitCode))."
    }
    return (($result.Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1) -as [string])
}
function Get-RemoteMainCommit {
    $result=Invoke-GitResult -a @('ls-remote','--heads','origin','refs/heads/main') -TimeoutSeconds 60
    if($result.ExitCode -ne 0){
        foreach($line in $result.Lines){ Write-Host $line }
        Fail "Could not inspect origin/main for $RepositoryName (exit $($result.ExitCode))."
    }
    $line=($result.Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
    if([string]::IsNullOrWhiteSpace($line)){ return $null }
    $parts=@($line -split '\s+')
    if($parts.Count -lt 1 -or $parts[0] -notmatch '^[0-9a-fA-F]{40,64}$'){
        Fail "GitHub returned an unexpected origin/main reference for ${RepositoryName}: $line"
    }
    return $parts[0]
}
function Ensure-RemoteCommitAvailable([string]$Commit){
    if([string]::IsNullOrWhiteSpace($Commit)){ return }
    $probe=Invoke-GitResult -a @('cat-file','-e',($Commit + '^{commit}'))
    if($probe.ExitCode -eq 0){ return }
    Write-Host "[INFO] Remote main commit $Commit is not present locally; fetching origin/main metadata before publishing."
    $fetch=Invoke-GitLiveResult -a @('-c','http.lowSpeedLimit=1','-c','http.lowSpeedTime=300','fetch','--no-tags','origin','main')
    if($fetch.ExitCode -ne 0){
        Fail "Could not fetch origin/main before the incremental $RepositoryName publish (exit $($fetch.ExitCode))."
    }
    $probe=Invoke-GitResult -a @('cat-file','-e',($Commit + '^{commit}'))
    if($probe.ExitCode -ne 0){ Fail "Remote main commit $Commit is still unavailable locally after fetch." }
}
function Get-MissingCommits([string]$RemoteCommit, [string]$LocalHead){
    $arguments=if([string]::IsNullOrWhiteSpace($RemoteCommit)){
        @('rev-list','--reverse',$LocalHead)
    } else {
        @('rev-list','--reverse',("$RemoteCommit..$LocalHead"))
    }
    $result=Invoke-GitResult -a $arguments -TimeoutSeconds 120
    if($result.ExitCode -ne 0){
        foreach($line in $result.Lines){ Write-Host $line }
        Fail "Could not calculate commits missing from GitHub for $RepositoryName (exit $($result.ExitCode))."
    }
    return @($result.Lines | Where-Object { $_ -match '^[0-9a-fA-F]{40,64}$' })
}
function Get-RemoteMainCommitWithRetry {
    $delays=@(5,15,30,60,120,120)
    for($attempt=1; $attempt -le ($delays.Count + 1); $attempt++){
        $result=Invoke-GitResult -a @('ls-remote','--heads','origin','refs/heads/main') -TimeoutSeconds 60
        if($result.ExitCode -eq 0){
            $line=($result.Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
            if([string]::IsNullOrWhiteSpace($line)){ return $null }
            $parts=@($line -split '\s+')
            if($parts.Count -lt 1 -or $parts[0] -notmatch '^[0-9a-fA-F]{40,64}$'){
                Fail "GitHub returned an unexpected origin/main reference for ${RepositoryName}: $line"
            }
            return $parts[0]
        }
        if($attempt -gt $delays.Count){
            foreach($line in $result.Lines){ Write-Host $line }
            Fail "Could not inspect origin/main for $RepositoryName after repeated connectivity retries (exit $($result.ExitCode))."
        }
        $delay=$delays[$attempt-1]
        Write-Host "[WARN] GitHub origin/main probe failed (exit $($result.ExitCode)); retrying in $delay second(s) without changing the publish batch."
        Start-Sleep -Seconds $delay
    }
}
function Wait-GitHubConnectivity([string]$Operation){
    $delays=@(5,15,30,60,120,120)
    for($attempt=1; $attempt -le ($delays.Count + 1); $attempt++){
        $probe=Invoke-GitResult -a @('ls-remote','--heads','origin') -TimeoutSeconds 60
        if($probe.ExitCode -eq 0){
            if($attempt -gt 1){ Write-Host "[ OK ] GitHub connectivity recovered for $Operation." }
            return $true
        }
        if($attempt -gt $delays.Count){
            foreach($line in $probe.Lines){ Write-Host $line }
            return $false
        }
        $delay=$delays[$attempt-1]
        Write-Host "[WARN] GitHub is currently unreachable while $Operation (probe exit $($probe.ExitCode)); retrying in $delay second(s)."
        Start-Sleep -Seconds $delay
    }
    return $false
}
function Measure-GitObjectRange([string]$BaseCommit, [string]$TargetCommit){
    $range=if([string]::IsNullOrWhiteSpace($BaseCommit)){ $TargetCommit } else { "$BaseCommit..$TargetCommit" }
    $objects=Invoke-GitResult -a @('rev-list','--objects','--no-object-names',$range) -TimeoutSeconds 300
    if($objects.ExitCode -ne 0){
        foreach($line in $objects.Lines){ Write-Host $line }
        Fail "Could not enumerate Git objects for incremental publish range $range (exit $($objects.ExitCode))."
    }
    $ids=@($objects.Lines | Where-Object { $_ -match '^[0-9a-fA-F]{40,64}$' })
    if($ids.Count -eq 0){ return [pscustomobject]@{ Bytes=[Int64]0; Objects=0 } }

    $psi=New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName=$script:GitExe
    $psi.WorkingDirectory=$SourceRoot
    $psi.UseShellExecute=$false
    $psi.CreateNoWindow=$true
    $psi.RedirectStandardInput=$true
    $psi.RedirectStandardOutput=$true
    $psi.RedirectStandardError=$true
    $psi.Arguments=( @('-C',$SourceRoot,'cat-file','--batch-check=%(objectsize)') | ForEach-Object { Quote-NativeArgument ([string]$_) } ) -join ' '
    $psi.EnvironmentVariables['GIT_TERMINAL_PROMPT']='0'
    $psi.EnvironmentVariables['GCM_INTERACTIVE']='Never'
    $process=New-Object System.Diagnostics.Process
    $process.StartInfo=$psi
    try {
        if(-not $process.Start()){ Fail "Could not start git cat-file while sizing an incremental publish batch." }
        $stdoutTask=$process.StandardOutput.ReadToEndAsync()
        $stderrTask=$process.StandardError.ReadToEndAsync()
        foreach($id in $ids){ $process.StandardInput.WriteLine($id) }
        $process.StandardInput.Close()
        if(-not $process.WaitForExit(300000)){
            Stop-ProcessTree $process
            Fail "Timed out while measuring Git objects for incremental publish range $range."
        }
        $stdout=$stdoutTask.GetAwaiter().GetResult()
        $stderr=$stderrTask.GetAwaiter().GetResult()
        if($process.ExitCode -ne 0){
            if(-not [string]::IsNullOrWhiteSpace($stderr)){ Write-Host $stderr }
            Fail "git cat-file failed while measuring incremental publish range $range (exit $($process.ExitCode))."
        }
        $total=[Int64]0
        $parsed=0
        foreach($line in @($stdout -split "`r?`n")){
            if([string]::IsNullOrWhiteSpace($line)){ continue }
            $size=[Int64]0
            if(-not [Int64]::TryParse($line.Trim(),[ref]$size)){
                Fail "Unexpected git cat-file size output while measuring ${range}: $line"
            }
            $total += $size
            $parsed++
        }
        if($parsed -ne $ids.Count){
            Fail "Git object sizing returned $parsed size(s) for $($ids.Count) object(s) in range $range."
        }
        return [pscustomobject]@{ Bytes=$total; Objects=$ids.Count }
    } finally { $process.Dispose() }
}
function Format-ByteCount([Int64]$Bytes){
    if($Bytes -ge 1GB){ return ('{0:N2} GiB' -f ($Bytes / 1GB)) }
    if($Bytes -ge 1MB){ return ('{0:N1} MiB' -f ($Bytes / 1MB)) }
    if($Bytes -ge 1KB){ return ('{0:N1} KiB' -f ($Bytes / 1KB)) }
    return "$Bytes B"
}
function Select-SizeAwareBatch([string]$BaseCommit, [string[]]$Missing, [int]$StartIndex, [int]$MaximumCount, [Int64]$MaximumBytes){
    $low=1
    $high=$MaximumCount
    $bestCount=0
    $bestMeasure=$null
    while($low -le $high){
        $mid=[int][Math]::Floor(($low+$high)/2.0)
        $target=$Missing[$StartIndex+$mid-1]
        $measure=Measure-GitObjectRange -BaseCommit $BaseCommit -TargetCommit $target
        if($measure.Bytes -le $MaximumBytes){
            $bestCount=$mid
            $bestMeasure=$measure
            $low=$mid+1
        } else {
            $high=$mid-1
        }
    }
    if($bestCount -eq 0){
        $target=$Missing[$StartIndex]
        $bestMeasure=Measure-GitObjectRange -BaseCommit $BaseCommit -TargetCommit $target
        $bestCount=1
        Write-Host "[WARN] The next single Inu commit requires approximately $(Format-ByteCount $bestMeasure.Bytes) of previously unpublished Git objects, above the configured batch target of $(Format-ByteCount $MaximumBytes). It will be attempted alone."
    }
    return [pscustomobject]@{ Count=$bestCount; Measure=$bestMeasure }
}
function Push-MissingCommitsInBatches([string]$RemoteCommit, [string]$LocalHead){
    if(-not [string]::IsNullOrWhiteSpace($RemoteCommit)){
        Ensure-RemoteCommitAvailable $RemoteCommit
        $ancestor=Invoke-GitResult -a @('merge-base','--is-ancestor',$RemoteCommit,$LocalHead)
        if($ancestor.ExitCode -eq 1){
            Fail "origin/main ($RemoteCommit) is not an ancestor of local HEAD ($LocalHead). Refusing to rewrite GitHub history; pull/reconcile the remote branch first."
        } elseif($ancestor.ExitCode -ne 0){
            foreach($line in $ancestor.Lines){ Write-Host $line }
            Fail "Could not verify origin/main ancestry for $RepositoryName (exit $($ancestor.ExitCode))."
        }
    }

    $missing=@(Get-MissingCommits -RemoteCommit $RemoteCommit -LocalHead $LocalHead)
    if($missing.Count -eq 0){
        Write-Host "[INFO] GitHub already contains every $RepositoryName commit through $LocalHead."
        return
    }

    $maximumCommitCount=25
    if(-not [string]::IsNullOrWhiteSpace($env:INU_GITHUB_PUSH_BATCH_SIZE)){
        $parsed=0
        if([int]::TryParse($env:INU_GITHUB_PUSH_BATCH_SIZE,[ref]$parsed) -and $parsed -ge 1 -and $parsed -le 1000){
            $maximumCommitCount=$parsed
        } else {
            Write-Host "[WARN] Ignoring invalid INU_GITHUB_PUSH_BATCH_SIZE='$($env:INU_GITHUB_PUSH_BATCH_SIZE)'; using a maximum of $maximumCommitCount commits per batch."
        }
    }
    $maximumMiB=192
    if(-not [string]::IsNullOrWhiteSpace($env:INU_GITHUB_PUSH_MAX_MIB)){
        $parsedMiB=0
        if([int]::TryParse($env:INU_GITHUB_PUSH_MAX_MIB,[ref]$parsedMiB) -and $parsedMiB -ge 32 -and $parsedMiB -le 1024){
            $maximumMiB=$parsedMiB
        } else {
            Write-Host "[WARN] Ignoring invalid INU_GITHUB_PUSH_MAX_MIB='$($env:INU_GITHUB_PUSH_MAX_MIB)'; using $maximumMiB MiB of unpublished object data per batch."
        }
    }
    $maximumBytes=[Int64]$maximumMiB * 1MB

    Write-Host "[INFO] $($missing.Count) $RepositoryName commit(s) are missing from GitHub. Batches are limited to $maximumCommitCount commit(s) and approximately $maximumMiB MiB of previously unpublished raw Git objects."
    $published=0
    $baseCommit=$RemoteCommit
    $countLimit=[Math]::Min($maximumCommitCount,$missing.Count)
    while($published -lt $missing.Count){
        $remaining=$missing.Count-$published
        $candidateLimit=[Math]::Min($countLimit,$remaining)
        $selection=Select-SizeAwareBatch -BaseCommit $baseCommit -Missing $missing -StartIndex $published -MaximumCount $candidateLimit -MaximumBytes $maximumBytes
        $attemptSize=[int]$selection.Count
        $lastIndex=$published+$attemptSize-1
        $target=$missing[$lastIndex]
        $firstDisplay=$published+1
        $lastDisplay=$lastIndex+1
        $shortTarget=if($target.Length -gt 12){ $target.Substring(0,12) } else { $target }
        $payloadText=Format-ByteCount ([Int64]$selection.Measure.Bytes)
        Write-Host "[INFO] $RepositoryName GitHub batch: commits $firstDisplay-$lastDisplay of $($missing.Count), target $shortTarget; $($selection.Measure.Objects) object(s), approximately $payloadText raw."

        $push=Invoke-GitLiveResult -a @('-c','http.version=HTTP/1.1','-c','http.lowSpeedLimit=1','-c','http.lowSpeedTime=300','push','origin',("${target}:refs/heads/main"))
        if($push.ExitCode -eq 0){
            $published+=$attemptSize
            $baseCommit=$target
            $countLimit=[Math]::Min($maximumCommitCount,$missing.Count-$published)
            Write-Host "[ OK ] $RepositoryName GitHub progress: $published / $($missing.Count) missing commit(s) published."
            continue
        }

        Write-Host "[WARN] $RepositoryName push returned exit $($push.ExitCode). Checking whether GitHub accepted the target and whether the network is still reachable before changing the batch."
        $connectivityProbe=Invoke-GitResult -a @('ls-remote','--heads','origin') -TimeoutSeconds 60
        $wasConnectivityFailure=($connectivityProbe.ExitCode -ne 0)
        if($wasConnectivityFailure){
            Write-Host "[WARN] The immediate GitHub probe also failed (exit $($connectivityProbe.ExitCode)); classifying this attempt as a connectivity failure, not a batch-size failure."
            if(-not (Wait-GitHubConnectivity -Operation "recovering the failed $RepositoryName batch $firstDisplay-$lastDisplay")){
                Fail "GitHub remained unreachable after repeated retries. No batch-size reduction was made because this is a connectivity failure; rerun publication when DNS/network access is restored. Earlier successful batches remain safely published."
            }
        }
        $remoteAfterFailure=Get-RemoteMainCommitWithRetry
        if($remoteAfterFailure -eq $target){
            $published+=$attemptSize
            $baseCommit=$target
            $countLimit=[Math]::Min($maximumCommitCount,$missing.Count-$published)
            Write-Host "[ OK ] GitHub already advanced to $shortTarget despite the client-side push error; treating batch $firstDisplay-$lastDisplay as published."
            continue
        }

        if($wasConnectivityFailure){
            Write-Host "[INFO] Connectivity has recovered and GitHub did not yet advance to $shortTarget; retrying the exact same batch before considering any size reduction."
            $retryPush=Invoke-GitLiveResult -a @('-c','http.version=HTTP/1.1','-c','http.lowSpeedLimit=1','-c','http.lowSpeedTime=300','push','origin',("${target}:refs/heads/main"))
            if($retryPush.ExitCode -eq 0){
                $published+=$attemptSize
                $baseCommit=$target
                $countLimit=[Math]::Min($maximumCommitCount,$missing.Count-$published)
                Write-Host "[ OK ] $RepositoryName GitHub progress: $published / $($missing.Count) missing commit(s) published after connectivity recovery."
                continue
            }
            if(-not (Wait-GitHubConnectivity -Operation "checking the retried $RepositoryName batch $firstDisplay-$lastDisplay")){
                Fail "GitHub became unreachable again while retrying the same batch. No size reduction was made; rerun publication when connectivity is stable. Earlier successful batches remain safely published."
            }
            $remoteAfterRetry=Get-RemoteMainCommitWithRetry
            if($remoteAfterRetry -eq $target){
                $published+=$attemptSize
                $baseCommit=$target
                $countLimit=[Math]::Min($maximumCommitCount,$missing.Count-$published)
                Write-Host "[ OK ] GitHub advanced to $shortTarget after the recovered retry despite a client-side error."
                continue
            }
        }

        if($attemptSize -le 1){
            Fail "GitHub rejected the single remaining $RepositoryName commit $target while connectivity was healthy. This commit may contain an object GitHub does not permit, or origin/main may have changed. Earlier successful batches remain safely published."
        }
        $countLimit=[Math]::Max(1,[int][Math]::Floor($attemptSize / 2.0))
        Write-Host "[WARN] GitHub did not advance to $shortTarget while connectivity was healthy. Retrying the same unpublished range with a smaller maximum of $countLimit commit(s); object-size limits remain in force."
    }
}

# Never force-push.  Publish backlog history through progressively newer
# intermediate commits so GitHub never has to receive the whole multi-gigabyte
# history in one pack. Each successful batch advances main by fast-forward only.
$localHead=Read-SingleGitLine -Arguments @('rev-parse','HEAD') -FailureMessage "Could not resolve local HEAD for $RepositoryName"
$remoteMain=Get-RemoteMainCommitWithRetry
if([string]::IsNullOrWhiteSpace($remoteMain)){
    Write-Host "[INFO] GitHub origin/main does not exist yet; incremental publication will begin from the oldest local commit."
} else {
    Write-Host "[INFO] GitHub origin/main currently points to $remoteMain."
}
Write-Host "[INFO] Pushing $RepositoryName to GitHub in incremental commit batches (no force-push; 5-minute zero-progress transport guard)..."
Push-MissingCommitsInBatches -RemoteCommit $remoteMain -LocalHead $localHead

# The final push is normally a no-op. It records the upstream relationship and
# verifies that GitHub main reaches the exact local HEAD after all batches.
$push=Invoke-GitLiveResult -a @('-c','http.version=HTTP/1.1','-c','http.lowSpeedLimit=1','-c','http.lowSpeedTime=300','push','-u','origin','HEAD:main')
if($push.ExitCode -ne 0){
    if(-not (Wait-GitHubConnectivity -Operation "verifying the final $RepositoryName HEAD push")){
        Fail "The incremental batches completed, but GitHub remained unreachable during the final HEAD verification. Rerun publication when connectivity returns."
    }
    $publishedAfterError=Get-RemoteMainCommitWithRetry
    if($publishedAfterError -ne $localHead){
        Fail "The incremental batches completed, but the final $RepositoryName HEAD verification push failed (exit $($push.ExitCode)) and origin/main did not reach local HEAD."
    }
    Write-Host "[ OK ] GitHub reached local HEAD despite the client-side final push error."
}
$publishedRemote=Get-RemoteMainCommitWithRetry
if($publishedRemote -ne $localHead){
    Fail "$RepositoryName GitHub publication ended at $publishedRemote instead of local HEAD $localHead."
}
Write-Host "[ OK ] $RepositoryName $Version published to GitHub in incremental batches."
