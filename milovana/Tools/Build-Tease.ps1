<#
  Build-Tease.ps1 - parse a tease's script.md (the DSL source) into tease.json.

  Usage:
    powershell -NoProfile -ExecutionPolicy Bypass -File Build-Tease.ps1 -TeaseDir <tease folder>
  or via a per-tease .cmd wrapper (e.g. Build-Tease-TFM.cmd).

  The <tease folder> must contain: script.md, asset-map.json, and the exported tease.json
  (whose galleries/files/editor are reused verbatim; its pages are replaced).

  Grammar (only lines starting at column 0 with a known [KEYWORD] are parsed; all else ignored):
    [PAGE: key]  comment            [IMAGE: bucket/file.jpg | hold]
    [SAY (mode=,align=,duration=): html]   [METRONOME (bpm=,secs=)]   [PAUSE (secs=)]
    [AUDIO (bpm=,loops=)]           [NOTIFICATION (id=,target=): label]
    [CHOICE]  [OPTION (target=,color=): label]   [GOTO: key]   [END]
  A page with any [METRONOME]/[PAUSE] is timed: each block becomes its own EOS page (one tempo per
  page); the [SAY]s after a block are revealed evenly across secs; an [IMAGE]/[NOTIFICATION] before
  a block attaches to it; the page's [GOTO]/[END] applies after the last block; [GOTO] to the page's
  own key = loop. Image filenames resolve to locators via asset-map.json.
