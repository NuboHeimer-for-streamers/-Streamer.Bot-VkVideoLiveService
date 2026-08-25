<#
.SYNOPSIS
  Pack VKVideoLiveService.cs into a Streamer.bot import string (.txt).

.DESCRIPTION
  Format: Base64( "SBAE" + gzip(utf8 JSON) ).
  Execute Code (type 99999) stores source in byteCode as Base64(UTF-8 text).

  Version is read from the file header comment:
    ///   Version:      x.y.z[_dev]

  Output name: VkLiveService_{version}.txt
  Written to OutputDir and copied to docs/static/files/VkLiveService/.

.EXAMPLE
  .\tools\Pack-StreamerBotImport.ps1 -SourceDir .
.EXAMPLE
  .\tools\Pack-StreamerBotImport.ps1 -GitRef dev
#>
[CmdletBinding()]
param(
    [string]$TemplatePath,

    [string]$SourceDir,

    [string]$GitRef,

    [string]$OutputDir = 'D:\Projects\NuboHeimer\Output\Code\Streamer.bot\VkLiveService',

    [string]$DocsFilesDir,

    [string]$RepoRoot,

    [string]$ImportName = 'VkVideoLive Service',

    [string]$CodeBlockName = 'VKVideoLive Method Collection',

    [string]$SourceFileName = 'VKVideoLiveService.cs',

    [string]$DefaultTemplate = 'VkLiveService_5.0.0_dev.3.txt',

    [switch]$SkipDocsCopy
)

$ErrorActionPreference = 'Stop'

if (-not $RepoRoot) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    $RepoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
}

if (-not $DocsFilesDir) {
    $DocsFilesDir = Join-Path $RepoRoot 'docs\static\files\VkLiveService'
}

function Decode-SbaeImport {
    param([string]$Path)
    $b64 = (Get-Content -LiteralPath $Path -Raw).Trim()
    $bytes = [Convert]::FromBase64String($b64)
    $sig = [Text.Encoding]::ASCII.GetString($bytes, 0, 4)
    if ($sig -ne 'SBAE') {
        throw "Unexpected magic '$sig' in $Path (expected SBAE)"
    }
    $gzLen = $bytes.Length - 4
    $gzBytes = New-Object byte[] $gzLen
    [Array]::Copy($bytes, 4, $gzBytes, 0, $gzLen)
    $gzMs = New-Object IO.MemoryStream(, $gzBytes)
    $gz = New-Object IO.Compression.GZipStream($gzMs, [IO.Compression.CompressionMode]::Decompress)
    $out = New-Object IO.MemoryStream
    $gz.CopyTo($out)
    return [Text.Encoding]::UTF8.GetString($out.ToArray())
}

function Encode-SbaeImport {
    param([string]$JsonText)
    $utf8 = New-Object Text.UTF8Encoding $false
    $payload = $utf8.GetBytes($JsonText)
    $ms = New-Object IO.MemoryStream
    $gz = New-Object IO.Compression.GZipStream($ms, [IO.Compression.CompressionLevel]::Optimal, $true)
    $gz.Write($payload, 0, $payload.Length)
    $gz.Dispose()
    $compressed = $ms.ToArray()
    $ms.Dispose()
    $out = New-Object byte[] ($compressed.Length + 4)
    [Array]::Copy([Text.Encoding]::ASCII.GetBytes('SBAE'), 0, $out, 0, 4)
    [Array]::Copy($compressed, 0, $out, 4, $compressed.Length)
    return [Convert]::ToBase64String($out)
}

function Get-SourceText {
    param(
        [string]$FileName,
        [string]$Dir,
        [string]$Ref
    )
    if ($Dir) {
        $path = Join-Path $Dir $FileName
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Source file not found: $path"
        }
        return [IO.File]::ReadAllText($path)
    }
    if ($Ref) {
        $tmp = Join-Path ([IO.Path]::GetTempPath()) ("sb-pack-" + [Guid]::NewGuid().ToString('n') + '-' + $FileName)
        try {
            $p = Start-Process -FilePath 'git' `
                -ArgumentList @('-C', $RepoRoot, 'show', "${Ref}:${FileName}") `
                -RedirectStandardOutput $tmp `
                -RedirectStandardError ($tmp + '.err') `
                -NoNewWindow -Wait -PassThru
            if ($p.ExitCode -ne 0) {
                $err = if (Test-Path ($tmp + '.err')) { [IO.File]::ReadAllText($tmp + '.err') } else { 'unknown' }
                throw "git show ${Ref}:${FileName} failed: $err"
            }
            return [IO.File]::ReadAllText($tmp, (New-Object Text.UTF8Encoding $false))
        }
        finally {
            Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath ($tmp + '.err') -ErrorAction SilentlyContinue
        }
    }
    throw 'Provide -SourceDir or -GitRef'
}

function ConvertTo-CrLf {
    param([string]$Text)
    $normalized = $Text -replace "`r`n", "`n" -replace "`r", "`n"
    return ($normalized -replace "`n", "`r`n")
}

function Get-VersionFromSourceComment {
    param([string]$Text)
    if ($Text -match '(?m)^///\s+Version:\s+(\S+)\s*$') {
        return $Matches[1]
    }
    throw 'Could not parse /// Version: from VKVideoLiveService.cs'
}

