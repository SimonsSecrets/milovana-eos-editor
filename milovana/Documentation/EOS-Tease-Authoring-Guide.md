# Milovana EOS Tease Authoring Guide

A reference for hand-writing Milovana **EOS** teases in JSON so they can be uploaded into the
EOS editor via its backup/restore function.

This guide is derived **strictly** from the worked example
[`teachingmaterial-2026-06-22.json`](teachingmaterial-2026-06-22.json), which demonstrates the
most commonly used actions. In that example, every action is preceded by a `say` action that
explains the action that follows it — this guide distills those explanations into a structured
reference. Only the actions and fields that actually appear in that example are documented as
fact; anything narrated but not concretely shown is collected under **§8 Open questions**
rather than guessed at.

---

## 1. Overview

A tease is a single JSON document. The player walks through **pages**, and each page runs an
ordered list of **actions** (show text, show an image, wait on a timer, branch on a choice,
play audio, jump to another page, end).

### Top-level shape

```json
{
  "pages": { "...": [ ...actions... ] },
  "init": "",
  "modules": { "audio": {} },
  "files": { "...": { ... } },
  "galleries": { "...": { ... } },
  "editor": { ... }
}
```

| Key         | Type   | Purpose |
|-------------|--------|---------|
| `pages`     | object | The tease itself: named pages, each an array of actions. **Required.** |
| `init`      | string | Empty in the example. See §8. |
| `modules`   | object | Feature toggles. `audio` must be present to use `audio.play`. |
| `files`     | object | Manifest of uploaded non-image files (e.g. audio), keyed by filename. |
| `galleries` | object | Manifest of uploaded image galleries, keyed by gallery UUID. |
| `editor`    | object | Editor-only convenience state (e.g. recently used images). Not needed for playback. |

### Round-tripping with the editor

The EOS editor is at <https://milovana.com/eos/editor/teases>. Upload a JSON via the editor's
**backup/restore** function. To inspect an existing published tease, change `showtease` to
`geteosscript` in its URL to download its JSON. (See [`Instructions.md`](Instructions.md).)

### 🔔 End-to-end workflow for a new (asset-backed) tease

This is the agreed process for building a real, script-driven tease. **Claude must surface this
when a new tease starts** — especially the **themed-gallery-buckets** recommendation in step 3 and
the fact that no `image`/`audio.play` locator resolves until steps 4–6 are done. (Asset mechanics
referenced below are detailed in §5.)

| # | Who        | Step                                                                                                                                                                                                                                                                                                                                                                               |
|---|------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1 | user       | **Local folder** — create `milovana/Teases/<TeaseName>/`.                                                                                                                                                                                                                                                                                                                          |
| 2 | both       | **Script** — write `script.md`: tone, pages, pacing, `[METRONOME]`/`[PAUSE]` markers, `[IMAGE: …]` image-placement markers (see below), author notes.                                                                                                                                                                                                                              |
| 3 | **Claude** | **Asset plan** — read the script and propose **(a)** the **themed gallery buckets** it needs (one gallery per mood/pace, e.g. `solo-sensual`, `machine-soft`, `machine-hard`, `climax`) plus any hard constraints (e.g. *tutorial = solo only*), and **(b)** the metronome/audio files the `[METRONOME]` markers call for. **Raise the themed-buckets approach here** (§5.3). |
| 4 | user       | **Asset setup** — create local `Gallery/<bucket>/` folders + `Files/`, add exact-byte sources; in the editor create matching galleries (folder name = gallery name) and upload **images *and* audio**.                                                                                                                                                                             |
| 5 | user       | **Export stub** — export the (stub) tease JSON into `tease.json`; it carries the `galleries`/`files` manifest.                                                                                                                                                                                                                                                                     |
| 6 | **Claude** | **Build map** — generate `asset-map.json` (SHA-1 join; §5.2).                                                                                                                                                                                                                                                                                                                      |
| 7 | **Claude** | **Vision tagging** — view each image, write `asset-content.json` (§5.3).                                                                                                                                                                                                                                                                                                           |
| 8 | user       | **Verify tags** — review/adjust the tags.                                                                                                                                                                                                                                                                                                                                          |
| 9 | **Claude** | **Generate tease** — author the full `tease.json` from the script: select images by joining `asset-content` tags ↔ `asset-map` locators (match pace to BPM), declare the `audio` module, add `audio.play` for `[METRONOME]` blocks.                                                                                                                                           |
| 10 | user       | **Upload & verify** — upload `tease.json`, play through.                                                                                                                                                                                                                                                                                                                           |
| 11 | both       | **Iterate** — refine on feedback until the result is right.                                                                                                                                                                                                                                                                                                                        |

