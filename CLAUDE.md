# Milovana Tease Editor

Authoring environment for Milovana EOS teases (JSON format) plus the **MilovanaEosEditor** desktop
tool used to prepare tease assets.

## Claude - Important

See [CLAUDE_GENERAL_GUIDELINES.md](CLAUDE_GENERAL_GUIDELINES.md) for general behavioral guidelines
when working in this project.

**Do not commit or push.** Never run `git commit` or `git push` (or otherwise create commits/tags
or publish to a remote) unless explicitly tasked to do so, or after asking and being given
permission. The user handles commits and pushes themselves most of the time — make the changes,
then leave staging/committing to them.

## Repository layout

- `Documentation/` — the EOS authoring guide, instructions, and teaching material.
- `Teases/<TeaseName>/` — per-tease `tease.json`, `asset-map.json`, `asset-content.json`,
  `Gallery/<bucket>/` images, and `Files/` audio.
- `Files/` — shared asset sources (e.g. metronome loops).
- `src/` — the **MilovanaEosEditor** WPF tool for asset tagging and generating `asset-map.json`
  (project file: `src/MilovanaEosEditor.csproj`).
- `Tools/` — build scripts (`Build-Tease.ps1`, `Build-Tease-TFM.cmd`).

## Milovana Teases

When asked to write, edit, or read a Milovana EOS tease (JSON format), **always consult
[Documentation/EOS-Tease-Authoring-Guide.md](Documentation/EOS-Tease-Authoring-Guide.md)
first** — it documents the page/action model, every supported action and field, the
`gallery:`/`file:` asset locators, and known constraints. Keep that guide updated when new
tease behavior is learned (the `§8 Open questions` section tracks unconfirmed fields).

**When starting a NEW tease that will use images or audio**, proactively walk the user through the
**end-to-end workflow in the guide's §1 Overview** *before* authoring any `image`/`audio.play`
locators. Key points to surface:
- **Recommend themed gallery buckets** (one gallery per script mood/pace, e.g.
  `solo-sensual` → `machine-hard`) so random locators stay on-theme and hard constraints (e.g.
  *tutorial = solo only*) become structural — see §5.3.
- Asset prep order: per-tease `Gallery/<bucket>/` + `Files/` folders → name each editor gallery to
  match its local folder → upload **images and audio** → export into `tease.json` → generate
  `asset-map.json` → **vision-tag images into `asset-content.json`** (user verifies) — locators are
  authored only after that.
- Gallery images resolve by **SHA-1 hash**, audio by **filename** — never by manifest
  `width`/`height` (those are unreliable). Image *selection* joins `asset-content.json` (content
  tags) with `asset-map.json` (locators) on the filename key.

## Assets / Git LFS

Tease images and audio (`*.jpg *.jpeg *.png *.mp3 *.wav`) are tracked with **Git LFS** (see
`.gitattributes`). Run `git lfs install` once after cloning so these files smudge to real binaries.

## Build & Run (MilovanaEosEditor)

```sh
dotnet build src/MilovanaEosEditor.csproj
dotnet run --project src
```
