<p align="center"><img src="https://raw.githubusercontent.com/hubertdungen/SpaceBourne2-HOTAS/main/docs/logo.png" alt="SB2 HOTAS" width="520"></p>

# SB2 HOTAS: real flight controls and head tracking for SpaceBourne 2

Fly SpaceBourne 2 like a proper space sim. This mod supports:

- your joystick, throttle and rudder pedals, a full HOTAS, or a HOSAS (two sticks) — anything Windows sees as a game controller;
- head tracking (OpenTrack), to look around the cockpit;
- triple monitors and ultrawide screens, with a proper wide view and the HUD on the centre screen.

The game executable is never modified, and you can remove everything with one click.

**[Download the latest release](https://github.com/hubertdungen/SpaceBourne2-HOTAS/releases/latest)** · [Discussion on Steam](https://steamcommunity.com/app/1646850/discussions/0/565923139446378768/) · [Report a bug or share a profile](https://github.com/hubertdungen/SpaceBourne2-HOTAS/issues)

<p align="center"><img src="https://raw.githubusercontent.com/hubertdungen/SpaceBourne2-HOTAS/main/docs/cockpit.png" alt="Cockpit view with head tracking" width="760"></p>

## Features

- **Analog flight.** Pitch, roll, yaw, throttle, strafe and lift, each on any axis of any controller. You can mix devices: a stick from one brand, pedals from another.
- **Head tracking.** OpenTrack moves your head in the cockpit. It works with a plain webcam, so you don't need special hardware.
- **Look axes.** No head tracker? Put Look Yaw / Look Pitch on a mini-stick to look around.
- **Triple screen and ultrawide.** Stretch the game over three monitors without NVIDIA Surround or AMD Eyefinity, mixed resolutions included. The view widens properly (Hor+) and the HUD stays on the centre screen.
- **Throttle modes.** Choose between:
  - forward only (0–100%);
  - full range with stop in the middle;
  - a separate reverse lever (great on dual-throttle HOTAS);
  - reverse buttons, either hold or toggle.
- **Every control counts.** Buttons, hat directions, rotary selectors, toggle switches, sliders and toe brakes can all press game keys. The game's 26 ship actions (fire, missiles, flares, boost, warp, landing gear, power…) come pre-filled with their default keys.
- **Selectors and switches done right.** Each binding can hold its key, tap once, or tap on both on and off. A rotary selector never jams a key down.
- **Templates.** Ready-made setups for popular controllers, which you can combine. You can also export your own setup and share it.
- **Keyboard still works.** Anything you don't put on a controller stays on keyboard and mouse.
- **Stays out of your way.** Flight control pauses by itself in menus, warp, landing and docking. **Ctrl+Alt+F12** cuts it instantly.
- **Fails safe.** If a game update changes something the mod relies on, the mod switches itself off and the game stays playable.
- **Windows**, plus **Linux / Steam Deck** through Proton (experimental).

## What you need

- **SpaceBourne 2** (Steam or GOG).
- Any **game controllers**: joystick, HOTAS, throttle, rudder pedals.
- Optionally, **[OpenTrack](https://github.com/opentrack/opentrack/releases)** for head tracking (free; a webcam is enough).

Nothing else: no .NET, no mod manager. The download already includes everything, including the mod loader [UE4SS](https://github.com/UE4SS-RE/RE-UE4SS).

## Install (Windows, about 2 minutes)

1. Download `SB2-HOTAS-x.y.z.zip` from the **[latest release](https://github.com/hubertdungen/SpaceBourne2-HOTAS/releases/latest)** and extract it somewhere you keep things, such as `Documents`. Not inside the game folder.
2. Open `bridge\SB2-HOTAS.exe`. If Windows SmartScreen warns you, click **More info → Run anyway**; it warns because the app is new and not code-signed.
3. Click **Install mod…**. The bridge finds the Steam copy of the game by itself. For GOG or another location, answer **No** and pick the game folder.

That's it. **Uninstall mod…** puts the game back exactly as it was. If you already use UE4SS for other mods, SB2 HOTAS reuses it and doesn't touch your other mods.

**Updating.** When a new version is out, the bridge shows a link at the bottom of its window that opens the download page. (It asks GitHub once when it starts, and sends nothing about you.) Extract the new zip, open its bridge and click **Install mod…** again. Your controller setup is kept, because it's saved in `%LOCALAPPDATA%\SB2HOTAS`, not next to the program.

## First flight (about 5 minutes)

All of this happens in the bridge window. The **NEXT STEP** line at the top of the **Axis monitor** always tells you what's missing.

<p align="center"><img src="https://raw.githubusercontent.com/hubertdungen/SpaceBourne2-HOTAS/main/docs/bridge.png" alt="The SB2 HOTAS bridge window" width="760"></p>

1. **Scan devices.** Your controllers appear in the Axis monitor; move them and watch the numbers.
2. **Assign axes.** You have two ways:
   - **Import profile…** and pick a template from `bridge\templates`. Templates are per device and add up: import your stick, then your throttle, then your pedals.
   - Or do it by hand: select a movement (Pitch, Roll…), click **Detect axis**, and move only that control.
3. **Calibrate axes.** Move every assigned axis end to end, centre the stick and pedals, and click **Finish**. You can do all axes in one go, or one at a time.
4. **Check directions.** Watch the **Normalized** line in the Axis monitor. If something goes the wrong way, tick **Invert** for it.
5. **Buttons.** On the Buttons tab, select an action, click **Detect input**, and press the button you want for it.
6. Click **▶ Play SpaceBourne 2** (or start the game the usual way), click **ARM** in the bridge, and fly.

## The controls, explained

### Flight axes

| Movement | What it does in the game |
|---|---|
| **Pitch** | Nose up/down. Stick forward dives, like a plane. |
| **Yaw** | Nose left/right. Use pedals or stick twist. |
| **Roll** | Rolls the ship, like the game's Q/E keys, with the game's own smoothing. |
| **Throttle** | Forward speed; see the throttle section below. |
| **Strafe / Lift** | Slide sideways or up/down, like A/D and R/V. A throttle mini-stick is perfect for this. |
| **Reverse thrust** | A second lever that pulls the throttle backwards. |
| **Look Yaw / Look Pitch** | Turn your view in the cockpit without head tracking. Full deflection looks 90° to the side and 60° up or down. |

Pitch and yaw move the game's aim point, the way the mouse does, and the ship turns to follow it. **Turn rate** (bottom of the window) sets how fast the aim moves at full deflection.

### Per-axis tuning

- **Invert** flips the direction.
- **Deadzone** ignores small movements around the centre (0–0.4). Raise it if a worn stick creeps.
- **Curve** sets the response: 1 is linear, and higher values give finer control near the centre with full power at the end (0.5–3).

### Throttle

- **Throttle forward only** is ticked by default: the lever goes from 0 to 100%, and you slow down by pulling it back.
- Untick it for **full range**: −100% to +100%, with stop in the middle. Recalibrate afterwards, with the throttle in the middle.
- **Reverse thrust** axis: on a dual-throttle HOTAS (such as the X-56), put one lever on Throttle and the other on Reverse thrust.
- **Throttle reverse (hold)** and **Throttle reverse (toggle)** are special button outputs on the Buttons tab. They flip the throttle into reverse. The monitor shows `[REVERSE]` while reverse is on.

### Buttons, hats, selectors, switches

Any control can press a game key:

- a button;
- a hat direction;
- a spare axis pushed past about a third of its travel, which shows as `Axis 3 +` or `Axis 3 -`. That covers rotaries, sliders and toe brakes.

The **Mode** column sets how each binding presses its key:

| Mode | What it does | Use it for |
|---|---|---|
| **Hold** | Holds the key down while the control is held | Normal buttons |
| **Tap** | One press each time the control turns on | Rotary selectors, latched switches |
| **Tap on/off** | One press when it turns on, another when it turns off | Two-position toggle switches |

You can also manage the rows:

- **Add row** adds a binding.
- **Clear input** unbinds the selected row but keeps its name and key.
- **Reset all…** brings back the 26 game actions.
- **Wheel up** / **Wheel down** send one mouse-wheel notch per press, for the game's speed steps.

If you changed a key in the game's settings, change it in the **Game key** column too.

### While you fly

| Key | What it does |
|---|---|
| **Ctrl+Alt+F12** | Disarms instantly; the game goes back to keyboard and mouse. |
| **Ctrl+Alt+F11** | Recentres your head (OpenTrack). |

Movements that aren't on a controller still answer to the game's default keys:

| Movement | Keys |
|---|---|
| Throttle | W / S |
| Strafe | A / D |
| Lift | R / V |
| Roll | Q / E |

The game's speed lock (Caps Lock) always wins: while it's on, the game holds your speed whatever the throttle says. Pitch and yaw stay on the mouse unless you assign them.

## HOSAS (two sticks)

Two joysticks, one per hand, is the classic space-sim setup, and SB2 HOTAS supports it fully. The bridge reads both sticks at once, and any movement can go on any axis of either stick.

A typical layout:

| Hand | Stick axis | Movement |
|---|---|---|
| Right | forward / back | **Pitch** |
| Right | left / right | **Roll** |
| Right | twist | **Yaw** |
| Left | forward / back | **Throttle**, with *Throttle forward only* unticked |
| Left | left / right | **Strafe** |
| Left | twist | **Lift** |

Setting it up:

1. **Scan devices.** Both sticks appear in the Axis monitor.
2. For each movement, select it, click **Detect axis**, and move **that** stick in **that** direction.
3. **Untick "Throttle forward only"**, so the left stick drives forward and back with stop in the middle.
4. **Calibrate axes.** Move both sticks end to end, then let both centre and click **Finish**.
5. Bind buttons from both sticks on the Buttons tab, the same way.

Some details:

- **Holding a speed.** A spring-centred stick stops the ship when you let go. Bind **Speed lock** (Caps Lock, already in the Buttons tab) to a button. While it's on, the game holds your speed and the stick's throttle is ignored, until you press it again.
- **Two identical sticks** (for example 2× T.16000M, or 2× Gladiator with the same configuration) show up as `Name` and `Name #2`. They're told apart by the USB port they're plugged into. Keep each stick in its own port, and the left one stays left after a reboot. If you swap ports, just Detect axis again.
- **Templates** describe one stick each, so they can't tell a left stick from a right one. For HOSAS, Detect axis is quicker than a template. For two VKB sticks, always use Detect axis.
- **Look axes** also work on a spare analog mini-stick on either stick.

## Head tracking with OpenTrack

You don't need TrackIR: OpenTrack is free, and a normal webcam is enough.

1. Install **[OpenTrack](https://github.com/opentrack/opentrack/releases)**.
2. Choose a **tracker** (the input):
   - **neuralnet tracker**: webcam only, no extra hardware. This is the easiest start.
   - **PointTracker**: for an IR clip or cap, if you have one.
3. Set the **output** to **UDP over network**. Click the hammer icon next to it and set:
   - **Remote IP** to `127.0.0.1`;
   - **Port** to `4242`.
4. Click **Start**. The bridge's bottom line should say **OpenTrack OK**.
5. In the game, switch to the cockpit view. Your head now moves the camera. **Ctrl+Alt+F11** (or OpenTrack's own centre key) recentres it.

Tips:

- If the view moves the wrong way on some axis, invert that axis in OpenTrack (**Options → Output**).
- OpenTrack's **Mapping** curves shape how far the view turns for a given head turn. Gentle curves feel best.
- Head tracking works in the cockpit view only.

## Triple screen and ultrawide

Play on three monitors (or two, or five) as one wide screen, without NVIDIA Surround or AMD Eyefinity. It helps a single ultrawide monitor too.

- **One wide window.** The bridge stretches the game's borderless window over all your monitors.
- **A proper wide view (Hor+).** On its own, SpaceBourne 2 keeps the same horizontal view on any screen shape, so a very wide window would only zoom the picture in and crop the top and bottom. The mod widens the view instead. The centre monitor looks just like a normal 16:9 screen, and the side monitors show more of space around it.
- **HUD on the centre monitor.** The ship panel, messages, warnings and main menu move to the centre monitor, where you can read them, instead of being pushed out to the far edges. Target markers and the crosshair still cover every monitor, so they stay on top of what they point at.

### Set up your monitors, once

1. **Give them the same height.** Three identical monitors are already fine. With mixed monitors, such as 1080p sides next to a 1440p centre, run the side monitors at 2560×1440 with NVIDIA DSR/DLDSR:
   1. NVIDIA Control Panel → **Manage 3D settings** → **Global Settings** → **DSR - Factors**: enable **1.78x** (it may read *1.78x DL*). Click **Apply**.
   2. Windows Settings → **System** → **Display**: select each side monitor and set **Display resolution** to **2560×1440**.

   AMD's *Virtual Super Resolution* does the same, or make a custom resolution with your graphics driver.
2. **Line them up.** In Windows Settings → **System** → **Display**, drag the monitors into one row with their top edges aligned. Keep the centre monitor as your main display.

### Play

1. In SpaceBourne 2's settings, set **Display Mode** to **Windowed Fullscreen** (once).
2. In the bridge, open the **Triple screen** tab and tick **Stretch the game over all my monitors**. This also ticks the other two boxes. The bridge saves your choice, and its status line confirms it.
3. Start the game. Within a second it spreads over all your monitors.

The tab lists the monitors it found and warns you when their heights don't match. Untick the box to put the game back on one monitor. None of this needs ARM.

All three boxes start unticked, so nothing changes until you turn it on. The other two also work on their own:

- **Wider view on wide screens** is the Hor+ fix. It also works on a single ultrawide (21:9, 32:9) or a Surround / Eyefinity screen, with or without the first box.
- **Keep the HUD and menus on the centre monitor**: untick it if you'd rather have the HUD spread over all the screens.

Good to know:

- Three 1440p monitors are about three times the pixels of one, so expect a lower frame rate. The game's resolution scale helps.
- It's one picture over all the monitors, so there's no bezel correction. Angle the side monitors towards you.
- With head tracking, flatten OpenTrack's **yaw** curve: you already turn your head to look at a side monitor, so the in-game view shouldn't turn as well.
- Target markers and the crosshair stay spread over every monitor on purpose: they sit on top of what they point at.
- Tested on a 1920×1080 | 2560×1440 | 1920×1080 row (6400×1440) on Windows 11. On Linux the window stretching hasn't been tested yet.

## Linux / Steam Deck (Proton, experimental)

SpaceBourne 2 runs on Linux through Steam's Proton, and so does SB2 HOTAS. The bridge runs inside the game's Proton prefix, next to the game.

### 1. Install protontricks, once

The bridge needs [protontricks](https://github.com/Matoking/protontricks) to start inside the game's prefix.

- **Steam Deck:** switch to Desktop Mode, open the **Discover** store, search for **Protontricks**, and install it.
- **Other Linux:** install `protontricks` from your package manager, or run `flatpak install flathub com.github.Matoking.protontricks`.

### 2. Install SB2 HOTAS with one command

Open a terminal (on Steam Deck: Konsole, in Desktop Mode) and run:

```
curl -fsSL https://raw.githubusercontent.com/hubertdungen/SpaceBourne2-HOTAS/main/linux-setup.sh | bash
```

This one command does all of this:

- downloads the latest release into `~/SB2-HOTAS`;
- finds the game in your Steam libraries and installs the mod;
- adds **SB2 HOTAS bridge** to your app menu;
- tells you if protontricks is missing.

If your game is somewhere unusual, add its folder at the end: `… | bash -s "/path/to/SpaceBourne 2"`.

To install by hand instead: download the zip from Releases, extract it, and run `bash install.sh` in that folder.

### 3. Set the launch option in Steam, once

In Steam, open SpaceBourne 2 → **Properties** → **General** → **Launch Options**, and enter:

```
WINEDLLOVERRIDES="dwmapi=n,b" %command%
```

Without it, Proton doesn't load the mod loader.

### 4. Every time you play

1. Start SpaceBourne 2 from Steam.
2. Open **SB2 HOTAS bridge** from your app menu. Or run `bash ~/SB2-HOTAS/run-bridge.sh`.
3. Set up and fly exactly as on Windows (see *First flight* above).

On Steam Deck, the bridge is a desktop window, so play in Desktop Mode, or set it up there first and then switch back.

### Head tracking and removing the mod

- **Head tracking:** OpenTrack for Linux works with the same output settings, *UDP over network* on `127.0.0.1:4242`.
- **Removing the mod:** run `bash ~/SB2-HOTAS/install.sh --uninstall`, then clear the launch option in Steam.

This path hasn't been tested on real Linux hardware yet. If you try it, a quick report in Issues helps a lot, whether it worked or not.

## Troubleshooting

**ARM does nothing in flight.** Read the **NEXT STEP** line. Usually an axis still needs **Calibrate axes**. Until every assigned axis is calibrated, ARM only enables head tracking and buttons, and the status line tells you so.

**The stick moves the ship forward or sideways while the bridge is disarmed.** That's the game's own leftover joystick support, which treats your HOTAS like a gamepad. Once you ARM, the mod takes over flight and this stops.

**The cursor or the ship drifts to one side while the game runs.** Most players won't see this. If you do, go in this order:

1. **Look for a forgotten gamepad.** An old wired pad, a USB receiver, or a Bluetooth pad left on: a worn stick on any of them drifts, and the game adds it to your HOTAS. Check Windows' *Set up USB game controllers* and Steam → Settings → **Controller**. Unplug it or switch it off.
2. **Check Steam Input.** In Steam → Settings → **Controller**, turn off Steam Input for generic controllers. You can also do it for this game only: SpaceBourne 2 → Properties → Controller → **Disable Steam Input**.
3. **Only if it still drifts:** your controller is one the game reads wrongly (it happened with an X-56 stick). The bridge reads it correctly, so hide the HOTAS from games with **[HidHide](https://github.com/nefarius/HidHide/releases)** (free):
   1. Install HidHide and restart the PC.
   2. Open the HidHide configuration client.
   3. On the **Applications** tab, add `bridge\SB2-HOTAS.exe`. Add your controller's own software too, if you use it.
   4. On the **Devices** tab, tick your stick, throttle and pedals, then tick **Enable device hiding**.
   5. Unplug and replug the controllers.

   Games now see nothing, and SB2 HOTAS gets everything. For another game that should use the HOTAS directly, add it to HidHide's application list, or untick **Enable device hiding** while you play it.

**Button bindings do nothing.** Run the game and the bridge with the same permissions: both normal, or both as administrator.

**"In-game mod" says "no news yet".** UE4SS didn't load the mod. Click **Install mod…** again. On Linux, check the launch option. The log is in `SB2\Binaries\Win64\UE4SS.log` inside the game folder.

**The bridge window takes up to a minute to appear.** That happens the first time a new version runs: your antivirus checks the new `SB2-HOTAS.exe` before letting it start. Later starts are quick.

**The bridge shows your sticks moving, but the ship ignores them.** Look at the top of the bridge window: if it says *"The game gets no controls"*, Windows isn't letting the bridge update its files, usually because an antivirus is watching `SB2-HOTAS.exe`. Allow it in your antivirus and restart the bridge. If "In-game mod" says *"Waiting for the bridge: no new frames"*, the game isn't getting the bridge's updates: restart the bridge.

**Two identical controllers.** They show up as `Name` and `Name #2`. Your settings live in `%LOCALAPPDATA%\SB2HOTAS\profile.json`.

## Templates

Templates live in `bridge\templates`, one per device:

- **Sticks / HOTAS:** Logitech Extreme 3D Pro; Logitech X-56; Logitech/Saitek X52 and X52 Pro; Thrustmaster T.16000M FCS; T.Flight HOTAS X / 4 / One; HOTAS Warthog; VKB Gladiator NXT EVO.
- **Throttle:** Thrustmaster TWCS.
- **Pedals:** Thrustmaster T.Flight Rudder (TFRP); Logitech/Saitek Pro Flight Rudder.

The X-56 and TFRP ones come from a real setup. The others have known device IDs, but their axis numbers are best guesses. After importing, always calibrate, and re-detect any axis that's off.

Got one working? **Export profile…** and share it in an issue, and it becomes a verified template.

## After a game update

If an update changes the game internals the mod relies on, the mod switches itself off instead of misbehaving. The **In-game mod** line then shows `ERROR: … incompatible game version (property …)`, and keyboard and mouse keep working. Please open an issue with that line.

## How it works

The mod has two parts:

- **Bridge** (`bridge\SB2-HOTAS.exe`). A small Windows app that reads your controllers (through the SDL2 library that ships with the game) and OpenTrack (over UDP). It writes what it reads to `%LOCALAPPDATA%\SB2HOTAS\state.txt` 60 times a second.
- **Mod** (a [UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) Lua mod). Inside the game, it reads that file and steers the ship through the game's own flight code, and moves the cockpit camera with your head. It uses no memory offsets and no executable patches, and it never touches your saves.
- **Triple screen.** The bridge moves and resizes the game's borderless window over your monitors, the way dragging it would, and writes your choices to `screen.txt`. The mod's `screen.lua` then widens the camera's field of view to match the window (Hor+, smoothed so the game's tiny FOV wobble doesn't shimmer), and anchors the HUD widgets to the centre monitor.

## Credits and license

SB2 HOTAS is MIT licensed (see `LICENSE`). It builds on:

- [UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) (MIT), bundled in the download;
- [SDL2](https://www.libsdl.org/) (zlib), from the game's own copy;
- the .NET runtime (MIT), bundled in the bridge.

`THIRD-PARTY-NOTICES.txt` has the details. Thanks to the OpenTrack and HidHide projects. This is a fan project, not affiliated with the developers of SpaceBourne 2.

## Developers: isolated test copy

`Prepare-TestCopy.ps1` builds the bridge and installs the mod into a full copy of the game in `work\sandbox\Game`. `Start-TestCopy.ps1` starts the bridge and that copy. Neither script writes to the Steam install.

The test copy shares the original game's save folder. `Start-TestCopy.ps1` backs it up to `work\backup` first, and refuses to start while the game is already running.

## Developers: release package

`Package.ps1` builds `release\SB2-HOTAS-<version>.zip` with the .NET 10 SDK. Put [UE4SS 3.0.0](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/v3.0.0) (`UE4SS.dll`, `dwmapi.dll`, `UE4SS-settings.ini`) in `..\..\work\tools\ue4ss` first. The version comes from `<Version>` in `source\Bridge\Bridge.csproj`. The packaged README stops before the Developers sections, because the developer scripts are not in the zip.