### Script markers (`script.md`)

`script.md` is the human-readable source the tease is generated from. Bracketed markers carry
authoring intent and are **never shown to the player**:

| Marker | Meaning |
|--------|---------|
| `[METRONOME: ~N BPM, ~Ns]` | A timed block where the machine moves at ~N BPM for ~N seconds. Becomes a one-tempo-per-page `audio.play` + `timer` (§3.6). Keep N a multiple of 10 so it maps to a pre-generated metronome file. |
| `[PAUSE: ~Ns]` | Silence — the machine is still (a `timer` with no audio). Covers both mid-scene stops and anticipation holds. |
| `[IMAGE: <bucket>/*]` | Show a **random** image from themed gallery bucket `<bucket>`; intensity follows the page's BPM band. The default for most pages — leans on themed buckets (§5.3) instead of per-image picks. |
| `[IMAGE: <bucket>/hero — desc]` | A **specific** hero shot for a key beat (e.g. the climax). Pinned to a real image `id` after tagging (steps 7–9); `desc` tells the tagger which shot. ⚠️ ids are reassigned on every re-upload, so a pinned id breaks if that gallery is re-uploaded. **Robust alternative:** give the guaranteed shot its own **single-image bucket** so `gallery:<bucket>/*` always returns it — deterministic, no id pinning, survives re-uploads. (This is what `climax-hero` does in The Fucking Machine tease.) |
| `[IMAGE: hold]` | Keep the previously shown image (it persists across pages until replaced — §2). Used on practical/menu pages so a sensual backdrop carries over without competing with the copy. |
| `[LOOP START]` / `[LOOP END]`, `[BUTTON: "label" → target]` | Repeat-until-button blocks and player branches. |

> **First page of a branch needs a real `[IMAGE: <bucket>/*]`, not `hold`** — a branch can be
> entered fresh (e.g. from a menu), so there may be no prior image to hold.

---

## 2. Pages & flow model

- `pages` is an object whose **keys are page names**. Allowed characters in a page name:
  **letters, numbers, and a hyphen** (`-`). The example uses names like `start`, `001-Images`,
  `002-Timers`, `099-End`.
- **`start` is the entry page.** Execution begins there.
- Each page's value is an **ordered array of action objects**. Actions run top to bottom.
- **Each action object has exactly one key**, naming the action. The actions used in the
  example are: `say`, `goto`, `image`, `timer`, `choice`, `audio.play`, `end`,
  `notification.create`, `notification.remove`.

### What carries across a page switch

From the example's own narration (`002-Timers`):

- The **last image shown persists** onto the next page until replaced.
- The **on-screen text is cleared** when the page switches — and **only** then. Within a page,
  successive `say` actions **append** (each is a new line below the last); see §3.1.

(Audio persistence across pages is controllable per `audio.play` action — see §3.6.)

---

## 3. Action reference

Each action below lists its parameters and a minimal snippet. An action is always wrapped in an
object under its action key, and actions are listed inside a page array.

### 3.1 `say` — show text

Displays formatted text. If an image is currently shown, the text appears **below** the image;
if a page opens with a `say` before any `image`, the text shows on a black screen.

| Param      | Type   | Values / notes |
|------------|--------|----------------|
| `label`    | string | The text, as **HTML**. Required. |
| `align`    | string | `left`, `center`, `right`. **Defaults to `center`** when omitted. |
| `mode`     | string | Timing. One of `pause`, `instant`, `autoplay`, `custom`. **Auto** is the default — achieved by *omitting* `mode` entirely (there is no `"auto"` value in the JSON). |
| `duration` | string | Only with `mode: "custom"`. A time like `"3s"`. |

