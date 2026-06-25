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
  A marker may span several lines (the closing ']' on its own line); for [SAY] each source line
  becomes one visual line of the SAME say (joined with <br>), e.g.
    [SAY: first line
    second line
    ]
  A page with any [METRONOME]/[PAUSE] is timed: each block becomes its own EOS page (one tempo per
  page); the [SAY]s after a block are revealed evenly across secs; an [IMAGE]/[NOTIFICATION] before
  a block attaches to it; the page's [GOTO]/[END] applies after the last block; [GOTO] to the page's
  own key = loop. Image filenames resolve to locators via asset-map.json.

  Notifications are page-scoped automatically: a post-assembly pass injects notification.remove for
  every notification a page created when navigating to a page that does NOT re-declare that same id
  (Milovana otherwise keeps a notification visible across page switches until explicitly removed).

  Diagnostics: every problem found while parsing/building is reported with the offending script.md
  line number (see the "Diagnostics" section printed at the end). Hard ERRORs (bad/missing image
  locators, missing or non-numeric params, structural problems, unresolved nav targets, missing
  metronome audio, no start page) abort the build WITHOUT overwriting tease.json, so a broken script
  never silently produces a broken tease. WARNINGs (e.g. unknown keyword/parameter, orphaned action)
  are surfaced but do not block the write. Exit code is 1 when any ERROR was reported.
