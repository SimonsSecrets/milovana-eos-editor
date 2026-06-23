# Script — The Fucking Machine (Introduction)

## Tone & writing style

Keep all edits and additions consistent with the style below.

**Voice** — spoken by a seductive female persona (the model, Leah Gotti), first person, addressing
the player as "you". Sensual and suggestive, not crude. Player is gender-neutral. Warm, intimate,
reassuring; she's in control but wants the player to feel safe. From the second half of Tease Page 9
she also voices her own pleasure (loving the surrender; then, once partner imagery begins, her own
physical pleasure) while still turning back to the player.

## Authoring format (this file is the source the generator parses)

`milovana/Tools/Build-Tease-TFM.ps1` parses **this file** into `tease.json`. Only lines that begin (at
column 0) with a recognised `[KEYWORD ...]` marker are processed; **everything else — headings, prose,
and `[Author note: …]` lines — is ignored**, so notes stay free-form.

Grammar: `[KEYWORD (param=value, param=value): payload]` (params and payload optional).

- `[PAGE: key]  free comment` — starts a page; `key` is the EOS page name (letters/numbers/hyphen). First page must be `start`.
- `[IMAGE: bucket/filename.jpg]` — image by **bucket/filename** (resolved to a locator via asset-map). `[IMAGE: hold]` keeps the previous image. **Edit the filename here to swap an image.**
- `[SAY (mode=pause, align=left, duration=3s): html]` — one say action; all params optional and mirror the milovana `say` action. Text is auto-wrapped in `<p>…</p>`; add `<em>`/`<strong>`/`<u>` inline. If `mode` is omitted the generator defaults it (see below).
- `[METRONOME (bpm=120, secs=60)]` — a timed block; becomes its own EOS page (one tempo per page). The `[SAY]` lines that follow it are revealed evenly across `secs`. An `[IMAGE]` immediately before it is that block's image; `[NOTIFICATION]`s before it attach to that block's page.
- `[PAUSE (secs=10)]` — a silent timed block (machine still).
- `[AUDIO (bpm=60, loops=40)]` — a non-blocking looping metronome under a *non-timed* page (used on the setup pages so the app has a beat to detect).
- `[NOTIFICATION (id=climax-btn, target=tease-17): label]` — a persistent right-side button overlay.
- `[CHOICE]` then one or more `[OPTION (target=key, color=#hex): label]` — a menu / branch.
- `[GOTO: key]` — page exit (jump). `[GOTO]` back to the current page key = loop. `[END]` — end the tease.

**Say-mode default:** an omitted `mode` becomes `instant` for every say except the last say on a
tap-advance page (one that exits via `[GOTO]`), which becomes `pause` so the page still advances on a
single tap. Pages that exit via `[CHOICE]`/`[END]` leave all says `instant`. Set `mode=` explicitly to override.

## Model

The main model featuring in this tease is Leah Gotti - https://www.pornpics.com/pornstars/leah-gotti/

---