**Timing modes** (JSON value → behavior; all confirmed in the example except Auto, which is the omitted default):

| Behavior  | `mode` value     | Meaning |
|-----------|------------------|---------|
| Auto      | *(omit `mode`)*  | Automatically choose the correct behavior based on the next action. |
| Pause     | `"pause"`        | The tease pauses and waits for user interaction. |
| Instant   | `"instant"`      | The tease continues instantly. |
| Auto-Play | `"autoplay"`     | Pauses for the approximate reading time, but the user can click to skip. |
| Custom    | `"custom"`       | Pauses for a custom amount of time; requires the `duration` parameter. |

**Formatting inside `label`** (all seen in the example): wrap text in `<p>…</p>`; inline tags
`<strong>bold</strong>`, `<em>italic</em>`, `<u>underline</u>`, and colored text with
`<span style="color: #1e88e5">…</span>`. Note that literal quotes/apostrophes are HTML-entity
encoded in the source (`&#39;` for `'`).

**Do not use `<code>`** — it does **not** render correctly on Milovana (confirmed). Use
`<strong>`/`<em>` for emphasis instead. Stick to the tags listed above; only those are known to
render.

**Non-blocking after media:** a `say` placed after an `image` or `audio.play` runs immediately
— it does not wait for the image transition or audio clip to finish.

**Text accumulates (append, not replace):** successive `say` actions on the **same page** each add
a **new line below** the previous text — they do **not** replace it. The on-screen text is only
cleared by a **page switch** (§2). This is the intended behavior for narrating over a timed block
(e.g. a metronome segment that reveals a new line every few seconds via `say` → `timer` pairs); if
you instead want a single line shown at a time, put each on its **own page**.

```json
{ "say": { "label": "<p>Hello <strong>there</strong></p>", "align": "left" } }
```

```json
{ "say": { "label": "<p>Pauses for three seconds</p>", "mode": "custom", "duration": "3s" } }
```

```json
{ "say": { "label": "<p>Waits for the user to click</p>", "mode": "pause" } }
```

### 3.2 `goto` — switch page

Jumps to another page by name.

| Param    | Type   | Values / notes |
|----------|--------|----------------|
| `target` | string | Destination page name, **or** a wildcard pattern. |

**Wildcard random target:** if `target` contains a wildcard (e.g. `"00*"`), the player picks a
**random** page from all pages whose name matches the pattern. Example: `00*` matches any page
whose name starts with `00`.

```json
{ "goto": { "target": "001-Images" } }
```

### 3.3 `image` — show an image

Displays an image, centered. Subsequent `say` text appears below it. A later `image` action
**replaces** the currently shown image.

| Param     | Type   | Values / notes |
|-----------|--------|----------------|
| `locator` | string | `gallery:<galleryId>/<imageId>` for a specific image, or `gallery:<galleryId>/*` for a **random** image from that gallery. |

The `<galleryId>` is the UUID key under `galleries`; `<imageId>` is an image `id` listed inside
that gallery (see §5).

```json
{ "image": { "locator": "gallery:c150a13b-1d79-412d-8fbe-6f609b77962c/4124308" } }
```

```json
{ "image": { "locator": "gallery:c150a13b-1d79-412d-8fbe-6f609b77962c/*" } }
```

### 3.4 `timer` — wait

Pauses for a duration with an optional visible countdown.

| Param      | Type   | Values / notes                                                                                                                                                                                                |
|------------|--------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `duration` | string | A single value with a unit suffix like `"10s"`, **or** a range `"min-max"` (e.g. `"1s-5s"` = a random duration between 1 and 5 seconds). **Only seconds (`s`) are supported**, and the number may have **at most one decimal place** (e.g. `"2.5s"`, `"0.5s-3.5s"`). |
| `style`    | string | `normal`, `secret`, or `hidden`.                                                                                                                                                                              |

**Style meanings (from the example):**
- `normal` — displays the remaining time.
- `secret` — shows a timer, but without the countdown number.
- `hidden` — does not display a timer at all.

```json
{ "timer": { "duration": "10s", "style": "normal" } }
```