function Set-JsonStringProperty {
    param(
        [string]$Json,
        [string]$PropertyName,
        [string]$Value
    )
    $escaped = ($Value -replace '\\', '\\' -replace '"', '\"')
    $pattern = '("' + [regex]::Escape($PropertyName) + '"\s*:\s*")(?:\\.|[^"\\])*(")'
    $regex = New-Object System.Text.RegularExpressions.Regex($pattern)
    if (-not $regex.IsMatch($Json)) {
        throw "Property '$PropertyName' not found for string replace"
    }
    return $regex.Replace($Json, '${1}' + $escaped.Replace('$', '$$') + '${2}', 1)
}

function Set-CodeBlockByteCode {
    param(
        [string]$Json,
        [string]$BlockName,
        [string]$ByteCodeBase64
    )
    $nameEsc = [regex]::Escape($BlockName)
    $pattern = '(?s)("name"\s*:\s*"' + $nameEsc + '".{0,50000}?"byteCode"\s*:\s*")[^"]*(")'
    $regex = New-Object System.Text.RegularExpressions.Regex($pattern)
    $m = $regex.Match($Json)
    if (-not $m.Success) {
        throw "Code block '$BlockName' / byteCode not found in template JSON"
    }
    return $regex.Replace($Json, '${1}' + $ByteCodeBase64 + '${2}', 1)
}

function Set-UiVersionComment {
    param(
        [string]$Json,
        [string]$VersionLabel
    )
    $escaped = ($VersionLabel -replace '\\', '\\' -replace '"', '\"')
    $pattern = '("value"\s*:\s*")v[\d][^"]*(")'
    $regex = New-Object System.Text.RegularExpressions.Regex($pattern)
    if (-not $regex.IsMatch($Json)) {
        throw 'UI version comment (value v...) not found in template'
    }
    return $regex.Replace($Json, '${1}' + $escaped.Replace('$', '$$') + '${2}', 1)
}

if (-not $TemplatePath) {
    $TemplatePath = Join-Path $OutputDir $DefaultTemplate
}
if (-not (Test-Path -LiteralPath $TemplatePath)) {
    $fallback = Join-Path $DocsFilesDir $DefaultTemplate
    if (Test-Path -LiteralPath $fallback) {
        $TemplatePath = $fallback
    }
    else {
        throw "Template not found: $TemplatePath (also tried $fallback)"
    }
}

if (-not $SourceDir -and -not $GitRef) {
    $SourceDir = $RepoRoot
}

Write-Host "Template:  $TemplatePath"
Write-Host "Sources:   $(if ($SourceDir) { $SourceDir } else { "git:$GitRef" })"

$sourceText = Get-SourceText -FileName $SourceFileName -Dir $SourceDir -Ref $GitRef
$version = Get-VersionFromSourceComment -Text $sourceText
$uiVersion = if ($version.StartsWith('v')) { $version } else { "v$version" }

Write-Host "Version:   $version"
Write-Host "UI label:  $uiVersion"
Write-Host "Name:      $ImportName"

$json = Decode-SbaeImport -Path $TemplatePath
$json = Set-JsonStringProperty -Json $json -PropertyName 'name' -Value $ImportName
$json = Set-JsonStringProperty -Json $json -PropertyName 'version' -Value $version
$json = Set-UiVersionComment -Json $json -VersionLabel $uiVersion

$src = ConvertTo-CrLf -Text $sourceText
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($src))
$json = Set-CodeBlockByteCode -Json $json -BlockName $CodeBlockName -ByteCodeBase64 $b64
Write-Host ("  updated: {0} ({1} chars)" -f $CodeBlockName, $src.Length)

$check = $json | ConvertFrom-Json
if ($check.meta.version -ne $version) {
    throw "meta.version mismatch after patch: $($check.meta.version)"
}
$codeAction = $check.data.actions | Where-Object { $_.name -eq '[VKVideoLive] Code' }
$methodSa = $codeAction.subActions | Where-Object { $_.name -eq $CodeBlockName }
if (-not $methodSa) {
    throw "Packed import missing code block '$CodeBlockName'"
}
$methodSrc = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($methodSa.byteCode))
if ($methodSrc -notmatch [regex]::Escape("///   Version:      $version")) {
    throw "Packed source does not contain expected Version comment: $version"
}

$encoded = Encode-SbaeImport -JsonText $json
$fileName = "VkLiveService_$version.txt"

if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
$outPath = Join-Path $OutputDir $fileName
[IO.File]::WriteAllText($outPath, $encoded)
Write-Host "Wrote:     $outPath"
Write-Host "Size:      $((Get-Item -LiteralPath $outPath).Length) bytes"

if (-not $SkipDocsCopy) {
    if (-not (Test-Path -LiteralPath $DocsFilesDir)) {
        New-Item -ItemType Directory -Path $DocsFilesDir -Force | Out-Null
    }
    $docsPath = Join-Path $DocsFilesDir $fileName
    Copy-Item -LiteralPath $outPath -Destination $docsPath -Force
    Write-Host "Copied:    $docsPath"
}