[PAGE: start]  Title / Hook
[IMAGE: title-hero/97162788_004_9e2c_cropped.jpg]
[SAY (mode=autoplay): <em>You've followed the metronome a hundred times.</em>]
[SAY: <em>Tonight, you don't have to move at all. I'll take it from here.</em>]
[GOTO: 002-pitch]

[PAGE: 002-pitch]  The pitch
[IMAGE: intro-solo-sensual/31517085_004_ca6a.jpg]
[SAY (mode=autoplay): Welcome to <strong>The Fucking Machine Tease</strong>. I'm so glad you're here.]
[SAY (mode=autoplay): Every tick of the metronome you're about to hear isn't meant for <em>you</em> — it's meant for <em>the machine.</em>]
[SAY (mode=autoplay): The same metronome you already know moves a <strong>real machine</strong>, in real time.]
[SAY (mode=custom, duration=1s): It quickens,]
[SAY (mode=custom, duration=1s): it slows,]
[SAY (mode=custom, duration=5s): it lingers and it drives...]
[SAY (mode=autoplay): ... and all you have to do is lie back and let <strong>me</strong> set the pace.]
[SAY (mode=autoplay): <em>Truly hands-free.</em> Slow and teasing, or relentless — tonight every rhythm is mine to choose, and yours to feel.]
[CHOICE]
[OPTION (target=003-need, color=#f06292): That sounds amazing!]

[PAGE: 003-need]  What you'll need  (no [IMAGE] = holds the previous image)
[SAY (mode=pause): It truly does sound <em>amazing</em>, doesn't it?]
[SAY (mode=autoplay): Before we begin, there are a few things you will need:]
[SAY:  🔌 <strong>A fucking machine</strong> — see Supported Devices on the right. 
                                                💻 <strong>The HismithController app</strong> — Windows PC only (download instructions come later).
                  📶 <strong>Bluetooth</strong> — your PC must be able to connect to Bluetooth devices.
💦 <strong>Plenty of lube</strong> — more than you think.                                  
]
[NOTIFICATION (id=devices-btn, target=devices): Supported Devices]
[SAY: <strong>No machine? You can still play.</strong>  Just ease a dildo in and out in time with each tick of the metronome. It's not quite the hands-free surrender I'd love to give you, but every rhythm I choose is still yours to feel.]
[CHOICE]
[OPTION (target=004-premenu, color=#f06292): This is... exciting]

[PAGE: 004-premenu] Pre-menu
[IMAGE: intro-solo-sensual/31517085_005_17db.jpg]
[SAY (mode=pause): So… shall we begin?]
[GOTO: 004-menu]

[PAGE: 004-menu]  Menu
[IMAGE: intro-solo-sensual/31517085_005_17db.jpg]
[SAY (mode=pause): So… shall we begin?]
[SAY: If this is your first time here, you will want to start with the <strong><span style="color: #1976d2">Application Setup</span></strong>, where your will install, pair and test your machine.
Then, move on to the <strong><span style="color: #2e7d32">Tutorial</span></strong> — a short, gentle run so you can feel how it responds.]
[SAY: When you think you are ready for it, you can move on to the real thing — <strong><span style="color: #d81b60">The Fucking Machine Tease</span></strong>.]
[CHOICE]
[OPTION (target=setup-1, color=#1976d2): ⚙️ Application Setup]
[OPTION (target=tut-1, color=#2e7d32): 🎓 Tutorial]
[OPTION (target=tease-1, color=#d81b60): 🔥 The Fucking Machine Tease]
[OPTION (target=about, color=#6a1b9a): ℹ️ About]

---

# Application Setup  (function check — player not using the machine on themselves yet; setup-solo bucket)

[PAGE: setup-1]  What this is
[IMAGE: setup-solo/66348606_009_5a77.jpg]
[SAY: <em>Before I can take over, let's make sure everything's working — together.</em>]
[SAY: This part is just setup. We'll get the app running, connect your machine, and check that it responds to the beat.]
[SAY: <strong>You won't be using the machine on yourself yet</strong> — for now we're only watching to confirm it all works. The real fun comes later.]
[SAY: Take your time. I'll be right here.]
[CHOICE]
[OPTION (target=setup-2, color=#5d4037): ➡️ Continue]
[OPTION (target=004-menu, color=#5d4037): ↩️ Back to the menu]

[PAGE: setup-2]  Get the app & your machine ready
[IMAGE: setup-solo/66348606_015_4d51.jpg]
[SAY (mode=pause): First, a little preparation.]
[SAY: 1. <strong>Download HismithController</strong> from the <a href='https://github.com/SimonsSecrets/hismith-bt-controller/releases'>GitHub releases page</a>.
2. <strong>Set up your Hismith Pro 1</strong> — mount it securely, attach your accessory, and <strong>turn it on</strong> so it's ready to pair over Bluetooth.
                    No need to point it at yourself yet — just have it switched on and within reach.
3. Start the <strong>HismithController</strong> app
💡 <strong>If Windows shows a blue 'Windows protected your PC' screen</strong>, that's normal for a small app like this one. Click 'More info', then 'Run anyway' to continue.
]
[CHOICE]
[OPTION (target=setup-3, color=#5d4037): ➡️ Continue]

[PAGE: setup-3]  Connect over Bluetooth
[IMAGE: setup-solo/66348606_022_5905.jpg]
[SAY (mode-pause): Now let's introduce them to each other.]
[SAY: 1. If this is the first time you run the app, click 'Get started'.
2. On the connection screen, click 'Scan for devices'.
3. When your Hismith appears in the list, <strong>tap to select it</strong>, then click 'Connect'.
🔍 <strong>Don't see your device?</strong> Make sure it's switched on, your PC's <strong>Bluetooth is enabled</strong>, and the machine isn't already paired to something else. Click 'Scan for devices' again to retry.
[SAY: Once you're connected, you'll land on the main screen with the <strong>Manual</strong> / <strong>Sound</strong> mode toggle at the top.]
[CHOICE]
[OPTION (target=setup-4, color=#5d4037): ➡️ Continue]

[PAGE: setup-4]  Test it in Manual mode
[IMAGE: setup-solo/66348606_028_5c9e.jpg]
[SAY: Let's make sure your machine listens to you first.]
[SAY: 1. Make sure the <strong>Manual</strong> tab is selected.
2. Under <strong>Speed control</strong>, either type a value into <strong>SPEED (%)</strong> or tap one of the <strong>PRESETS</strong>.
3. Watch your machine — it should ramp smoothly up to the speed you set.
]
[SAY: Feel free to play with a few values. When you've seen it respond, bring it back down to a stop.]
[CHOICE]
[OPTION (target=setup-5, color=#5d4037): ➡️ Continue]

[PAGE: setup-5]  PC sound configuration
[IMAGE: setup-solo/92231232_025_172c.jpg]
[SAY (mode-pause): Now the part that makes this special — letting the metronome drive it.]
[SAY: The app listens to whatever sound your PC is playing, so first make sure it can hear sounds coming from this tease:]
[SAY: 🔊 Play this tease <strong>on the same Windows PC</strong> the app is running on.
🔉 Turn your <strong>volume up</strong> and make sure you're <strong>not muted</strong>.
🎧 Keep the sound coming out of that PC — if your audio goes to a separate speaker or a Bluetooth headset, the app may not hear it.
]
[CHOICE]
[OPTION (target=setup-5b, color=#5d4037): ➡️ Continue]

[PAGE: setup-5b]  Let the app hear the beat  (plays a 60 BPM metronome so the app has a beat to detect)
[AUDIO (bpm=60, loops=40)]
[SAY: Switch to the <strong>Sound</strong> tab and let the metronome on this page play. Within a few seconds you should see the <strong>MUSIC</strong> readout showing a live <strong>bpm</strong>, and the <strong>visualizer bars</strong> moving in time with the beat.]
[SAY: That means the app can hear me. Once you see the beat being picked up, you're ready for the next step.]
[CHOICE]
[OPTION (target=setup-6, color=#5d4037): ➡️ Continue]

[PAGE: setup-6]  Watch it move to the beat
[IMAGE: setup-solo/92231232_040_1eaa.jpg]
[AUDIO (bpm=60, loops=40)]
[SAY (mode=pause): This is the moment everything's been leading to.]
[SAY (mode=autoplay): Press the <strong>play button</strong> in the centre of the visualizer.]
[SAY (mode=autoplay): Watch your machine fall into rhythm with the metronome — speeding up and easing off exactly with the beat.]
[SAY (mode=autoplay): That's me, moving it. No hands, no effort from you — just the beat.]
[SAY: While it's running, find the <strong>emergency stop</strong> — just press <strong>Alt+Space</strong> and everything halts instantly. 
Try it once now, so your fingers know exactly where it is before you ever need it.]
[CHOICE]
[OPTION (target=setup-7, color=#5d4037): ➡️ Continue]

[PAGE: setup-7]  All set
[IMAGE: setup-solo/92231232_032_968c.jpg]
[SAY: <em>Perfect. Everything's working.</em>]
[SAY: Your machine connects, it responds, and it moves to the beat. That's all we needed to see.]
[SAY: When you're ready to actually <em>feel</em> it, head to the <strong>Tutorial</strong> — I'll guide you through your first real run... <em>gently, I promise</em>.]
[CHOICE]
[OPTION (target=tut-1, color=#2e7d32): 🎓 Continue to the Tutorial]
[OPTION (target=004-menu, color=#5d4037): ↩️ Back to the main menu]

---

# Tutorial  (SOLO ONLY — the model alone; gentle, low BPM)

[PAGE: tut-1]  Get comfortable
[IMAGE: tutorial-solo/31517085_010_482d.jpg]
[SAY: <em>No rush now. This first time is all about you getting comfortable with me.</em>]
[SAY: Before we start, let's set the scene:]
[SAY: 🍆 <strong>Choose a dildo you're completely comfortable with</strong> — for your first time, go smaller and familiar, never bigger. Too much and this becomes far too intense; the right size lets you relax and enjoy.]
[SAY: 💦 <strong>Lube generously</strong> — far more than feels necessary. Then add a little more.]
[SAY: 🛋️ Get into a relaxed, comfortable position with the machine settled where you want it.]
[SAY: 🎚️ In the app's <strong>Sound</strong> mode, set <strong>THRUST RHYTHM</strong> to <strong>1 beat per stroke</strong> — so every tick of the metronome is one full, deliberate stroke.]
[SAY: 🛑 Emergency stop is still just <strong>Alt+Space</strong>, any time you want it.]
[SAY: Take a slow breath. When you're ready, and not a moment before, we'll begin.]
[GOTO: tut-2]

[PAGE: tut-2]  Ease yourself onto it
[IMAGE: tutorial-solo/31517085_012_d5b4.jpg]
[SAY: Before I take over, let's get you settled onto it — at your own pace, by your own hand.]
[SAY: 1. Position yourself against the dildo and, slowly, <strong>ease yourself onto it</strong>.]
[SAY: 2. Gently <strong>move up and down</strong> along its length — no machine yet, just you — until it feels easy and comfortable.]
[SAY: 3. Add more lube if you need it. There's no rush at all.]
[SAY: <em>Take all the time you want. When it feels good and you're ready to hand control to me… turn the page.</em>]
[GOTO: tut-3]

[PAGE: tut-3]  The first, slow strokes
[IMAGE: tutorial-solo/31517085_014_7a54.jpg]
[PAUSE (secs=10)]
[SAY: <em>Here we go. Stay just like that for me… let the anticipation build.</em>]
[METRONOME (bpm=20, secs=45)]
[SAY: <em>There it is. Feel me start to move.</em>]
[SAY: <em>Slow… easy… there's nowhere to be but right here.</em>]
[SAY: <em>Don't tense up. Let it glide. Let me do all the work.</em>]
[PAUSE (secs=12)]
[SAY: <em>…and stop. Feel that sudden emptiness? Hold onto that wanting for me.</em>]
[GOTO: tut-4]

[PAGE: tut-4]  Settling into a rhythm
[IMAGE: tutorial-solo/73573492_002_8fd7.jpg]
[METRONOME (bpm=40, secs=50)]
[SAY: <em>Here I come again — a little more sure of myself this time.</em>]
[SAY: <em>Breathe with it. In… and out… let every stroke sink in a little deeper.</em>]
[SAY: <em>See how easy this is? You don't have to do a thing. Just feel.</em>]
[PAUSE (secs=12)]
[SAY: <em>Quiet again. I love teasing you like this — wanting just a little more than I'm giving.</em>]
[GOTO: tut-5]

[PAGE: tut-5]  Teasing you
[IMAGE: tutorial-solo/73573492_003_bc6d.jpg]
[METRONOME (bpm=50, secs=30)]
[SAY: <em>Mmm, there. Just enough to keep you needing it.</em>]
[PAUSE (secs=15)]
[SAY: <em>And gone again. Patience. I decide when you get more.</em>]
[IMAGE: tutorial-solo/73573492_008_e504.jpg]
[METRONOME (bpm=50, secs=30)]
[SAY: <em>Back so soon? You're starting to crave the beat now, aren't you.</em>]
[PAUSE (secs=15)]
[SAY: <em>Feel how much you miss it the instant it stops. That ache is the whole point.</em>]
[GOTO: tut-6]

[PAGE: tut-6]  A taste of what's coming
[IMAGE: tutorial-solo/73573492_010_a1a9.jpg]
[METRONOME (bpm=60, secs=40)]
[SAY: <em>Now… let me show you just a little more.</em>]
[SAY: <em>Feel me quicken? A hint of how relentless I can be when I really want you.</em>]
[SAY: <em>That's it — let go, let it build…</em>]
[IMAGE: tutorial-solo/31517085_015_7eac.jpg]
[METRONOME (bpm=80, secs=8)]
[SAY: <em>…faster — feel that? That's barely the beginning.</em>]
[PAUSE (secs=18)]
[SAY: <em>…and that's where I'll leave you.</em>]
[SAY: <em>Aching, wet, and wanting — exactly how you should be before the real thing.</em>]
[SAY (mode=pause): <em>Everything you just felt? That was me being gentle. If you dare, start the real tease and find out what happens when I'm not.</em>]
[GOTO: tut-7]

[PAGE: tut-7]  Ready for the real thing
[IMAGE: tutorial-solo/73573492_004_55a3.jpg]
[SAY: So… do you think you can handle some real fucking now?]
[CHOICE]
[OPTION (target=tease-1, color=#d81b60): 🔥 Start The Fucking Machine Tease]
[OPTION (target=004-menu, color=#5d4037): ↩️ Back to the main menu]

---

# The Fucking Machine Tease  (the main event; escalates solo → partner → hard → climax → afterglow)

[PAGE: tease-1]  You came back
[IMAGE: tease-solo-build/62413076_001_35c5.jpg]
[SAY: <em>So. You came back for the real thing.</em>]
[SAY: Welcome to <strong>The Fucking Machine Tease</strong> — the one I've been saving for you.]
[SAY: No more gentle introductions, no more easing you in. Tonight I take over completely… and you will let me.]
[GOTO: tease-2]

[PAGE: tease-2]  A little honesty first
[IMAGE: tease-solo-build/97162788_016_afda.jpg]
[SAY: Before we begin, here's what you're agreeing to.]
[SAY: This runs about <strong>20 minutes</strong> — and then I don't stop. I keep going until you fall apart for me, however long that takes.]
[SAY: So get everything you need within reach now. Once I start, I really don't like to stop.]
[CHOICE]
[OPTION (target=tease-3, color=#d81b60): 🔥 I'm ready — take me]
[OPTION (target=004-menu, color=#5d4037): ↩️ Not yet — back to the menu]

[PAGE: tease-3]  Is your machine ready for me?
[IMAGE: tease-solo-build/97162788_039_a0e2.jpg]
[SAY: First, tell me — is everything set up?]
[SAY: I need the app <strong>installed, paired, and listening</strong> before I can move you a single inch.]
[CHOICE]
[OPTION (target=tease-4, color=#2e7d32): ✅ All set up and connected]
[OPTION (target=setup-1, color=#1976d2): ⚙️ I haven't set it up yet]

[PAGE: tease-4]  Choose well
[IMAGE: tease-solo-build/97162788_025_5fc9.jpg]
[SAY: <em>Choose carefully — you'll be living with this decision for a while.</em>]
[SAY: 🍆 <strong>Pick a dildo you completely trust</strong> — familiar, comfortable, and <strong>never bigger than you know you can take</strong>. I don't ease off the way I did in the tutorial; tonight I get rough. The right size is what lets you surrender instead of brace.]
[GOTO: tease-5]

[PAGE: tease-5]  Get ready for me
[IMAGE: tease-solo-build/97162788_030_aa99.jpg]
[SAY: Now let's get you slick and the app listening.]
[SAY: 💦 <strong>Lube far more than feels necessary</strong> — then add more. We'll be at this a long while.]
[SAY: 🎚️ In the app's <strong>Sound</strong> mode, set <strong>THRUST RHYTHM</strong> to <strong>1 beat per stroke</strong>, then press start so it's listening for me.]
[GOTO: tease-6]

[PAGE: tease-6]  How to stop me
[IMAGE: tease-solo-build/97162788_048_88f1.jpg]
[SAY: Knowing how to stop me is exactly what lets you let go of everything else.]
[SAY: 🛑 <strong>Alt+Space</strong> halts everything instantly — it works even when the app isn't the window you're looking at.]
[SAY: 🆘 At the very end you'll have two buttons: one for when you come, and a <strong>mercy</strong> button for when you simply can't take any more. <strong>Either one is completely okay.</strong>]
[SAY: 💧 Keep water and extra lube close.]
[SAY: You're safe with me. That's the whole reason you can give in.]
[GOTO: tease-7]

[PAGE: tease-7]  Ease yourself on  (explicit consent button — only way forward)
[IMAGE: tease-solo-build/97162788_083_d64f.jpg]
[SAY: By your own hand now — get yourself onto it.]
[SAY: 1. Slowly <strong>ease yourself onto</strong> the dildo.]
[SAY: 2. Gently <strong>move up and down</strong> — just you, no machine — until it feels easy and welcoming.]
[SAY: 3. More lube if you want it. No rush.]
[SAY: <em>When you're settled, and aching to be taken… press the button below. Once you do that... you're mine.</em>]
[CHOICE]
[OPTION (target=tease-8, color=#d81b60): 🔥 I'm yours — take me]

[PAGE: tease-8]  Hands off
[IMAGE: tease-solo-build/97162788_111_d82d.jpg]
[PAUSE (secs=12)]
[SAY: <em>Good. Now hold perfectly still for me.</em>]
[SAY: <em>Hands off. From this moment you don't move — I do. All you have to do is feel, and obey.</em>]
[SAY: <em>Here we go.</em>]
[GOTO: tease-9]

[PAGE: tease-9]  Act I · Slow seduction
[IMAGE: tease-solo-build/97162788_113_dce9.jpg]
[METRONOME (bpm=20, secs=45)]
[SAY: <em>There. Feel me take that very first stroke.</em>]
[SAY: <em>Slow… deliberate… I'm in no hurry with you tonight.</em>]
[SAY: <em>Breathe out for me. Let your whole body go soft and heavy.</em>]
[IMAGE: tease-solo-build/97162788_126_1fb3.jpg]
[METRONOME (bpm=30, secs=45)]
[SAY: <em>A little more now. Let it open you up.</em>]
[SAY: <em>Let your body remember who's in charge here.</em>]
[SAY: <em>Mmm… you're already starting to give in, aren't you.</em>]
[SAY: <em>That's it. No thinking. Just me, moving you.</em>]
[PAUSE (secs=10)]
[SAY: <em>…and still. Feel how much you already miss me?</em>]
[IMAGE: tease-solo-build/97162788_076_5e12.jpg]
[METRONOME (bpm=50, secs=50)]
[SAY: <em>There I am again.</em>]
[SAY: <em>Settle into it — this is mine to decide now, not yours.</em>]
[SAY: <em>Feel how easy it is to stop holding on?</em>]
[SAY: <em>Good. Let every stroke pull you a little deeper under.</em>]
[SAY: <em>God, I love this — feeling you give yourself up to me, a little more with every stroke.</em>]
[IMAGE: tease-solo-build/97162788_065_bfb3.jpg]
[METRONOME (bpm=50, secs=30)]
[SAY: <em>You're already learning to just… let go.</em>]
[SAY: <em>See? You don't have to do a single thing but feel.</em>]
[SAY: <em>You have no idea what it does to me, watching you let go like this.</em>]
[GOTO: tease-10]

[PAGE: tease-10]  Act II · The first rise
[IMAGE: partner-build/47702997_013_2f18.jpg]
[METRONOME (bpm=60, secs=40)]
[SAY: <em>Now we climb. Stay with me.</em>]
[SAY: <em>Feel me pick up the pace, bit by bit.</em>]
[SAY: <em>No more easing in — I want more of you now.</em>]
[SAY: <em>And I want this for me too — this is where I start taking my pleasure from you.</em>]
[IMAGE: partner-build/47702997_019_6af5.jpg]
[METRONOME (bpm=80, secs=40)]
[SAY: <em>Feel that? I'm not teasing anymore.</em>]
[SAY: <em>This is me starting to take what I want.</em>]
[SAY: <em>And you're letting me. God, you're letting me.</em>]
[SAY: <em>Feel what that does to me — your surrender is what tips me over.</em>]
[IMAGE: partner-build/47702997_021_a776.jpg]
[METRONOME (bpm=100, secs=30)]
[SAY: <em>Harder. You can take it — I know exactly what you can take.</em>]
[SAY: <em>Don't tense up — open up, and let me in deeper.</em>]
[SAY: <em>Mmm… you feel so good I can barely keep myself slow.</em>]
[IMAGE: partner-hard/47702997_014_214e.jpg]
[METRONOME (bpm=120, secs=60)]
[SAY: <em>There it is. This is what you came back for.</em>]
[SAY: <em>Don't fight it. Let me fuck you exactly as hard as I please.</em>]
[SAY: <em>Feel me filling you, over and over.</em>]
[SAY: <em>Yes — just like that. You're mine now, completely.</em>]
[SAY: <em>Stay with it… stay right there with me…</em>]
[SAY: <em>Yes — this is exactly what I wanted, and I'm nowhere near satisfied.</em>]
[PAUSE (secs=8)]
[SAY: <em>…and breathe. That was only the first time I take you tonight.</em>]
[GOTO: tease-11]

[PAGE: tease-11]  Act III · Catch your breath
[IMAGE: partner-tender/80877276_050_0956.jpg]
[METRONOME (bpm=40, secs=40)]
[SAY: <em>Slow again. Feel your heart pounding? I did that to you.</em>]
[SAY: <em>You're already mine — we both know it now.</em>]
[SAY: <em>Easy strokes… just enough to keep you simmering for me.</em>]
[SAY: <em>Mmm… I could watch you ache like this for hours. It's delicious to me.</em>]
[PAUSE (secs=12)]
[SAY: <em>Look at you — hips chasing nothing, aching for a rhythm I've taken away.</em>]
[IMAGE: partner-tender/25952977_043_5cbb.jpg]
[METRONOME (bpm=50, secs=30)]
[SAY: <em>Shh. You'll get it back when I decide you've earned it.</em>]
[SAY: <em>There — a little taste. Don't go getting greedy.</em>]
[SAY: <em>See how good you are for me? God, I love you needy like this.</em>]
[PAUSE (secs=10)]
[SAY: <em>Not yet. A little wanting is so good for you.</em>]
[GOTO: tease-12]

[PAGE: tease-12]  Act IV · The ladder  (4 cycles of 60→90→120→150, each rung a distinct partner-hard image)
[IMAGE: partner-hard/88033482_047_5ad6.jpg]
[METRONOME (bpm=60, secs=8)]
[SAY: <em>Up… and up…</em>]
[IMAGE: partner-hard/88033482_052_09cb.jpg]
[METRONOME (bpm=90, secs=8)]
[IMAGE: partner-hard/88033482_055_41db.jpg]
[METRONOME (bpm=120, secs=8)]
[IMAGE: partner-hard/88033482_060_eaf4.jpg]
[METRONOME (bpm=150, secs=10)]
[SAY: <em>…harder — all the way — now take it!</em>]
[PAUSE (secs=6)]
[SAY: <em>…and nothing. Again.</em>]
[IMAGE: partner-hard/88033482_063_da8c.jpg]
[METRONOME (bpm=60, secs=8)]
[SAY: <em>Climb for me. Faster.</em>]
[IMAGE: partner-hard/88033482_068_c567.jpg]
[METRONOME (bpm=90, secs=8)]
[IMAGE: partner-hard/88033482_070_b7c5.jpg]
[METRONOME (bpm=120, secs=8)]
[SAY: <em>Don't you dare come yet — I haven't said you can.</em>]
[IMAGE: partner-hard/88033482_076_8630.jpg]
[METRONOME (bpm=150, secs=10)]
[SAY: <em>God, you feel good when you fight it.</em>]
[PAUSE (secs=6)]
[SAY: <em>Stopped again. You hate how much you love this. Again.</em>]
[IMAGE: partner-hard/88033482_081_d8bc.jpg]
[METRONOME (bpm=60, secs=8)]
[SAY: <em>Yes — all the way up—</em>]
[IMAGE: partner-hard/88033482_082_6803.jpg]
[METRONOME (bpm=90, secs=8)]
[IMAGE: partner-hard/47702997_012_0a5f.jpg]
[METRONOME (bpm=120, secs=8)]
[IMAGE: partner-hard/47702997_015_7bd9.jpg]
[METRONOME (bpm=150, secs=10)]
[SAY: <em>—give it to me, don't hold anything back.</em>]
[PAUSE (secs=6)]
[SAY: <em>Breathe. You're trembling for me now. One more.</em>]
[IMAGE: partner-hard/47702997_016_b65e.jpg]
[METRONOME (bpm=60, secs=8)]
[SAY: <em>Last climb now—</em>]
[IMAGE: partner-hard/47702997_017_948c.jpg]
[METRONOME (bpm=90, secs=8)]
[IMAGE: partner-hard/48437305_054_ed4c.jpg]
[METRONOME (bpm=120, secs=8)]
[IMAGE: partner-hard/48437305_069_68d4.jpg]
[METRONOME (bpm=150, secs=10)]
[SAY: <em>—everything you have — give it all to me!</em>]
[SAY: <em>Yes — all of it — I want it.</em>]
[PAUSE (secs=6)]
[SAY: <em>…and stop. Good. So good for me.</em>]
[GOTO: tease-13]

[PAGE: tease-13]  Act V · Story interlude
[IMAGE: partner-tender/47702997_007_8b36.jpg]
[METRONOME (bpm=30, secs=40)]
[SAY: <em>Now I want you slow. Achingly slow.</em>]
[SAY: <em>Feel every single inch.</em>]
[SAY: <em>There's no rushing what I'm going to do to you.</em>]
[SAY: <em>I want to savour every inch — this is for me too, make no mistake.</em>]
[IMAGE: partner-tender/62413076_009_a501.jpg]
[METRONOME (bpm=50, secs=30)]
[SAY: <em>Closer now… let it build… right up to the edge…</em>]
[SAY: <em>Almost — almost — don't you dare tip over without me.</em>]
[SAY: <em>I'm right there aching with you — I want us teetering together.</em>]
[PAUSE (secs=12)]
[SAY: <em>…and stop. Not yet. Not until I say so.</em>]
[SAY: <em>Stay right there, trembling on the edge. I love you most like this.</em>]
[GOTO: tease-14]

[PAGE: tease-14]  Act VI · The climb
[IMAGE: partner-build/47702997_022_a147.jpg]
[METRONOME (bpm=100, secs=40)]
[SAY: <em>No more mercy now. We climb — and this time we don't come back down.</em>]
[SAY: <em>Feel me settle into a harder rhythm.</em>]
[SAY: <em>Match your breath to it… there.</em>]
[SAY: <em>God, I've wanted this — wanted you — all night long.</em>]
[IMAGE: partner-hard/25952977_049_5587.jpg]
[METRONOME (bpm=120, secs=50)]
[SAY: <em>Harder. Feel me taking what's mine.</em>]
[SAY: <em>Deeper now, with every single stroke.</em>]
[SAY: <em>You couldn't stop me even if you wanted to — and you don't.</em>]
[SAY: <em>Give in to it. Give in to me.</em>]
[SAY: <em>I'm taking my pleasure now, and I won't be gentle.</em>]
[IMAGE: partner-hard/25952977_070_cc29.jpg]
[METRONOME (bpm=140, secs=50)]
[SAY: <em>Don't hold back from me — I certainly won't.</em>]
[SAY: <em>Faster. Feel just how relentless I can be.</em>]
[SAY: <em>This is exactly what I promised you.</em>]
[SAY: <em>Stay with me — we are not slowing down.</em>]
[SAY: <em>Yes — just like that — you feel far too good to stop.</em>]
[IMAGE: partner-hard/25952977_075_9bee.jpg]
[METRONOME (bpm=160, secs=40)]
[SAY: <em>Almost there. Let it build, let it build…</em>]
[SAY: <em>Higher. I can feel you trembling around me.</em>]
[SAY: <em>Don't fight the edge — race toward it.</em>]
[SAY: <em>I'm so close myself — take me there with you.</em>]
[IMAGE: partner-hard/48437305_008_ece4.jpg]
[METRONOME (bpm=170, secs=30)]
[SAY: <em>Right to the edge now. Hold it for me.</em>]
[SAY: <em>So close… I want you aching for it.</em>]
[SAY: <em>Right there with you — I want us to break apart together.</em>]
[GOTO: tease-15]

[PAGE: tease-15]  Act VII · The climax moment
[IMAGE: climax/14017489_008_ba5f.jpg]
[METRONOME (bpm=180, secs=40)]
[SAY: <em>Now. This is it.</em>]
[SAY: <em>I'm going to fuck you straight over that edge — don't you dare resist me.</em>]
[SAY: <em>Feel it building, building, nowhere left to hide.</em>]
[SAY: <em>I'm almost there too — you're going to take me over with you.</em>]
[IMAGE: climax-hero/14017489_012_173f.jpg]
[NOTIFICATION (id=climax-btn, target=tease-17): I'm coming!]
[METRONOME (bpm=190, secs=40)]
[SAY: <em>Come for me. Right now. Give me every last bit of you.</em>]
[SAY: <em>Let go — completely — I've got you.</em>]
[SAY: <em>God — yes — you're pulling me over the edge with you—</em>]
[SAY: <em>That's it — let go — <strong>come!</strong></em>]
[GOTO: tease-16]

[PAGE: tease-16]  The loop · Relentless finish  ([GOTO: tease-16] loops; the buttons exit)
[IMAGE: partner-hard/48437305_038_e870.jpg]
[NOTIFICATION (id=climax-btn, target=tease-17): I'm coming!]
[NOTIFICATION (id=mercy-btn, target=tease-18): I can't take any more]
[METRONOME (bpm=120, secs=15)]
[SAY: <em>Still holding on? Mmm. Stubborn thing. I like that.</em>]
[IMAGE: partner-hard/48437305_041_5738.jpg]
[NOTIFICATION (id=climax-btn, target=tease-17): I'm coming!]
[NOTIFICATION (id=mercy-btn, target=tease-18): I can't take any more]
[METRONOME (bpm=150, secs=15)]
[SAY: <em>Then I won't stop. I'll just keep taking you, and taking you.</em>]
[IMAGE: partner-hard/48437305_087_f74f.jpg]
[NOTIFICATION (id=climax-btn, target=tease-17): I'm coming!]
[NOTIFICATION (id=mercy-btn, target=tease-18): I can't take any more]
[METRONOME (bpm=180, secs=20)]
[SAY: <em>Come on — come for me. Let go. You know you want to give in.</em>]
[SAY: <em>Every time you hold back, I only want you more.</em>]
[IMAGE: partner-hard/48437305_113_a7cc.jpg]
[NOTIFICATION (id=climax-btn, target=tease-17): I'm coming!]
[NOTIFICATION (id=mercy-btn, target=tease-18): I can't take any more]
[METRONOME (bpm=140, secs=15)]
[SAY: <em>Catch your breath… but I am nowhere near finished with you.</em>]
[IMAGE: partner-hard/14017489_006_c7f9.jpg]
[NOTIFICATION (id=climax-btn, target=tease-17): I'm coming!]
[NOTIFICATION (id=mercy-btn, target=tease-18): I can't take any more]
[METRONOME (bpm=170, secs=20)]
[SAY: <em>Again. Right back to the edge. Fall for me.</em>]
[SAY: <em>I could do this to you all night — and I just might.</em>]
[GOTO: tease-16]

[PAGE: tease-17]  Outro · Afterglow  (reached by the climax button)
[IMAGE: afterglow/54365525_016_0261.jpg]
[METRONOME (bpm=30, secs=20)]
[SAY: <em>There it is. Good. That's exactly what I wanted from you.</em>]
[SAY: <em>Shh… I've got you. Just a little more, soft and slow now — feel me ease you down.</em>]
[IMAGE: afterglow/54365525_101_5ac1.jpg]
[METRONOME (bpm=20, secs=15)]
[SAY: <em>Easy… easy… there's no rush at all.</em>]
[PAUSE (secs=20)]
[SAY: <em>And… rest. You were perfect.</em>]
[SAY: <em>Take all the time you need to come back to me. You did so beautifully — I'm proud of you, and you should be too.</em>]
[END]

[PAGE: tease-18]  Outro · Gentle aftercare  (reached by the mercy button)
[IMAGE: afterglow/54365525_044_d62e.jpg]
[METRONOME (bpm=20, secs=15)]
[SAY: <em>Of course. That's all you ever had to say.</em>]
[SAY: <em>I'm slowing right down for you… there. All stopped. You're safe.</em>]
[PAUSE (secs=20)]
[SAY: <em>No disappointment here — none at all. You gave me so much tonight, and I loved every second of it.</em>]
[SAY: <em>Breathe. Drink some water. Be gentle with yourself, the way I'd be with you.</em>]
[SAY: <em>You were wonderful. Come back to me whenever you want more.</em>]
[END]

---

# About + Supported Devices  (meta/credits pages — out of persona; about-hero image)

[PAGE: about]  About this tease
[IMAGE: about-hero/66348606_002_b404.jpg]
[NOTIFICATION (id=devices-btn, target=devices): Supported Devices]
[SAY: <strong>The model</strong> — every image in this tease is of <strong>Leah Gotti</strong>.
          <strong>The app and the tease</strong> — HismithController and this tease were created by <strong>SimonsSecrets</strong>.
                    ❤️ <strong>Enjoying it?</strong> If you'd like to support my work, you can buy me a coffee at <a href='https://ko-fi.com/simonssecrets'>ko-fi.com/simonssecrets</a>. It genuinely helps.
                    💬 <strong>Found a bug, or want your machine supported?</strong> I'd love to help. 
                     Please get in touch at <a href='mailto:simonssecrets@gmail.com'>simonssecrets@gmail.com</a>. Your reports and requests are exactly what makes the next version better.
]
[SAY: Thank you for playing! <strong><span style="color: #f06292"><3</span></strong>]
[CHOICE]
[OPTION (target=004-menu, color=#5d4037): ↩️ Back to the menu]

[PAGE: devices]  Will my machine work?
[IMAGE: setup-solo/66348606_028_5c9e.jpg]
[SAY: ✅ <strong>Confirmed working</strong>
• <strong>Hismith Premium 3.0 Pro</strong> (AK-01 Series)
👍 <strong>Should work</strong> — same legacy protocol (but not tested)
• <strong>Table Top 2.0 / 2.0 Pro / Max</strong>, <strong>Double Penetration (2.0 Pro)</strong>
• <strong>Pro Traveler</strong>, <strong>Capsule</strong>, <strong>G011</strong>, <strong>Thrusting Cup</strong>, <strong>Wildolo</strong>.
❌ <strong>Not yet supported</strong> — These will probably connect but won't respond to the app.
• <strong>Premium 4.0 Pro</strong>, <strong>Hismith Servo / Servok</strong>, <strong>Hismith Mini Pro</strong>
•  The newer <strong>HISMITH S1 / S2 / S3</strong> and Premium 4.0 generation
]
[SAY: 💬 <strong>Not sure, or don't see your machine?</strong> Just connect it once — the app reads the model when it pairs. And if your device isn't supported yet, get in touch at <a href='mailto:simonssecrets@gmail.com'>simonssecrets@gmail.com</a>.]
[CHOICE]
[OPTION (target=004-menu, color=#5d4037): ↩️ Back to the menu]