```json
{ "timer": { "duration": "1s-5s", "style": "secret" } }
```

### 3.5 `choice` — branching buttons

Presents the user with clickable buttons. Each button runs its own list of actions.

| Param     | Type  | Notes |
|-----------|-------|-------|
| `options` | array | One entry per button. |

Each option object:

| Field      | Type   | Notes |
|------------|--------|-------|
| `label`    | string | Button text. |
| `commands` | array  | A nested array of action objects to run when this button is picked. Can be a single `goto`, or multiple actions of any kind. |
| `color`    | string | Button color, `#hex` (e.g. `#1976d2`). |

```json
{
  "choice": {
    "options": [
      {
        "label": "Go back to start",
        "commands": [ { "goto": { "target": "start" } } ],
        "color": "#1976d2"
      },
      {
        "label": "Read more",
        "commands": [
          { "say": { "label": "<p>First line</p>" } },
          { "say": { "label": "<p>Second line</p>" } }
        ],
        "color": "#f44336"
      }
    ]
  }
}
```

### 3.6 `audio.play` — play audio

Plays an audio file from the `files` manifest. **Requires the `audio` module** to be declared
(see §4). Playback is **non-blocking** — actions after it run immediately.

| Param        | Type    | Values / notes |
|--------------|---------|----------------|
| `locator`    | string  | `file:<filename>`, referencing a key in `files`. |
| `volume`     | number  | A **fraction from 0.0 to 1.0** (not 0–100). `0.8` = 80%. The UI shows it as a percentage but the JSON stores the fraction. |
| `loops`      | integer | **Total number of times the clip plays**, counting from 1. `2` = play exactly twice. (So `1` = play once.) |
| `background` | boolean | Continue across pages. `true` = audio keeps playing after a page switch; `false`/omitted = audio stops when the page changes. |

```json
{ "audio.play": { "locator": "file:metronome-120bpm.mp3", "loops": 2, "background": true, "volume": 0.8 } }
```

#### Clips **layer**, they do not replace (confirmed)

A second `audio.play` **does not stop the first** — both clips play **simultaneously**. There is
**no `audio.stop` action**. The only way to stop a playing clip is to **switch to another page**,
and that only stops clips that are *not* `background: true` (a `background` clip keeps playing
across the switch by design). Practical consequences:

- **Overlapping the same clip doubles it** (e.g. starting the next metronome before the previous
  one finishes produces audible doubled beats at the join).
- A **`background: true` clip can only be stopped by ending the tease / navigating away** — there
  is no per-action stop, so use it sparingly.

#### Pattern: timed tempo sequence (one tempo per page)

To play a sequence of tempo segments (as in a metronome-driven tease), give **each segment its own
page**: `audio.play` (loops sized to cover the segment) → a `say` label → a `timer` for the
segment length → `goto` the next segment's page. The page switch stops the previous (non-
`background`) clip, so the next tempo starts clean with **no doubled beats**. Because the page
switch trims the tail, size `loops` to **cover** the timer (round up); the exact overrun is cut on
exit. *Do not* put two tempo segments on the same page (they would stack). See
`VerificationTease` pages `033-Metronome-1..3`.

### 3.7 `end` — end the tease

Ends the tease and prompts the user to rate it. No actions run after it.

```json
{ "end": {} }
```

### 3.8 `notification.create` — show a notification

Shows a notification. **Requires the `notification` module** to be declared (see §4). A
notification can optionally carry a **button element** and/or a **timer element**, each with its
own list of commands.

| Param            | Type    | Values / notes |
|------------------|---------|----------------|
| `id`             | string  | **Required.** A unique identifier for this notification, used to remove it later (via `notification.remove`, or from inside its own button/timer commands). |
| `title`          | string  | **Optional.** The notification's title. May be omitted for a notification with no title. |
| `buttonLabel`    | string  | Optional. Label for a clickable button on the notification. Pair with `buttonCommands`. |
| `buttonCommands` | array   | Optional. A nested array of action objects run when the button is clicked. |
| `timerDuration`  | string  | Optional. A timer on the notification; single value (`"3s"`) or range (`"3s-5s"`). Same seconds-only / one-decimal rules as the `timer` action (§3.4). Pair with `timerCommands`. |
| `timerCommands`  | array   | Optional. A nested array of action objects run when the timer fires. |