#>
param(
    [Parameter(Mandatory = $true)][string]$TeaseDir
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Web.Extensions
$base = (Resolve-Path -LiteralPath $TeaseDir).Path
$js = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$js.MaxJsonLength = [int]::MaxValue

# ---- locator lookup ----
$mapRaw = $js.DeserializeObject([IO.File]::ReadAllText("$base\asset-map.json", [Text.Encoding]::UTF8))
$loc = @{}
foreach ($g in $mapRaw["galleries"].Keys) {
    $imgs = $mapRaw["galleries"][$g]["images"]
    foreach ($f in $imgs.Keys) { $loc["$g/$f"] = $imgs[$f]["locator"] }
}
function L($k) { if (-not $loc.ContainsKey($k)) { throw "no locator for image '$k'" }; $loc[$k] }

# ---- action builders ----
function aSay2($payload, $mode, $align, $duration) {
    $s = [ordered]@{ label = ("<p>" + $payload + "</p>") }
    if ($mode) { $s["mode"] = $mode }
    if ($align) { $s["align"] = $align }
    if ($duration) { $s["duration"] = $duration }
    [ordered]@{ say = $s }
}
function aImg($k) { [ordered]@{ image = [ordered]@{ locator = (L $k) } } }
function aTimer($d, $st) { [ordered]@{ timer = [ordered]@{ duration = $d; style = $st } } }
function aGoto($t) { [ordered]@{ goto = [ordered]@{ target = $t } } }
function aEnd() { [ordered]@{ end = [ordered]@{} } }
function aAudio($bpm, $secs) {
    $f = ("metronome-{0:000}bpm.mp3" -f [int]$bpm)
    $lp = [int]([math]::Ceiling($secs / 3.0) + 1)
    [ordered]@{ "audio.play" = [ordered]@{ locator = "file:$f"; volume = 1.0; loops = $lp; background = $false } }
}
function aAudioN($bpm, $loops) {
    $f = ("metronome-{0:000}bpm.mp3" -f [int]$bpm)
    [ordered]@{ "audio.play" = [ordered]@{ locator = "file:$f"; volume = 1.0; loops = [int]$loops; background = $false } }
}
function aNotif($id, $lbl, $tgt) {
    [ordered]@{ "notification.create" = [ordered]@{ id = $id; buttonLabel = $lbl; buttonCommands = @( (aGoto $tgt) ) } }
}
function aChoice($opts) {
    $o = @()
    foreach ($x in $opts) { $o += [ordered]@{ label = $x.l; commands = @( (aGoto $x.t) ); color = $x.c } }
    [ordered]@{ choice = [ordered]@{ options = $o } }
}
function ParseParams($s) {
    $h = @{}
    if ($s) { foreach ($kv in ($s -split ',')) { $kv = $kv.Trim(); if ($kv) { $ix = $kv.IndexOf('='); if ($ix -ge 0) { $h[$kv.Substring(0, $ix).Trim()] = $kv.Substring($ix + 1).Trim() } } } }
    $h
}

$pages = [ordered]@{}

function BuildChoice($items) {
    $opts = @()
    foreach ($it in $items) { if ($it.kind -eq "OPTION") { $p = ParseParams $it.params; $opts += @{ l = $it.payload; t = $p["target"]; c = $p["color"] } } }
    aChoice $opts
}
function BuildSimple($key, $items) {
    $exit = $null
    foreach ($it in $items) { if ($it.kind -eq "GOTO") { $exit = @{ type = "goto"; target = $it.payload.Trim() } } elseif ($it.kind -eq "END") { $exit = @{ type = "end" } } }
    $hasChoice = @($items | Where-Object { $_.kind -eq "CHOICE" }).Count -gt 0
    $lastSay = -1; for ($i = 0; $i -lt $items.Count; $i++) { if ($items[$i].kind -eq "SAY") { $lastSay = $i } }
    $tapAdvance = ($exit -and $exit.type -eq "goto" -and -not $hasChoice)
    $a = @()
    for ($i = 0; $i -lt $items.Count; $i++) {
        $it = $items[$i]
        switch ($it.kind) {
            "IMAGE" { if ($it.payload.Trim() -ne "hold") { $a += (aImg $it.payload.Trim()) } }
            "AUDIO" { $p = ParseParams $it.params; $a += (aAudioN $p["bpm"] $p["loops"]) }
            "NOTIFICATION" { $p = ParseParams $it.params; $a += (aNotif $p["id"] $it.payload $p["target"]) }
            "SAY" {
                $p = ParseParams $it.params
                $mode = if ($p.ContainsKey("mode")) { $p["mode"] } elseif ($tapAdvance -and $i -eq $lastSay) { "pause" } else { "instant" }
                $a += (aSay2 $it.payload $mode $p["align"] $p["duration"])
            }
            "CHOICE" { $a += (BuildChoice $items) }
            default { }
        }
    }
    if ($exit -and $exit.type -eq "goto") { $a += (aGoto $exit.target) }
    elseif ($exit -and $exit.type -eq "end") { $a += (aEnd) }
    $pages[$key] = $a
}
function BuildTimed($key, $items) {
    $blocks = @(); $pendImg = $null; $pendNotifs = @(); $exit = $null
    foreach ($it in $items) {
        switch ($it.kind) {
            "IMAGE" { $pendImg = if ($it.payload.Trim() -eq "hold") { $null } else { $it.payload.Trim() } }
            "NOTIFICATION" { $p = ParseParams $it.params; $pendNotifs += (aNotif $p["id"] $it.payload $p["target"]) }
            "METRONOME" { $p = ParseParams $it.params; $blocks += @{ kind = "metro"; bpm = [int]$p["bpm"]; secs = [double]$p["secs"]; img = $pendImg; notifs = $pendNotifs; lines = @() }; $pendImg = $null; $pendNotifs = @() }
            "PAUSE" { $p = ParseParams $it.params; $blocks += @{ kind = "pause"; bpm = 0; secs = [double]$p["secs"]; img = $pendImg; notifs = $pendNotifs; lines = @() }; $pendImg = $null; $pendNotifs = @() }
            "SAY" { if ($blocks.Count -eq 0) { throw "[SAY] before any [METRONOME]/[PAUSE] on page '$key'" }; $blocks[-1].lines += $it.payload }
            "GOTO" { $exit = @{ type = "goto"; target = $it.payload.Trim() } }
            "END" { $exit = @{ type = "end" } }
            default { }
        }
    }
    if (-not $exit) { throw "timed page '$key' has no [GOTO]/[END] exit" }
    $n = $blocks.Count
    for ($i = 0; $i -lt $n; $i++) {
        $b = $blocks[$i]
        $pk = if ($i -eq 0) { $key } else { "$key-$($i+1)" }
        $a = @()
        foreach ($nf in $b.notifs) { $a += $nf }
        if ($b.img) { $a += (aImg $b.img) }
        if ($b.kind -eq "metro") { $a += (aAudio $b.bpm $b.secs) }
        $lines = @($b.lines)
        if ($lines.Count -eq 0) { $a += (aTimer ("{0}s" -f $b.secs) "hidden") }
        else {
            $per = [math]::Round(($b.secs / $lines.Count), 1); if ($per -le 0) { $per = 1 }
            foreach ($ln in $lines) { $a += (aSay2 $ln "instant" $null $null); $a += (aTimer ("{0}s" -f $per) "hidden") }
        }
        if ($i -lt $n - 1) { $a += (aGoto "$key-$($i+2)") }
        elseif ($exit.type -eq "end") { $a += (aEnd) }
        else { $a += (aGoto $exit.target) }   # target may equal $key -> loop
        $pages[$pk] = $a
    }
}
function DispatchPage($key, $items) {
    $timed = @($items | Where-Object { $_.kind -eq "METRONOME" -or $_.kind -eq "PAUSE" }).Count -gt 0
    if ($timed) { BuildTimed $key $items } else { BuildSimple $key $items }
}

# ---- parse script.md ----
$rx = [regex]'^\[(\w+)(?:\s*\(([^)]*)\))?\s*(?::\s*(.*?))?\]'
$known = "PAGE", "IMAGE", "SAY", "METRONOME", "PAUSE", "AUDIO", "NOTIFICATION", "CHOICE", "OPTION", "GOTO", "END"
$curKey = $null; $curItems = @(); $order = @()
foreach ($line in [IO.File]::ReadAllLines("$base\script.md", [Text.Encoding]::UTF8)) {
    $m = $rx.Match($line)
    if (-not $m.Success) { continue }
    $kw = $m.Groups[1].Value.ToUpper()
    if ($known -notcontains $kw) { continue }
    $params = $m.Groups[2].Value
    $payload = $m.Groups[3].Value
    if ($kw -eq "PAGE") {
        if ($curKey) { DispatchPage $curKey $curItems }
        $curKey = $payload.Trim(); $curItems = @(); $order += $curKey
    }
    else {
        if (-not $curKey) { continue }
        $curItems += @{ kind = $kw; params = $params; payload = $payload }
    }
}
if ($curKey) { DispatchPage $curKey $curItems }

# ---- assemble (reuse stub galleries/files/editor) ----
$teaseJsonPath = "$base\tease.json"
$tease = $js.DeserializeObject([IO.File]::ReadAllText($teaseJsonPath, [Text.Encoding]::UTF8))
$tease["pages"] = $pages
$tease["init"] = ""
$tease["modules"] = [ordered]@{ audio = [ordered]@{}; notification = [ordered]@{} }
$out = $js.Serialize($tease)
[IO.File]::WriteAllText($teaseJsonPath, $out, (New-Object Text.UTF8Encoding($false)))

# ---- validate ----
$declared = @($pages.Keys)
$used = [regex]::Matches($out, '"target":"([^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$missing = $used | Where-Object { $declared -notcontains $_ }
Write-Host "Tease                   : $(Split-Path $base -Leaf)"
Write-Host "Logical pages in script : $($order.Count)"
Write-Host "EOS pages generated     : $($declared.Count)"
Write-Host "Distinct nav targets    : $($used.Count)"
if ($declared -notcontains "start") { Write-Host "ERROR: no start page" } else { Write-Host "start page: present" }
if ($missing) { Write-Host "MISSING TARGETS:"; $missing | ForEach-Object { Write-Host "  $_" } } else { Write-Host "All goto/button targets resolve to a page." }
Write-Host "image locators: $((([regex]::Matches($out,'gallery:')).Count)) ; audio locators: $((([regex]::Matches($out,'file:metronome')).Count))"
Write-Host "Wrote $teaseJsonPath ($([math]::Round($out.Length/1kb,1)) KB)"