#>
param(
    [Parameter(Mandatory = $true)][string]$TeaseDir
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Web.Extensions
$base = (Resolve-Path -LiteralPath $TeaseDir).Path
$js = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$js.MaxJsonLength = [int]::MaxValue

# ---- diagnostics ----
# Every problem is collected here (rather than thrown) so the build reports ALL issues in one pass,
# each tied to the originating script.md line, instead of dying on the first one with a bare stack
# trace. $script: scope so the action builders below can append from inside function calls.
$script:diags = New-Object System.Collections.Generic.List[object]
function AddDiag($severity, $line, $msg) {
    $script:diags.Add([pscustomobject]@{ Severity = $severity; Line = [int]$line; Message = $msg })
}
function Err($line, $msg) { AddDiag "ERROR" $line $msg }
function Warn($line, $msg) { AddDiag "WARN" $line $msg }

# Param helpers: report (with line) instead of letting [int]$null / [double]"abc" silently coerce or
# throw. Return $null on failure so callers can guard. Use ($null -ne $x) to test, since 0 is valid.
function ReqStr($p, $name, $line, $ctx) {
    if (-not $p.ContainsKey($name) -or [string]::IsNullOrWhiteSpace([string]$p[$name])) { Err $line "$ctx missing required parameter '$name='"; return $null }
    [string]$p[$name]
}
function ReqInt($p, $name, $line, $ctx) {
    if (-not $p.ContainsKey($name) -or [string]::IsNullOrWhiteSpace([string]$p[$name])) { Err $line "$ctx missing required parameter '$name='"; return $null }
    $r = 0
    if ([int]::TryParse([string]$p[$name], [ref]$r)) { return $r }
    Err $line "$ctx parameter '$name=' is not a whole number: '$($p[$name])'"; return $null
}
function ReqDouble($p, $name, $line, $ctx) {
    if (-not $p.ContainsKey($name) -or [string]::IsNullOrWhiteSpace([string]$p[$name])) { Err $line "$ctx missing required parameter '$name='"; return $null }
    $r = 0.0
    if ([double]::TryParse([string]$p[$name], [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$r)) { return $r }
    Err $line "$ctx parameter '$name=' is not a number: '$($p[$name])'"; return $null
}
# Recognised parameter names per keyword; anything else is a likely typo (e.g. alignment= for align=).
$script:knownParams = @{
    SAY          = @("mode", "align", "duration")
    METRONOME    = @("bpm", "secs")
    PAUSE        = @("secs")
    AUDIO        = @("bpm", "loops")
    NOTIFICATION = @("id", "target")
    OPTION       = @("target", "color")
}
function WarnUnknownParams($kw, $p, $line) {
    if (-not $script:knownParams.ContainsKey($kw)) { return }
    $allowed = $script:knownParams[$kw]
    foreach ($k in $p.Keys) {
        if ($allowed -notcontains $k) { Warn $line "[$kw] unknown parameter '$k=' (expected one of: $($allowed -join ', '))" }
    }
}
# A parameter segment with no '=' (e.g. the typo "mode-pause" instead of "mode=pause") is silently
# dropped by ParseParams, so the intended param would vanish with no effect and no trace. Flag each
# such malformed segment from the RAW param string (ParseParams has already discarded it by the time
# WarnUnknownParams sees the hash), tying it to the offending script.md line.
function WarnMalformedParams($kw, $rawParams, $line) {
    if ([string]::IsNullOrWhiteSpace($rawParams)) { return }
    foreach ($seg in ($rawParams -split ',')) {
        $s = $seg.Trim()
        if ($s -and $s.IndexOf('=') -lt 0) {
            Warn $line "[$kw] malformed parameter '$s' (expected name=value, e.g. 'mode=pause'); it was ignored"
        }
    }
}
# Tags Milovana actually renders in a say label (everything else is silently dropped on Milovana,
# e.g. <color=…> / <font> / <b> / <code>). Flag the rest so a typo'd format is caught at build time.
$script:sayAllowedTags = @("p", "br", "strong", "em", "u", "span")
function WarnUnsupportedSayTags($payload, $line) {
    $seen = @{}
    foreach ($mm in [regex]::Matches($payload, '</?\s*([A-Za-z][A-Za-z0-9]*)')) {
        $tag = $mm.Groups[1].Value.ToLower()
        if ($script:sayAllowedTags -notcontains $tag -and -not $seen.ContainsKey($tag)) {
            $seen[$tag] = $true
            Warn $line "[SAY] uses tag '<$tag>' which Milovana does not render (only <strong>/<em>/<u>/<a> and <span style=`"color: #RRGGBB`">). For colored text use <span style=`"color: #RRGGBB`">text</span>."
        }
    }
}

# ---- locator lookup ----
$mapRaw = $js.DeserializeObject([IO.File]::ReadAllText("$base\asset-map.json", [Text.Encoding]::UTF8))
$loc = @{}
foreach ($g in $mapRaw["galleries"].Keys) {
    $imgs = $mapRaw["galleries"][$g]["images"]
    foreach ($f in $imgs.Keys) { $loc["$g/$f"] = $imgs[$f]["locator"] }
}
# Known metronome audio files (file:<name>.mp3) actually present in the tease's Files folder, so a
# [METRONOME]/[AUDIO] bpm that has no matching mp3 is caught instead of producing a dead locator.
$filesDir = Join-Path $base "Files"
$availableFiles = @{}
if (Test-Path -LiteralPath $filesDir) {
    foreach ($fi in (Get-ChildItem -LiteralPath $filesDir -File)) { $availableFiles[$fi.Name] = $true }
}

# ---- action builders ----
# Note: Milovana strips layout CSS from style="" (only `color:` survives -- display:inline-block and
# text-align were confirmed stripped on 2026-06-23), so there is no way to center a say as a group
# while left-aligning its lines. Use align=left for a left-aligned list.
function aSay2($payload, $mode, $align, $duration) {
    $s = [ordered]@{ label = ("<p>" + $payload + "</p>") }
    if ($mode) { $s["mode"] = $mode }
    if ($align) { $s["align"] = $align }
    if ($duration) { $s["duration"] = $duration }
    [ordered]@{ say = $s }
}
# Returns $null (and records an ERROR at $line) when the image has no locator, so callers guard the
# append rather than emitting an image action with a missing locator.
function aImg($k, $line) {
    if (-not $loc.ContainsKey($k)) { Err $line "no locator for image '$k' (not found in asset-map.json)"; return $null }
    [ordered]@{ image = [ordered]@{ locator = $loc[$k] } }
}
function aTimer($d, $st) { [ordered]@{ timer = [ordered]@{ duration = $d; style = $st } } }
function aGoto($t) { [ordered]@{ goto = [ordered]@{ target = $t } } }
function aEnd() { [ordered]@{ end = [ordered]@{} } }
# $line is used to verify the referenced metronome mp3 exists in Files/.
function aAudio($bpm, $secs, $line) {
    $f = ("metronome-{0:000}bpm.mp3" -f [int]$bpm)
    if ($availableFiles.Count -gt 0 -and -not $availableFiles.ContainsKey($f)) { Err $line "metronome audio '$f' (bpm=$bpm) not found in Files/" }
    $lp = [int]([math]::Ceiling($secs / 3.0) + 1)
    [ordered]@{ "audio.play" = [ordered]@{ locator = "file:$f"; volume = 1.0; loops = $lp; background = $false } }
}
function aAudioN($bpm, $loops, $line) {
    $f = ("metronome-{0:000}bpm.mp3" -f [int]$bpm)
    if ($availableFiles.Count -gt 0 -and -not $availableFiles.ContainsKey($f)) { Err $line "metronome audio '$f' (bpm=$bpm) not found in Files/" }
    [ordered]@{ "audio.play" = [ordered]@{ locator = "file:$f"; volume = 1.0; loops = [int]$loops; background = $false } }
}
function aNotif($id, $lbl, $tgt) {
    [ordered]@{ "notification.create" = [ordered]@{ id = $id; buttonLabel = $lbl; buttonCommands = @( (aGoto $tgt) ) } }
}
function aNotifRemove($id) { [ordered]@{ "notification.remove" = [ordered]@{ id = $id } } }
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
# Preserve authored spacing inside a SAY line. HTML collapses tabs and runs of spaces, so to keep
# manual indentation/alignment we convert the whitespace the author clearly intended into &nbsp;
# (non-breaking spaces, which do not collapse): a tab -> 4 nbsp anywhere, leading indentation ->
# one nbsp per space, and any interior run of 2+ spaces -> that many nbsp. Single spaces between
# words are left as normal spaces so the text can still wrap naturally.
$script:nbspTabWidth = 4
function PreserveSpacing($line) {
    $line = $line -replace "`t", ("&nbsp;" * $script:nbspTabWidth)
    $line = [regex]::Replace($line, '^ +', { param($m) "&nbsp;" * $m.Value.Length })
    [regex]::Replace($line, ' {2,}', { param($m) "&nbsp;" * $m.Value.Length })
}
# A [SAY] may span several script.md lines (the closing ']' on its own line); each source line
# becomes one visual line within the *single* say, joined with <br>. INTERIOR blank lines are kept:
# an authored empty line becomes an extra <br> (i.e. a visible empty line for vertical spacing), and
# two blank lines give two -- so spacing is authorable. Only LEADING/TRAILING blank lines are trimmed
# -- in particular the empty segment created by the closing ']' on its own line, which is structural,
# not authored content. Leading, interior AND trailing space/tab runs on non-blank lines are kept (as
# &nbsp; via PreserveSpacing), so padding at the END of a line aligns just like leading indentation.
# Note: the FIRST line's leading whitespace is eaten by the [SAY: \s* marker, so indent via the others.
function NormalizeSay($payload) {
    if ($payload -notmatch "`n") { return (PreserveSpacing $payload) }
    $raw = @($payload -split "`n")
    # Blank-ness is decided on the RAW line (before PreserveSpacing, which would turn a spaces-only
    # line into &nbsp; and so no longer read as blank). Trim only the outer blank lines; keep inner.
    $start = 0; $end = $raw.Count - 1
    while ($start -le $end -and $raw[$start].Trim() -eq "") { $start++ }
    while ($end -ge $start -and $raw[$end].Trim() -eq "") { $end-- }
    $parts = @()
    for ($i = $start; $i -le $end; $i++) {
        if ($raw[$i].Trim() -eq "") { $parts += "" } else { $parts += (PreserveSpacing $raw[$i]) }
    }
    $parts -join "<br>"
}

$pages = [ordered]@{}

function BuildChoice($key, $items) {
    $opts = @()
    foreach ($it in $items) {
        if ($it.kind -eq "OPTION") {
            $p = ParseParams $it.params
            WarnUnknownParams "OPTION" $p $it.line
            $tgt = ReqStr $p "target" $it.line "[OPTION]"
            if ([string]::IsNullOrWhiteSpace($it.payload)) { Err $it.line "[OPTION] has no label text" }
            $opts += @{ l = $it.payload; t = $tgt; c = $p["color"] }
        }
    }
    if ($opts.Count -eq 0) { Err $key.line "[CHOICE] on page '$($key.name)' has no [OPTION]s" }
    aChoice $opts
}
function BuildSimple($key, $items) {
    $exit = $null
    foreach ($it in $items) {
        if ($it.kind -eq "GOTO") {
            $t = $it.payload.Trim()
            if (-not $t) { Err $it.line "[GOTO] has no target page key" }
            $exit = @{ type = "goto"; target = $t }
        }
        elseif ($it.kind -eq "END") { $exit = @{ type = "end" } }
    }
    $hasChoice = @($items | Where-Object { $_.kind -eq "CHOICE" }).Count -gt 0
    if (-not $exit -and -not $hasChoice) { Warn $key.line "page '$($key.name)' has no [GOTO]/[END]/[CHOICE] exit (it will dead-end)" }
    $lastSay = -1; for ($i = 0; $i -lt $items.Count; $i++) { if ($items[$i].kind -eq "SAY") { $lastSay = $i } }
    $tapAdvance = ($exit -and $exit.type -eq "goto" -and -not $hasChoice)
    $a = @()
    for ($i = 0; $i -lt $items.Count; $i++) {
        $it = $items[$i]
        switch ($it.kind) {
            "IMAGE" { if ($it.payload.Trim() -ne "hold") { $img = aImg $it.payload.Trim() $it.line; if ($img) { $a += $img } } }
            "AUDIO" {
                $p = ParseParams $it.params
                WarnUnknownParams "AUDIO" $p $it.line
                $bpm = ReqInt $p "bpm" $it.line "[AUDIO]"
                $loops = ReqInt $p "loops" $it.line "[AUDIO]"
                if ($null -ne $bpm -and $null -ne $loops) { $a += (aAudioN $bpm $loops $it.line) }
            }
            "NOTIFICATION" {
                $p = ParseParams $it.params
                WarnUnknownParams "NOTIFICATION" $p $it.line
                $id = ReqStr $p "id" $it.line "[NOTIFICATION]"
                $tgt = ReqStr $p "target" $it.line "[NOTIFICATION]"
                if ([string]::IsNullOrWhiteSpace($it.payload)) { Err $it.line "[NOTIFICATION] has no button label text" }
                if ($null -ne $id -and $null -ne $tgt) { $a += (aNotif $id $it.payload $tgt) }
            }
            "SAY" {
                $p = ParseParams $it.params
                WarnUnknownParams "SAY" $p $it.line
                $mode = if ($p.ContainsKey("mode")) { $p["mode"] } elseif ($tapAdvance -and $i -eq $lastSay) { "pause" } else { "instant" }
                $a += (aSay2 (NormalizeSay $it.payload) $mode $p["align"] $p["duration"])
            }
            "CHOICE" { $a += (BuildChoice $key $items) }
            default { }
        }
    }
    if ($exit -and $exit.type -eq "goto") { $a += (aGoto $exit.target) }
    elseif ($exit -and $exit.type -eq "end") { $a += (aEnd) }
    $pages[$key.name] = $a
}
function BuildTimed($key, $items) {
    $blocks = @(); $pendImg = $null; $pendImgLine = 0; $pendNotifs = @(); $exit = $null
    foreach ($it in $items) {
        switch ($it.kind) {
            "IMAGE" { if ($it.payload.Trim() -eq "hold") { $pendImg = $null } else { $pendImg = $it.payload.Trim(); $pendImgLine = $it.line } }
            "NOTIFICATION" {
                $p = ParseParams $it.params
                WarnUnknownParams "NOTIFICATION" $p $it.line
                $id = ReqStr $p "id" $it.line "[NOTIFICATION]"
                $tgt = ReqStr $p "target" $it.line "[NOTIFICATION]"
                if ([string]::IsNullOrWhiteSpace($it.payload)) { Err $it.line "[NOTIFICATION] has no button label text" }
                if ($null -ne $id -and $null -ne $tgt) { $pendNotifs += (aNotif $id $it.payload $tgt) }
            }
            "METRONOME" {
                $p = ParseParams $it.params
                WarnUnknownParams "METRONOME" $p $it.line
                $bpm = ReqInt $p "bpm" $it.line "[METRONOME]"
                $secs = ReqDouble $p "secs" $it.line "[METRONOME]"
                if ($null -eq $bpm) { $bpm = 0 }; if ($null -eq $secs) { $secs = 0.0 }
                $blocks += @{ kind = "metro"; bpm = $bpm; secs = $secs; img = $pendImg; imgLine = $pendImgLine; notifs = $pendNotifs; lines = @(); line = $it.line }
                $pendImg = $null; $pendNotifs = @()
            }
            "PAUSE" {
                $p = ParseParams $it.params
                WarnUnknownParams "PAUSE" $p $it.line
                $secs = ReqDouble $p "secs" $it.line "[PAUSE]"
                if ($null -eq $secs) { $secs = 0.0 }
                $blocks += @{ kind = "pause"; bpm = 0; secs = $secs; img = $pendImg; imgLine = $pendImgLine; notifs = $pendNotifs; lines = @(); line = $it.line }
                $pendImg = $null; $pendNotifs = @()
            }
            "SAY" {
                if ($blocks.Count -eq 0) { Err $it.line "[SAY] before any [METRONOME]/[PAUSE] on page '$($key.name)'" }
                else { $blocks[-1].lines += (NormalizeSay $it.payload) }
            }
            "GOTO" {
                $t = $it.payload.Trim()
                if (-not $t) { Err $it.line "[GOTO] has no target page key" }
                $exit = @{ type = "goto"; target = $t }
            }
            "END" { $exit = @{ type = "end" } }
            default { }
        }
    }
    if (-not $exit) { Err $key.line "timed page '$($key.name)' has no [GOTO]/[END] exit"; $exit = @{ type = "end" } }
    $n = $blocks.Count
    for ($i = 0; $i -lt $n; $i++) {
        $b = $blocks[$i]
        $pk = if ($i -eq 0) { $key.name } else { "$($key.name)-$($i+1)" }
        $a = @()
        foreach ($nf in $b.notifs) { $a += $nf }
        if ($b.img) { $img = aImg $b.img $b.imgLine; if ($img) { $a += $img } }
        if ($b.kind -eq "metro") { $a += (aAudio $b.bpm $b.secs $b.line) }
        $lines = @($b.lines)
        if ($lines.Count -eq 0) { $a += (aTimer ("{0}s" -f $b.secs) "hidden") }
        else {
            $per = [math]::Round(($b.secs / $lines.Count), 1); if ($per -le 0) { $per = 1 }
            foreach ($ln in $lines) { $a += (aSay2 $ln "instant" $null $null); $a += (aTimer ("{0}s" -f $per) "hidden") }
        }
        if ($i -lt $n - 1) { $a += (aGoto "$($key.name)-$($i+2)") }
        elseif ($exit.type -eq "end") { $a += (aEnd) }
        else { $a += (aGoto $exit.target) }   # target may equal $key.name -> loop
        $pages[$pk] = $a
    }
}
function DispatchPage($key, $items) {
    $timed = @($items | Where-Object { $_.kind -eq "METRONOME" -or $_.kind -eq "PAUSE" }).Count -gt 0
    if ($timed) { BuildTimed $key $items } else { BuildSimple $key $items }
}

# ---- parse script.md ----
# (?s): a marker's payload may span multiple source lines (see CoalesceMarkers below), so '.' must
# match newlines; the non-greedy (.*?) still stops at the first ']'.
# After the ':' separator we consume at most ONE space (': ?', not ':\s*') so that any *extra*
# leading whitespace on a SAY's first line is kept as indentation (PreserveSpacing turns it into
# &nbsp;), matching how continuation lines behave. Keywords that .Trim() their payload (PAGE, IMAGE,
# GOTO, OPTION, …) are unaffected.
$rx = [regex]'(?s)^\[(\w+)(?:\s*\(([^)]*)\))?\s*(?:: ?(.*?))?\]'
$known = "PAGE", "IMAGE", "SAY", "METRONOME", "PAUSE", "AUDIO", "NOTIFICATION", "CHOICE", "OPTION", "GOTO", "END"

# Merge multi-line markers into single logical records. A marker line that opens with '[' but has no
# ']' yet is continued onto the following lines until the line that closes it (the closing ']' may be
# on its own line). Each record keeps the *starting* line number for diagnostics. Lines that aren't
# unclosed markers (prose, or normal single-line markers that already contain ']') pass through 1:1.
function CoalesceMarkers($lines) {
    $recs = New-Object System.Collections.Generic.List[object]
    $i = 0
    while ($i -lt $lines.Count) {
        $line = $lines[$i]; $startNo = $i + 1
        if ($line.StartsWith("[") -and $line.IndexOf("]") -lt 0) {
            $buf = $line; $j = $i + 1
            while ($j -lt $lines.Count -and $lines[$j].IndexOf("]") -lt 0) { $buf += "`n" + $lines[$j]; $j++ }
            if ($j -lt $lines.Count) { $buf += "`n" + $lines[$j]; $i = $j + 1 }
            else { Err $startNo "marker '[' is never closed with ']' before end of file"; $i = $j }
            $recs.Add([pscustomobject]@{ text = $buf; line = $startNo })
        }
        else { $recs.Add([pscustomobject]@{ text = $line; line = $startNo }); $i++ }
    }
    $recs
}

$cur = $null; $curItems = @(); $order = @(); $pageLines = @{}
# Records each user-authored nav target with its line, so an unresolved target can be reported at the
# exact [GOTO]/[OPTION]/[NOTIFICATION] that referenced it.
$navRefs = New-Object System.Collections.Generic.List[object]
$allLines = [IO.File]::ReadAllLines("$base\script.md", [Text.Encoding]::UTF8)
foreach ($rec in (CoalesceMarkers $allLines)) {
    $line = $rec.text
    $lineNo = $rec.line
    $m = $rx.Match($line)
    if (-not $m.Success) {
        # A line that opens with '[' at column 0 but doesn't parse is almost always a typo'd marker
        # (e.g. a missing ']' or a stray space) rather than prose, so flag it instead of dropping it.
        if ($line -match '^\[' -and $line -notmatch '^\[Author note') { Warn $lineNo "line looks like a marker but did not parse; ignored: $(($line -split "`n")[0].Trim())" }
        continue
    }
    $kw = $m.Groups[1].Value.ToUpper()
    if ($known -notcontains $kw) { Warn $lineNo "unknown keyword '[$kw]'; line ignored"; continue }
    $params = $m.Groups[2].Value
    $payload = $m.Groups[3].Value
    WarnMalformedParams $kw $params $lineNo
    if ($kw -eq "PAGE") {
        $newKey = $payload.Trim()
        if (-not $newKey) { Err $lineNo "[PAGE] with an empty key" }
        elseif ($order -contains $newKey) { Err $lineNo "duplicate page key '$newKey' (first declared at line $($pageLines[$newKey]))" }
        if ($cur) { DispatchPage $cur $curItems }
        $cur = @{ name = $newKey; line = $lineNo }
        $curItems = @(); $order += $newKey
        if (-not $pageLines.ContainsKey($newKey)) { $pageLines[$newKey] = $lineNo }
    }
    else {
        if (-not $cur) { Warn $lineNo "[$kw] appears before the first [PAGE]; ignored"; continue }
        $curItems += @{ kind = $kw; params = $params; payload = $payload; line = $lineNo }
        if ($kw -eq "SAY") { WarnUnsupportedSayTags $payload $lineNo }
        # Capture user-authored nav targets for the unresolved-target check below.
        if ($kw -eq "GOTO" -and $payload.Trim()) { $navRefs.Add([pscustomobject]@{ target = $payload.Trim(); line = $lineNo }) }
        elseif ($kw -eq "OPTION" -or $kw -eq "NOTIFICATION") {
            $pp = ParseParams $params
            if ($pp.ContainsKey("target") -and $pp["target"]) { $navRefs.Add([pscustomobject]@{ target = $pp["target"]; line = $lineNo }) }
        }
    }
}
if ($cur) { DispatchPage $cur $curItems }

if ($order.Count -eq 0) { Err 0 "no [PAGE] markers found in script.md" }

# ---- notification scoping: auto-remove page-scoped notifications on navigation ----
# Milovana notifications persist across page switches until explicitly removed (there is no implicit
# clear on a page change). Treat each notification as scoped to the page that created it: whenever the
# player navigates to a page that does NOT re-declare the same id, emit a notification.remove just
# before that navigation. A destination that re-declares the id keeps it (no remove), so a self-loop
# like tease-16 doesn't flicker. Without this, a notification (e.g. "Supported Devices" on 003-need,
# or the climax/mercy buttons in the tease-16 loop) would linger onto later pages.
$pageNotifs = @{}
foreach ($pn in @($pages.Keys)) {
    $ids = New-Object System.Collections.Generic.List[string]
    foreach ($act in $pages[$pn]) {
        if ($act.Contains("notification.create")) { $ids.Add([string]$act["notification.create"]["id"]) }
    }
    $pageNotifs[$pn] = $ids
}
# The navigation target of a command list is its last goto (aGoto/aNotif emit exactly one).
function NotifTarget($commands) {
    $t = $null
    foreach ($c in $commands) { if ($c.Contains("goto")) { $t = [string]$c["goto"]["target"] } }
    $t
}
# notification.remove actions for each created id the destination page does NOT re-declare. A null /
# unknown / wildcard target re-declares nothing, so all are removed (acceptable; no such case here).
function RemovesFor($createdIds, $targetPage) {
    $r = @()
    if ($createdIds.Count -eq 0) { return $r }
    $keep = if ($targetPage -and $pageNotifs.ContainsKey($targetPage)) { $pageNotifs[$targetPage] } else { @() }
    foreach ($id in $createdIds) { if ($keep -notcontains $id) { $r += (aNotifRemove $id) } }
    $r
}
foreach ($pn in @($pages.Keys)) {
    $created = $pageNotifs[$pn]
    if ($created.Count -eq 0) { continue }
    $newActions = @()
    foreach ($act in $pages[$pn]) {
        if ($act.Contains("goto")) {
            $newActions += (RemovesFor $created ([string]$act["goto"]["target"]))
            $newActions += $act
        }
        elseif ($act.Contains("end")) {
            $newActions += (RemovesFor $created $null)   # tease ends -> clear everything this page made
            $newActions += $act
        }
        elseif ($act.Contains("choice")) {
            foreach ($opt in $act["choice"]["options"]) {
                $tgt = NotifTarget $opt["commands"]
                $opt["commands"] = @(RemovesFor $created $tgt) + @($opt["commands"])
            }
            $newActions += $act
        }
        elseif ($act.Contains("notification.create")) {
            # $created includes this button's own id plus any sibling notifs on the page, so a click
            # removes itself and any sibling the destination doesn't continue.
            $nc = $act["notification.create"]
            $tgt = NotifTarget $nc["buttonCommands"]
            $nc["buttonCommands"] = @(RemovesFor $created $tgt) + @($nc["buttonCommands"])
            $newActions += $act
        }
        else { $newActions += $act }
    }
    $pages[$pn] = $newActions
}

# ---- assemble (reuse stub galleries/files/editor) ----
$teaseJsonPath = "$base\tease.json"
$tease = $js.DeserializeObject([IO.File]::ReadAllText($teaseJsonPath, [Text.Encoding]::UTF8))
$tease["pages"] = $pages
$tease["init"] = ""
$tease["modules"] = [ordered]@{ audio = [ordered]@{}; notification = [ordered]@{} }
$out = $js.Serialize($tease)

# ---- validate navigation ----
$declared = @($pages.Keys)
$used = [regex]::Matches($out, '"target":"([^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($t in $used) {
    if ($declared -notcontains $t) {
        $refLine = ($navRefs | Where-Object { $_.target -eq $t } | Select-Object -First 1 -ExpandProperty line)
        if (-not $refLine) { $refLine = 0 }
        Err $refLine "nav target '$t' does not resolve to any page"
    }
}
if ($declared -notcontains "start") { Err 0 "no 'start' page (the first logical page must be [PAGE: start])" }

# ---- report diagnostics ----
$errs = @($script:diags | Where-Object { $_.Severity -eq "ERROR" })
$warns = @($script:diags | Where-Object { $_.Severity -eq "WARN" })
Write-Host ""
Write-Host "Tease                   : $(Split-Path $base -Leaf)"
Write-Host "Logical pages in script : $($order.Count)"
Write-Host "EOS pages generated     : $($declared.Count)"
Write-Host "Distinct nav targets    : $($used.Count)"
Write-Host "image locators          : $((([regex]::Matches($out,'gallery:')).Count)) ; audio locators: $((([regex]::Matches($out,'file:metronome')).Count))"

if ($script:diags.Count -gt 0) {
    Write-Host ""
    Write-Host "Diagnostics ($($errs.Count) error(s), $($warns.Count) warning(s)) -- line numbers are script.md:"
    foreach ($d in ($script:diags | Sort-Object @{ Expression = { $_.Line } }, @{ Expression = { $_.Severity } })) {
        $where = if ($d.Line -gt 0) { "script.md:$($d.Line)" } else { "script.md" }
        $tag = if ($d.Severity -eq "ERROR") { "ERROR" } else { "WARN " }
        $color = if ($d.Severity -eq "ERROR") { "Red" } else { "Yellow" }
        Write-Host ("  {0}  {1,-16} {2}" -f $tag, $where, $d.Message) -ForegroundColor $color
    }
}

if ($errs.Count -gt 0) {
    Write-Host ""
    Write-Host "Build FAILED: $($errs.Count) error(s). tease.json was NOT modified -- fix the lines above and re-run." -ForegroundColor Red
    exit 1
}

# ---- write (only when no errors) ----
[IO.File]::WriteAllText($teaseJsonPath, $out, (New-Object Text.UTF8Encoding($false)))
Write-Host ""
if ($warns.Count -gt 0) { Write-Host "Build succeeded with $($warns.Count) warning(s)." -ForegroundColor Yellow }
else { Write-Host "Build succeeded, no issues." -ForegroundColor Green }
Write-Host "Wrote $teaseJsonPath ($([math]::Round($out.Length/1kb,1)) KB)"