A common pattern is to have the button/timer commands remove the notification by its own `id`.

```json
{ "notification.create": { "id": "N001", "title": "Notification title" } }
```

```json
{
  "notification.create": {
    "id": "N002",
    "title": "Button notification",
    "buttonLabel": "Click me",
    "buttonCommands": [
      { "say": { "label": "<p>You clicked the button</p>" } },
      { "notification.remove": { "id": "N002" } }
    ]
  }
}
```

```json
{
  "notification.create": {
    "id": "N003",
    "title": "Timer notification",
    "timerDuration": "3s",
    "timerCommands": [ { "notification.remove": { "id": "N003" } } ]
  }
}
```

### 3.9 `notification.remove` — remove a notification

Removes a previously created notification by its `id`. Requires the `notification` module.

| Param | Type   | Values / notes |
|-------|--------|----------------|
| `id`  | string | The `id` of the notification to remove. |

```json
{ "notification.remove": { "id": "N001" } }
```

---

## 4. Top-level metadata sections

### `init`
A string, empty (`""`) in the example. Purpose/expected content still unconfirmed — see §8.
Include it as `""` for parity with editor exports.

### `modules`
Feature toggles, each enabled by an empty object. A module must be present to use its actions:
`audio` for `audio.play`, `notification` for `notification.create` / `notification.remove`.

```json
"modules": { "audio": {}, "notification": {} }
```

### `editor`
Editor-only convenience state. In the example it holds `recentImages` (the last images picked in
the editor UI). **Not required for playback** — generated files can omit it or include an empty
object. Example shape:

```json
"editor": {
  "recentImages": [
    {
      "type": "gallery",
      "mimeType": "image/jpeg",
      "galleryId": "c150a13b-1d79-412d-8fbe-6f609b77962c",
      "url": "gallery:c150a13b-1d79-412d-8fbe-6f609b77962c/4124309",
      "imageId": 4124309
    }
  ]
}
```

---

## 5. External assets: `galleries` and `files`

Locators in `image` and `audio.play` actions resolve against these two manifests.

### `galleries`

Keyed by **gallery UUID**. Each gallery has a display `name` and an `images` array.

```json
"galleries": {
  "c150a13b-1d79-412d-8fbe-6f609b77962c": {
    "name": "PictureSet001",
    "images": [
      { "id": 4124308, "hash": "775d9d7b…", "size": 192900, "width": 100,  "height": 100  },
      { "id": 4124310, "hash": "44e8f95c…", "size": 165431, "width": 853,  "height": 1280 }
    ]
  }
}
```

Each image entry: `id` (integer, referenced by `gallery:<uuid>/<id>` locators), `hash`, `size`
(bytes), `width`, `height`.

### `files`

Keyed by **filename** (the same string used in `file:<filename>` locators).

```json
"files": {
  "metronome-120bpm.mp3": {
    "id": 4124314,
    "hash": "7a7baf76…",
    "size": 96431,
    "type": "audio/mpeg"
  }
}
```

Each file entry: `id` (integer), `hash`, `size` (bytes), `type` (MIME type).

### ⚠️ Asset IDs are server-assigned

The `id`, `hash`, and `size` values are **assigned by Milovana when the asset is uploaded**.
They **cannot be derived offline**: the local source images have unrelated filenames
(e.g. `80877276_032_1886.jpg`) and carry no Milovana ID. Therefore a hand-written tease cannot
invent valid `galleries`/`files` entries from local files alone — the real manifest must always
be obtained from an editor export.

The workflow below resolves how we author asset-backed teases offline despite this.

---

## 5.1 Asset workflow (local → upload → resolve)

This is the **confirmed, end-to-end process** for using local galleries/audio in a tease. It was
validated on `VerificationTease` (2 galleries, 12 images, 2 audio files).

### Per-tease, by design

