param(
    [string]$Destination = (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '.toolchain\Fonts\LinuxKernel')
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$KernelRepository = 'https://raw.githubusercontent.com/torvalds/linux'
$KernelCommit = 'a13307e97d5c54b65720bb71fa379960ded1e51a'
$Fonts = @(
    @{ Id='linux-vga-8x8';     Source='font_8x8.c';      Width=8;  Height=8;  Blob='e5b697fc967500fc9532591ae77329f191d4028d' },
    @{ Id='linux-vga-8x16';    Source='font_8x16.c';     Width=8;  Height=16; Blob='523e95c75569eee09d133adf1e52c6b08a18fab3' },
    @{ Id='linux-vga-6x11';    Source='font_6x11.c';     Width=6;  Height=11; Blob='671487ccc1724d9b73f969c5cb8ee6d39f1264f5' },
    @{ Id='linux-sun-8x16';    Source='font_sun8x16.c';  Width=8;  Height=16; Blob='2b7b2d8e548ac100a112ec6c6a62ca6311112d89' },
    @{ Id='linux-sun-12x22';   Source='font_sun12x22.c'; Width=12; Height=22; Blob='2afbc144bea81b8be8acafe2679a47bd017af4b5' }
)

function Write-UInt32LE([System.IO.BinaryWriter]$Writer, [UInt32]$Value) { $Writer.Write($Value) }
function Convert-KernelFontToPsf2([string]$SourcePath, [string]$OutputPath, [int]$Width, [int]$Height) {
    $text = Get-Content -LiteralPath $SourcePath -Raw
    # Linux kernel font sources use more than one comment style. Locate the
    # font_data initializer itself, then parse only its bitmap payload.
    $table = [regex]::Match($text, 'static\s+const\s+struct\s+font_data\s+fontdata_[A-Za-z0-9_]+\s*=\s*\{\s*\{[^}]*\}\s*,\s*\{', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $table.Success) { throw "Unable to locate font bitmap initializer in $SourcePath." }
    $glyphText = $text.Substring($table.Index + $table.Length)
    $end = $glyphText.IndexOf('} }')
    if ($end -lt 0) { $end = $glyphText.IndexOf('}}') }
    if ($end -gt 0) { $glyphText = $glyphText.Substring(0, $end) }
    $matches = [regex]::Matches($glyphText, '0x([0-9A-Fa-f]{2})\s*,')
    $rowBytes = [int][Math]::Ceiling($Width / 8.0)
    $bytesPerGlyph = $rowBytes * $Height
    $required = 256 * $bytesPerGlyph
    if ($matches.Count -lt $required) { throw "$SourcePath contains only $($matches.Count) glyph bytes; $required are required." }
    $glyphBytes = New-Object byte[] $required
    for ($i = 0; $i -lt $required; $i++) { $glyphBytes[$i] = [Convert]::ToByte($matches[$i].Groups[1].Value, 16) }

    $stream = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $writer = New-Object System.IO.BinaryWriter($stream)
        try {
            Write-UInt32LE $writer ([UInt32]::Parse('864AB572', [Globalization.NumberStyles]::HexNumber)) # PSF2 magic
            Write-UInt32LE $writer 0          # version
            Write-UInt32LE $writer 32         # header size
            Write-UInt32LE $writer 0          # no Unicode table; glyph index equals byte value
            Write-UInt32LE $writer 256        # glyph count
            Write-UInt32LE $writer ([UInt32]$bytesPerGlyph)
            Write-UInt32LE $writer ([UInt32]$Height)
            Write-UInt32LE $writer ([UInt32]$Width)
            $writer.Write($glyphBytes)
        } finally { $writer.Dispose() }
    } finally { $stream.Dispose() }
}

New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$sourceDir = Join-Path $Destination 'Source'
New-Item -ItemType Directory -Path $sourceDir -Force | Out-Null
$installedFonts = @()
$missingFonts = @()

function Invoke-OptionalDownload([string]$Url, [string]$OutFile, [string]$Id) {
    if (Test-Path -LiteralPath $OutFile -PathType Leaf) {
        Write-Host "[ OK ] Reusing cached source for ${Id}: $OutFile"
        return $true
    }

    $attempts = 3
    for ($attempt = 1; $attempt -le $attempts; $attempt++) {
        try {
            Write-Host "[INFO] Downloading $Id from Linux kernel $KernelCommit (attempt $attempt/$attempts)."
            Invoke-WebRequest -Uri $Url -OutFile $OutFile -UseBasicParsing -Headers @{ 'User-Agent' = 'Inu-SDK-FontInstaller' }
            return $true
        } catch {
            Remove-Item -LiteralPath $OutFile -Force -ErrorAction SilentlyContinue
            $status = $null
            try { $status = [int]$_.Exception.Response.StatusCode } catch { }
            $message = $_.Exception.Message
            if ($attempt -lt $attempts -and ($status -eq 429 -or $status -ge 500 -or -not $status)) {
                $delay = [Math]::Pow(2, $attempt)
                Write-Host "[WARN] Optional font download failed for $Id (HTTP $status). Retrying in $delay second(s)."
                Start-Sleep -Seconds $delay
                continue
            }
            Write-Host "[WARN] Optional font $Id could not be downloaded: $message"
            return $false
        }
    }
    return $false
}

