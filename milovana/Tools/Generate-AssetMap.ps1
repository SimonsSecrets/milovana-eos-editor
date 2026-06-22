<#
.SYNOPSIS
  Build asset-map.json for a Milovana tease by joining the exported tease.json
  manifest to the local source files.

.DESCRIPTION
  Images are matched by SHA-1 of the file bytes (the manifest 'hash' field is SHA-1);
  audio/other files are matched by filename (the 'files' manifest is keyed by filename).
  Manifest width/height are unreliable, so true dimensions are read from the local image.
  See milovana/Documentation/EOS-Tease-Authoring-Guide.md §5.1-5.2.

.PARAMETER TeaseDir
  Path to the tease folder containing tease.json, Gallery/<name>/, and Files/.

.EXAMPLE
  ./Generate-AssetMap.ps1 -TeaseDir ../Teases/TheFuckingMachine-Introduction
#>
param(
    [Parameter(Mandatory = $true)][string]$TeaseDir
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$TeaseDir = (Resolve-Path -LiteralPath $TeaseDir).Path
$teaseJsonPath = Join-Path $TeaseDir 'tease.json'
$tease = Get-Content -Raw -LiteralPath $teaseJsonPath | ConvertFrom-Json
$teaseName = Split-Path $TeaseDir -Leaf

function Get-Sha1Lower([string]$path) {
    (Get-FileHash -Algorithm SHA1 -LiteralPath $path).Hash.ToLower()
}

$unmatchedImages = @()
$unusedLocal = @()
$unmatchedFiles = @()

# ---- galleries: match by SHA-1 within each gallery's local folder ----
$galleriesOut = [ordered]@{}
foreach ($uuid in $tease.galleries.PSObject.Properties.Name) {
    $g = $tease.galleries.$uuid
    $name = $g.name
    $folderRel = "Gallery/$name"
    $folderAbs = Join-Path $TeaseDir $folderRel

    $localByHash = @{}
    if (Test-Path -LiteralPath $folderAbs) {
        Get-ChildItem -LiteralPath $folderAbs -File |
            Where-Object { $_.Extension -match '^\.(jpg|jpeg|png)$' } |
            ForEach-Object { $localByHash[(Get-Sha1Lower $_.FullName)] = $_ }
    }

    $matched = @{}
    $imagesOut = [ordered]@{}
    foreach ($img in @($g.images)) {
        $h = ([string]$img.hash).ToLower()
        if ($localByHash.ContainsKey($h)) {
            $f = $localByHash[$h]
            $w = 0; $ht = 0
            try {
                $bmp = [System.Drawing.Image]::FromFile($f.FullName)
                $w = $bmp.Width; $ht = $bmp.Height
                $bmp.Dispose()
            } catch { }
            $imagesOut[$f.Name] = [ordered]@{
                id      = $img.id
                hash    = $h
                size    = $img.size
                width   = $w
                height  = $ht
                locator = "gallery:$uuid/$($img.id)"
            }
            $matched[$h] = $true
        }
        else {
            $unmatchedImages += "gallery '$name' ($uuid) image id $($img.id) hash $h - no local file"
        }
    }
    foreach ($h in $localByHash.Keys) {
        if (-not $matched.ContainsKey($h)) {
            $unusedLocal += "$folderRel/$($localByHash[$h].Name) (hash $h) - not referenced in manifest"
        }
    }
    $galleriesOut[$name] = [ordered]@{
        uuid        = $uuid
        localFolder = $folderRel
        images      = $imagesOut
    }
}

# ---- files: match by filename ----
$filesOut = [ordered]@{}
foreach ($fname in $tease.files.PSObject.Properties.Name) {
    $fmeta = $tease.files.$fname
    $localRel = "Files/$fname"
    $localAbs = Join-Path $TeaseDir $localRel
    if (Test-Path -LiteralPath $localAbs) {
        $filesOut[$fname] = [ordered]@{
            id        = $fmeta.id
            hash      = ([string]$fmeta.hash).ToLower()
            size      = $fmeta.size
            type      = $fmeta.type
            localPath = $localRel
            locator   = "file:$fname"
        }
    }
    else {
        $unmatchedFiles += "$fname - no local Files/ entry"
    }
}

$map = [ordered]@{
    tease         = $teaseName
    description   = "Per-tease mapping from Milovana server asset IDs (uuid/image id, file name) to local source files. Built by matching each local file's SHA-1 to the manifest 'hash' in tease.json. Milovana manifest width/height are unreliable; width/height here are true local dimensions."
    hashAlgorithm = "sha1"
    galleries     = $galleriesOut
    files         = $filesOut
}

$outPath = Join-Path $TeaseDir 'asset-map.json'
$json = $map | ConvertTo-Json -Depth 12
[System.IO.File]::WriteAllText($outPath, $json, (New-Object System.Text.UTF8Encoding($false)))

# ---- report ----
$imgCount = (@($tease.galleries.PSObject.Properties.Name) | ForEach-Object { @($tease.galleries.$_.images).Count } | Measure-Object -Sum).Sum
Write-Host "Tease            : $teaseName"
Write-Host "Galleries        : $(@($tease.galleries.PSObject.Properties.Name).Count)"
Write-Host "Manifest images  : $imgCount  | matched: $($imgCount - $unmatchedImages.Count)  | unmatched: $($unmatchedImages.Count)"
Write-Host "Manifest files   : $(@($tease.files.PSObject.Properties.Name).Count) | unmatched: $($unmatchedFiles.Count)"
if ($unmatchedImages.Count) { Write-Host "`nUNMATCHED IMAGES:"; $unmatchedImages | ForEach-Object { Write-Host "  $_" } }
if ($unusedLocal.Count)     { Write-Host "`nLOCAL FILES NOT IN MANIFEST:"; $unusedLocal | ForEach-Object { Write-Host "  $_" } }
if ($unmatchedFiles.Count)  { Write-Host "`nUNMATCHED FILES:"; $unmatchedFiles | ForEach-Object { Write-Host "  $_" } }
$ok = ($unmatchedImages.Count -eq 0 -and $unmatchedFiles.Count -eq 0)
Write-Host "`nResult: $(if ($ok) { 'OK - every manifest entry resolved' } else { 'INCOMPLETE - see unmatched above' })"
Write-Host "Wrote $outPath"