Milovana **re-uploads and re-IDs assets for every tease** — the same picture used in two teases
gets two different UUIDs/image-IDs and two stored server copies. We mirror that locally: **each
tease owns its own asset folders and its own `asset-map.json`.** Assets are *not* shared in a
repo-level pool.

### Local folder layout

Each tease lives in `milovana/Teases/<TeaseName>/` and holds its own assets:

```
milovana/Teases/<TeaseName>/
├── tease.json          ← the tease (also the editor-export target — see below)
├── asset-map.json      ← generated: local file ↔ Milovana locator map
├── Gallery/
│   ├── <GalleryNameA>/ ← one folder per gallery; images inside
│   └── <GalleryNameB>/
└── Files/              ← audio (and other non-image) files
```

**Naming convention (required for resolution):** name each `Gallery/<GalleryName>/` folder
**identically** to the gallery's `name` in the editor. That folder-name = gallery-name link is
how the resolver pairs a UUID to a local folder (image *entries* carry no name — see below).

### The three steps

1. **Author locally.** Create the `Gallery/<name>/` and `Files/` folders and drop the source
   files in. Bytes must be **exactly** what you upload — no re-encoding/recompression in between,
   or the hash/size match breaks.
2. **Upload & export.** In the EOS editor, create one gallery per local folder (same name) and
   upload its images; upload the audio files. Export the tease JSON (backup/restore, or the
   `showtease`→`geteosscript` URL trick) **into `tease.json`**. The export carries the real
   `galleries` and `files` manifests.
3. **Resolve to a map.** Build `asset-map.json` by joining the manifest in `tease.json` to the
   local files (see §5.2). After this, author `image`/`audio.play` actions using the `locator`
   strings the map provides.

### How resolution actually pairs IDs to local files

- **Audio / `files`:** trivial — the `files` manifest is **keyed by the original filename**, so
  `metronome-120bpm.mp3` maps straight to `Files/metronome-120bpm.mp3`. (Filename is the key even
  when two files share an identical byte `size`.)
- **Gallery images:** an image entry has **no filename** — only `id`/`hash`/`size`/`width`/
  `height`. Pair them by **exact content hash**: the manifest `hash` is **SHA-1** of the file
  bytes, so compute each local file's SHA-1 and match it to the manifest entry. This is exact and
  collision-free.

> **⚠️ Do not match gallery images on `width`/`height`.** Milovana's manifest dimensions are
> **unreliable** — observed reporting `100×100` for images that are actually `853×1280`. `size`
> is a usable secondary signal, but **SHA-1 hash is the source of truth.** (Files keyed by name,
> images keyed by SHA-1.)

## 5.2 `asset-map.json` (the generated map)