foreach ($font in $Fonts) {
    $sourcePath = Join-Path $sourceDir $font.Source
    $psfPath = Join-Path $Destination ($font.Id + '.psf')

    if (Test-Path -LiteralPath $psfPath -PathType Leaf) {
        Write-Host "[ OK ] Reusing installed $($font.Id): $psfPath"
        $installedFonts += $font
        continue
    }

    $url = "$KernelRepository/$KernelCommit/lib/fonts/$($font.Source)"
    if (-not (Invoke-OptionalDownload $url $sourcePath $font.Id)) {
        $missingFonts += $font.Id
        continue
    }

    try {
        $firstLine = Get-Content -LiteralPath $sourcePath -TotalCount 1
        if ($firstLine -notmatch 'SPDX-License-Identifier:\s*GPL-2.0') { throw "Unexpected license header in $($font.Source)." }
        Convert-KernelFontToPsf2 $sourcePath $psfPath $font.Width $font.Height
        Write-Host "[ OK ] $($font.Id): $($font.Width)x$($font.Height) -> $psfPath"
        $installedFonts += $font
    } catch {
        Remove-Item -LiteralPath $psfPath -Force -ErrorAction SilentlyContinue
        Write-Host "[WARN] Optional font $($font.Id) could not be installed: $($_.Exception.Message)"
        $missingFonts += $font.Id
    }
}

$metadata = [ordered]@{
    source = 'Linux kernel'
    repository = 'https://github.com/torvalds/linux'
    commit = $KernelCommit
    license = 'GPL-2.0'
    generatedFormat = 'PSF2'
    glyphCount = 256
    complete = ($missingFonts.Count -eq 0)
    missingFonts = @($missingFonts)
    fonts = @($installedFonts | ForEach-Object { [ordered]@{ id=$_.Id; sourceFile=$_.Source; width=$_.Width; height=$_.Height; gitBlob=$_.Blob; psfFile=($_.Id + '.psf') } })
}
$metadata | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Destination 'LinuxKernelFonts.json') -Encoding UTF8
if ($installedFonts.Count -gt 0) {
    Write-Host "[ OK ] Linux kernel console font pack ready with $($installedFonts.Count) installed font(s)."
} else {
    Write-Host '[WARN] No optional Linux-kernel fonts are currently installed; Inu will use its embedded fallback console font.'
}
if ($missingFonts.Count -gt 0) {
    Write-Host "[WARN] Optional fonts not installed: $($missingFonts -join ', '). This does not block the Inu SDK toolchain."
}


# Optional free TrueType console face. The font binary is downloaded into the local toolchain and is not part of the Inu source distribution.
$ideRoot = Split-Path -Parent $PSScriptRoot
$trueTypeRoot = Join-Path $ideRoot 'Assets\Fonts\TrueType'
$trueTypeFont = Join-Path $trueTypeRoot 'DejaVuSansMono.ttf'
$trueTypeLicense = Join-Path $trueTypeRoot 'DejaVu-LICENSE.txt'
if (-not (Test-Path -LiteralPath $trueTypeFont -PathType Leaf)) {
    New-Item -ItemType Directory -Path $trueTypeRoot -Force | Out-Null
    $archive = Join-Path $trueTypeRoot 'dejavu-fonts-ttf-2.37.zip'
    $extract = Join-Path $trueTypeRoot 'extract'
    $url = 'https://github.com/dejavu-fonts/dejavu-fonts/releases/download/version_2_37/dejavu-fonts-ttf-2.37.zip'
    try {
        Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing -Headers @{ 'User-Agent'='Inu-SDK-FontInstaller' }
        $sha=(Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
        if($sha -ne 'f0ecb95e832a6adfde3e0cc7ec24b6b4e3471b3fc658125c8e57f981cb495689'){ throw "Unexpected DejaVu 2.37 archive SHA256: $sha" }
        Remove-Item -LiteralPath $extract -Recurse -Force -ErrorAction SilentlyContinue
        Expand-Archive -LiteralPath $archive -DestinationPath $extract -Force
        $font=Get-ChildItem -LiteralPath $extract -Recurse -Filter 'DejaVuSansMono.ttf' | Select-Object -First 1
        $license=Get-ChildItem -LiteralPath $extract -Recurse -Filter 'LICENSE' | Select-Object -First 1
        if(-not $font){ throw 'DejaVuSansMono.ttf was not found in the verified archive.' }
        Copy-Item -LiteralPath $font.FullName -Destination $trueTypeFont -Force
        if($license){ Copy-Item -LiteralPath $license.FullName -Destination $trueTypeLicense -Force }
        Write-Host "[ OK ] Free TrueType console font ready: $trueTypeFont"
    } catch {
        Remove-Item -LiteralPath $trueTypeFont -Force -ErrorAction SilentlyContinue
        Write-Host "[WARN] Optional DejaVu Sans Mono TrueType console font could not be installed: $($_.Exception.Message)"
    } finally {
        Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $extract -Recurse -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "[ OK ] Reusing free TrueType console font: $trueTypeFont"
}

exit 0
