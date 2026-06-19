# Milovana EOS Tease Authoring Guide

A reference for hand-writing Milovana **EOS** teases in JSON so they can be uploaded into the
EOS editor via its backup/restore function.

This guide is derived **strictly** from the worked example
[`teachingmaterial-2026-06-19.json`](teachingmaterial-2026-06-19.json), which demonstrates the
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

---

## 2. Pages & flow model

- `pages` is an object whose **keys are page names**. Allowed characters in a page name:
  **letters, numbers, and a hyphen** (`-`). The example uses names like `start`, `001-Images`,
  `002-Timers`, `099-End`.
- **`start` is the entry page.** Execution begins there.
- Each page's value is an **ordered array of action objects**. Actions run top to bottom.
- **Each action object has exactly one key**, naming the action. The seven actions used in the
  example are: `say`, `goto`, `image`, `timer`, `choice`, `audio.play`, `end`.

### What carries across a page switch

From the example's own narration (`002-Timers`):

- The **last image shown persists** onto the next page until replaced.
- The **on-screen text is cleared** when the page switches.

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

**Non-blocking after media:** a `say` placed after an `image` or `audio.play` runs immediately
— it does not wait for the image transition or audio clip to finish.

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

### 3.7 `end` — end the tease

Ends the tease and prompts the user to rate it. No actions run after it.

```json
{ "end": {} }
```

---

## 4. Top-level metadata sections

### `init`
A string, empty (`""`) in the example. Purpose/expected content still unconfirmed — see §8.
Include it as `""` for parity with editor exports.

### `modules`
Feature toggles. To use `audio.play`, the `audio` module must be present:

```json
"modules": { "audio": {} }
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

### ⚠️ Asset IDs are server-assigned (offline caveat)

The `id`, `hash`, and `size` values are **assigned by Milovana when the asset is uploaded**.
They **cannot be derived offline**: the local files under
[`milovana/Galleries/`](../Galleries/) have unrelated filenames (e.g. `80877276_032_1886.jpg`)
and carry no Milovana ID or hash. Therefore a hand-written tease cannot invent valid
`galleries`/`files` entries from local files alone.

**The concrete offline authoring workflow for assets is deferred / not yet decided.** Until
then, treat real `galleries`/`files` manifests as something obtained from the editor (upload
assets there, export the JSON, copy the manifests). When drafting without real IDs, use clearly
fake placeholder values and flag that they must be replaced before upload.

---

## 6. Authoring checklist & gotchas

- **One key per action object.** `{ "say": {…} }`, not two actions in one object.
- **Define `start`.** It is the entry page.
- **Page names:** letters, numbers, hyphen only.
- **Declare the `audio` module** before using `audio.play`.
- **Every locator must resolve:** each `gallery:<uuid>/<id>` and `file:<name>` needs a matching
  entry in `galleries` / `files`.
- **HTML-encode `say` text:** wrap in `<p>…</p>`; entity-encode apostrophes/quotes (`&#39;`).
- **Remember cross-page state:** the last image persists; text clears (§2).
- **`editor` is optional** for playback; `id`/`hash` in manifests are server-assigned (§5).

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
2. **`modules` entries beyond `audio`** — only `audio: {}` is demonstrated; other modules and
  their config shapes are unknown.

### Resolved (previously open)

The following were open in earlier drafts and are now confirmed from
[`teachingmaterial-2026-06-19.json`](teachingmaterial-2026-06-19.json):

- **`say.mode` value strings** → `pause`, `instant`, `autoplay`, `custom`; Auto = omit `mode`. (§3.1)
- **`say.align` default** → `center`. (§3.1)
- **`audio.play.volume`** → key `volume`, a `0.0`–`1.0` fraction (`0.8` = 80%). (§3.6)
- **Audio continue-across-pages key** → `background` (boolean). (§3.6)
- **`audio.play.loops` semantics** → total play count starting at 1 (`2` = play twice). (§3.6)
- **`timer.duration` range syntax** → `"min-max"`, e.g. `"1s-5s"`. (§3.4)
- **`timer.duration` units** → seconds only, with at most one decimal place (e.g. `"2.5s"`). (§3.4)
```