One per tease, beside `tease.json`. Maps each local file to its Milovana `id`/`hash`/`size`, true
local dimensions, and a ready-to-paste `locator`. Generate it (don't hand-write it) by joining the
export manifest to on-disk SHA-1 hashes, so there is no transcription error. Shape:

```json
{
  "tease": "VerificationTease",
  "hashAlgorithm": "sha1",
  "galleries": {
    "LeahGotti_001": {
      "uuid": "c6929b12-b8e1-4f54-a3f7-877650f258aa",
      "localFolder": "Gallery/LeahGotti_001",
      "images": {
        "80877276_032_1886.jpg": {
          "id": 4124446, "hash": "079a82ee…", "size": 199040, "width": 853, "height": 1280,
          "locator": "gallery:c6929b12-b8e1-4f54-a3f7-877650f258aa/4124446"
        }
      }
    }
  },
  "files": {
    "metronome-120bpm.mp3": {
      "id": 4124459, "hash": "7a7baf76…", "size": 96431, "type": "audio/mpeg",
      "localPath": "Files/metronome-120bpm.mp3", "locator": "file:metronome-120bpm.mp3"
    }
  }
}
```

A PowerShell generator (Windows; no Python on this machine) reads `tease.json`, builds a
`SHA-1 → local file` index with `Get-FileHash -Algorithm SHA1`, then emits the map. Validate by
confirming **every** manifest entry resolved (zero unmatched).

## 5.3 Image selection: vision tagging & themed galleries

Gallery image filenames are opaque (`80877276_032_1886.jpg`), so to pick images that fit a script
beat, Claude must know **what each image depicts**. Claude can view local image files directly, so
after the map is built it does a **content-tagging pass**.

### Storage: `asset-content.json`

Per tease, **sibling to `asset-map.json`**, **keyed by `<GalleryName>/<filename>`** (the stable
local-content identity — *not* Milovana IDs, which change on every re-upload). Kept **separate**
from `asset-map.json` so regenerating the mechanical map never clobbers hand-verified tags. At
authoring time the two are **joined on the filename key**: `asset-content` = *what's in the
picture*, `asset-map` = *how to reference it*. (Tags are content-intrinsic, so they can be reused
if the same source image appears in a later tease.)

### Tag vocabulary (controlled, for consistency)

`pace` and `explicitness` are **two independent axes** — `explicitness` = how much is *shown*;
`pace` = how *energetic/fast* the scene is, ignoring nudity. They do **not** combine into one
score (a calm nude pose is more exposed but lower-paced than an active clothed shot). Keeping them
separate lets a tease's tempo arc (BPM) and exposure arc escalate independently.

| Tag | Values | Purpose |
|-----|--------|---------|
| `subject` | `solo` / `machine` / `partner` | Enforce constraints (e.g. tutorial = `solo` only). |
| `pace` | `1`–`5`, energy/tempo **only** (ignores nudity), **anchored to BPM bands** (≈ 1:≤40, 2:40–70, 3:70–110, 4:110–150, 5:150+) | "Match the picture's pace to the current BPM" becomes a lookup. |
| `explicitness` | `clothed` / `underwear` / `partial-nudity` / `nude` / `explicit` | Exposure **only**, independent of pace. (clothed = nothing bared; underwear = bra/panties/thong as worn; partial-nudity = one region bared; nude = fully exposed/undressed; explicit = explicit act.) |
| `orientation` | `portrait` / `landscape` | Known from dimensions; affects display. |
| `notes` | free text | Pose/setting cue for a specific beat. |

```json
{
  "tease": "TheFuckingMachine",
  "vocabulary": {
    "subject": ["solo","machine","partner"],
    "explicitness": ["clothed","underwear","partial-nudity","nude","explicit"],
    "pace": "Energy/tempo only, independent of explicitness. 1 (still/posed) to 5 (frantic/hard); BPM-banded.",
    "orientation": ["portrait","landscape"]
  },
  "galleries": {
    "LeahGotti_001": {
      "theme": "solo-sensual",
      "images": {
        "80877276_032_1886.jpg": { "subject": "solo", "pace": 1, "explicitness": "partial-nudity",
                                    "orientation": "portrait", "notes": "standing, daylight window, direct gaze; still/posed" }
      }
    }
  }
}
```

Tags are **Claude's visual judgement** — the user verifies them (step 8). It's a **one-time pass**
per gallery, re-run only when images change.

### Themed gallery buckets (raise this at step 3)

Organize galleries so **each gallery ≈ one script section** (mood/pace) (e.g. `solo-sensual`,
`machine-soft`, `machine-hard`, `climax`). Two payoffs:

- **Random locators stay on-theme:** `gallery:<machine-hard-uuid>/*` on a high-BPM page auto-picks
  something fitting, no per-image selection needed.
- **Hard constraints become structural:** tutorial pages draw only from `solo`-themed galleries,
  satisfying author-note rules like "model alone" automatically.

Specific (tagged) locators are then reserved for precise moments (e.g. the climax shot); themed
random locators cover "any fitting image here."

---

## 6. Authoring checklist & gotchas

- **One key per action object.** `{ "say": {…} }`, not two actions in one object.
- **Define `start`.** It is the entry page.
- **Page names:** letters, numbers, hyphen only.
- **Declare the matching module** before using a module action: `audio` for `audio.play`,
  `notification` for `notification.create` / `notification.remove`.
- **Give every notification a unique `id`** so it can be removed (§3.8).
- **Every locator must resolve:** each `gallery:<uuid>/<id>` and `file:<name>` needs a matching
  entry in `galleries` / `files`.
- **HTML-encode `say` text:** wrap in `<p>…</p>`; entity-encode apostrophes/quotes (`&#39;`).
  Use only `<strong>`/`<em>`/`<u>`/`<span style="color:…">` — **`<code>` does not render** (§3.1).
- **Remember cross-page state:** the last image persists; text clears (§2).
- **`editor` is optional** for playback; `id`/`hash` in manifests are server-assigned (§5).
- **Asset-backed teases:** prep local assets, upload, export, and resolve to `asset-map.json`
  **before** authoring any `image`/`audio.play` locators (§5.1–§5.3). Gallery images resolve by
  **SHA-1 hash**, audio by **filename** — never by `width`/`height` (manifest dims are unreliable).

---

## 7. Annotated mini-example

A minimal complete tease: intro text → image → timer → choice → end. (Asset IDs here are
placeholders — replace with real ones from the editor before upload; see §5.)

```json
{
  "pages": {
    "start": [
      { "say": { "label": "<p>Welcome.</p>", "align": "center" } },
      { "goto": { "target": "010-Show" } }
    ],
    "010-Show": [
      { "image": { "locator": "gallery:REPLACE-UUID/REPLACE-IMAGE-ID" } },
      { "say": { "label": "<p>Hold still…</p>" } },
      { "timer": { "duration": "10s", "style": "normal" } },
      {
        "choice": {
          "options": [
            { "label": "Again", "commands": [ { "goto": { "target": "010-Show" } } ], "color": "#1976d2" },
            { "label": "Finish", "commands": [ { "goto": { "target": "099-End" } } ], "color": "#f44336" }
          ]
        }
      }
    ],
    "099-End": [
      { "say": { "label": "<p>Done.</p>" } },
      { "end": {} }
    ]
  },
  "init": "",
  "modules": {},
  "files": {},
  "galleries": {
    "REPLACE-UUID": {
      "name": "PlaceholderSet",
      "images": [ { "id": 0, "hash": "REPLACE", "size": 0, "width": 0, "height": 0 } ]
    }
  },
  "editor": {}
}
```

---

## 8. Open questions

Fields/behaviors present or referenced in the example whose exact form is **not** confirmed by
the JSON itself. Resolve these (e.g. by inspecting a real editor export) before relying on them:

1. **`init`** — empty string in the example; its purpose and expected content are unknown.
2. **`modules` entries beyond `audio` and `notification`** — only `audio: {}` and
  `notification: {}` are demonstrated; other modules and their config shapes are unknown.

### Resolved (previously open)

The following were open in earlier drafts and are now confirmed from
[`teachingmaterial-2026-06-19.json`](teachingmaterial-2026-06-19.json):

- **`say.mode` value strings** → `pause`, `instant`, `autoplay`, `custom`; Auto = omit `mode`. (§3.1)
- **`say.align` default** → `center`. (§3.1)
- **`audio.play.volume`** → key `volume`, a `0.0`–`1.0` fraction (`0.8` = 80%). (§3.6)
- **Audio continue-across-pages key** → `background` (boolean). (§3.6)
- **`audio.play.loops` semantics** → total play count starting at 1 (`2` = play twice). (§3.6)
- **Multiple `audio.play` clips** → **layer/stack** (a new clip does *not* stop the previous); no
  `audio.stop` action exists; the only stop for a non-`background` clip is a page switch. Timed
  tempo sequences therefore use **one tempo per page**. (§3.6)
- **Multiple `say` actions on one page** → **append** (each adds a new line below; text is cleared
  only by a page switch). (§3.1, §2)
- **`timer.duration` range syntax** → `"min-max"`, e.g. `"1s-5s"`. (§3.4)
- **`timer.duration` units** → seconds only, with at most one decimal place (e.g. `"2.5s"`). (§3.4)
- **Offline asset authoring workflow** → resolved: per-tease local folders, upload+export, then
  resolve to `asset-map.json` by SHA-1 hash (images) / filename (audio). (§5.1–§5.3)
- **Manifest `hash` algorithm** → **SHA-1** of the file bytes. (§5.1)
- **Manifest image `width`/`height`** → **unreliable** (can report `100×100` for a `853×1280`
  image); do not key on them. (§5.1)
