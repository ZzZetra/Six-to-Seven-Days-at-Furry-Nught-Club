# AI guide: SSDFNC

**Six to Seven Days at the Furry Night Club**: Unity 6 (6000.6.0f1, URP 17.6) 3D FNAF-like survival
game, Roblox-style models. The player sits in the club's reception office during a day shift and
fends off five furries with four resources: fuel for the generator, the dance floor, the sound
button and the right door, plus a "mog battle" duel with Nova. Early prototype. **Done 2026-10-30:
Nova's four skill-check types, the menu «Скилл-чеки» test tab and the chart editor as a separate .exe** — the user still has
to try them by hand and make charts for real music. Also on the list:
re-tune the balance (10-min shift, mog lives, calls distract; the sim bot ignores calls) and polish the
calls (draft dialogues, no penalty yet, frequency, dialogue editor). See README «Что доработать».
This guide is in English to save tokens; the user, the README, the CHANGELOG and `docs/` are in Russian.

Concept in one screen (details, numbers and the map are in `docs/GAME_DESIGN.md`):
- Office views: **left = generator** (passage Г from corridor 5), **forward = corridor with the dance
  floor**, **right = door to corridor 2**. The prototype's three yaw stops already match this.
- Generator powers everything except the sound button. State: fuel (tank 75, 150 per shift, 145 in
  canisters), HP (100, no repair; Dali deals 20 HP / 2 s), running/stalled (needs a restart).
- Furries: Dir (feed via cameras every ~30 s), Stray (door or dance floor), Nova (mog battle with
  4 skill-check types), Dali (generator → sound button), Doli (door: close it, never sound; or
  generator: sound button).

## Read first

- `README.md`: run, controls, status, file layout.
- `docs/ARCHITECTURE.md`: how each script and the test scene work, coordinates, known issues.
- `docs/GAME_DESIGN.md`: the design doc (map `docs/img/map.png`, routes, tools, generator numbers,
  furries, mog battle), the mechanic status table and the list of unclear points.
- The code is tiny (4 runtime scripts + 2 editor builders, ~400 lines). Just read it.

## The user and working rules

- Russian-speaking student. Answer in simple Russian, work in small steps, use standard Unity practices.
- **Git since 2026-10-23** (branch `main`, Unity `.gitignore`, LFS for audio/models in `.gitattributes`).
  Commit only when asked, and every commit also updates `README.md` and `CHANGELOG.md` (newest at the top,
  under `## YYYY-MM-DD`). Don't commit `Library`/`Temp`/`Logs`/`UserSettings` or the speech-test venv/models.
- When a mechanic lands, update the status table in `docs/GAME_DESIGN.md`, `docs/ARCHITECTURE.md`
  and `CHANGELOG.md`.
- After changing how something feels (turn speed, timings, drain rates), ask the user to **test it
  by hand**. Don't do long automated tuning.
- The design doc leaves some points open (see "Что в концепте пока неясно" in `docs/GAME_DESIGN.md`).
  Ask the user about the ones the current step depends on instead of inventing answers. Record the
  answers in that doc.

## Tools and paths

- Project: `C:/Users/user/SSDFNC`. Editor: `C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe`.
- Shell: Git Bash (the Bash tool) and PowerShell.
- Headless compile check, or running an editor method (only when the editor is **closed**: Unity
  locks the project, and `Temp/UnityLockfile` exists while it is open):
  ```
  "/c/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe" -batchmode -nographics -quit \
    -projectPath "C:/Users/user/SSDFNC" -executeMethod TestPlatformBuilder.Build -logFile <scratch>/unity.log
  ```
  Takes ~30 s. Then grep the log for `error CS`, `warning CS` and `[SSDFNC]`. The builders are
  `TestPlatformBuilder.Build` (office) and `MainMenuBuilder.Build` (menu + Build Settings); one
  `-executeMethod` per run. Drop `-executeMethod` for a plain compile check. Builders ask to save
  modified scenes only outside batchmode (`Application.isBatchMode`).
- If the editor is open (it locks the project), test on a copy. `cp -r Assets Packages
  ProjectSettings Library` into the scratchpad (2.4 GB, ~1 min). Add an `[InitializeOnLoad]` editor
  harness: `-executeMethod` → `EditorApplication.EnterPlaymode()`, log `playModeStateChanged`,
  `EditorApplication.Exit` on edit mode or on a timeout. Add a runtime `[RuntimeInitializeOnLoadMethod]`
  driver that clicks through the flow and logs `REPRO ...`. Run without `-quit`. This is how
  the menu → shift → menu cycle and the generator were verified on 2026-10-20. For pictures,
  render `Camera.Render()` into a RenderTexture and save a PNG. Edit-mode renders in batchmode
  show red materials as grey (an artifact); play-mode renders show true colours.
  `EventSystem.RaycastAll` at `cam.WorldToScreenPoint(x)` checks what a click would hit.
- Quick compile check while the editor is open: Unity's Roslyn
  (`Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll` via `Editor/Data/NetCoreRuntime/dotnet.exe`),
  `-nostdlib -define:UNITY_EDITOR`, references `NetStandard/ref/2.1.0/netstandard.dll`,
  `Managed/UnityEngine/{UnityEngine.CoreModule,UIModule,TextRenderingModule,UnityEditor.CoreModule}.dll`,
  `Library/ScriptAssemblies/{UnityEngine.UI,Unity.InputSystem}.dll`, on `Assets/Scripts/*.cs Assets/Editor/*.cs`.
- Speech test tool: `Tools/SpeechTest` (outside Assets). Python 3.14 venv in `.venv` (Python 3.13 on this
  machine has no Tcl/Tk), `vosk` + `sounddevice`, model in `models/vosk-model-small-ru-0.22`.
  `run.bat` opens the GUI; `run.bat --wav f.wav [--grammar] [--words list.txt]` is the headless check.
  Test audio: Windows TTS voice "Microsoft Irina Desktop" (ru-RU) via System.Speech to 16 kHz mono WAV.
  Don't open the user's microphone yourself.
- Heredocs with Cyrillic and quotes break in Git Bash here. Write Python patch scripts to the
  scratchpad with the Write tool.

## Unity pitfalls in this project

- **Input:** `activeInputHandler: 1`, so the Input System only. `UnityEngine.Input.*` throws. Use
  `Keyboard.current` / `Mouse.current` (as `CameraLook` does) or `InputSystem_Actions`.
- **UI:** uGUI with legacy `UnityEngine.UI.Text` and the built-in `LegacyRuntime.ttf`. TMP Essentials
  are not imported. The monitor and buttons are World Space canvases (scale 0.001, so 1000 px = 1 m),
  clicked through `GraphicRaycaster` + `InputSystemUIInputModule`.
- **Bump `TestPlatformBuilder.Version` whenever the builder's output changes.** On script reload
  the editor then asks the user to rebuild the office (EditorPrefs key per project, once per
  session). Without it the user runs new code against the old scene: "я не вижу ген слева" on
  2026-10-21, because the scene was never rebuilt.
- **Both scenes are generated.** `Assets/Editor/TestPlatformBuilder.cs` (SSDFNC → Build Test
  Platform) deletes and rebuilds `TestPlatform`, `Shift` and the `EventSystem` in `SampleScene`,
  overwrites `Assets/Materials/*.mat` and saves. `MainMenuBuilder` (SSDFNC → Build Main Menu)
  recreates `MainMenu.unity` from scratch and sets Build Settings to [MainMenu, SampleScene].
  Manual edits under those roots are lost. Add new scene content to a builder, or put it
  outside those roots. `NewText` / `NewImage` / `Stretch` in `TestPlatformBuilder` are `internal`
  and shared.
- Prefer editor scripts (`[MenuItem("SSDFNC/...")]`) over hand-editing `.unity` YAML. If you change
  a scene on disk, tell the user to reload it in the editor.
- Keep `.meta` files with their assets and never regenerate GUIDs. The scene references scripts
  by GUID: `CameraLook` b71dd7906e149074d85d133eec401b08, `CameraMonitor` 9b3f628fd5d44bf4d855942a6bacf18b.
- **Enter Play Mode Options are on with domain and scene reload disabled** (`EditorSettings.asset`,
  `m_EnterPlayModeOptions: 3`). Static fields and static event subscriptions survive between Play
  sessions. Reset them in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` method, or
  avoid statics.
- `PlayFromMenu` (Editor, `[InitializeOnLoad]`) sets `EditorSceneManager.playModeStartScene` to
  `MainMenu`, so Play always starts at the menu. Toggle: SSDFNC → Play From Main Menu (EditorPrefs).
  A scene that is open in the editor is loaded by `LoadScene` from `Temp/__Backupscenes/0.backup`,
  **with unsaved edits**; other scenes load from disk. In Editor.log that backup load looks like a
  Play exit. A runtime load is preceded by an `UnloadTime:` line; a Play exit is not.
- Runtime logs: `[SSDFNC] Начать ночь`, `[SSDFNC] Смена окончена, возврат в меню`, `[SSDFNC] Выйти`.
  F9 ends the shift instantly (editor / development builds only).
- **Serialized scene values override code defaults, and the user tunes values in code.** They
  edited `ShiftTimer.duration` in the `.cs` file several times and it never applied (the scene had
  600). Inspector instructions didn't land either. So the shift length is now `public const float
  Duration` in code. For game-rule numbers the user will tweak, prefer code constants (or a single
  obvious config) over serialized fields, or warn clearly when a field lives in the scene.
- `Color * float` also scales alpha. `CameraMonitor` resets `a = 1` after tinting.
- Legacy `Text` silently drops a line that doesn't fit the rect height. `NewText` sets
  `verticalOverflow = Overflow` for this reason.
- 3D clicks need the `PhysicsRaycaster` on Main Camera (the builder adds it) plus a collider.
  Handlers on a parent get the event via `ExecuteHierarchy`.
- Template leftovers (`TutorialInfo/`, `Readme.asset`, the AI / Visual Scripting / Timeline /
  Collab packages) are unused. Don't remove them without asking.

## Architecture (short)

- `CameraLook` (on Main Camera at (0, 1.6, 0)): yaw snaps between −90 / 0 / +90 with A/D or the
  arrow keys. Exponential slerp (`turnSharpness` 8). New input is accepted only within 0.5° of the
  target, and presses during a turn are dropped. Clamped, no wrap. Shows and unlocks the cursor.
- `CameraMonitor` (on `MonitorPanel`): 6 buttons → `Select(i)` tints the screen with the camera's
  colour ×0.6 and shows its number. `Select(-1)` means idle/off. No real camera feeds yet.
- `TestPlatformBuilder` (Editor only): center platform 8×8, three 10×3 paths with a prop at each end
  (left red cube, forward green sphere, right blue cylinder), the monitor 28° right of forward
  (visible only when facing forward), and the buttons below it.
- `MainMenu` (on the `Menu` overlay canvas in `MainMenu.unity`): wires `startButton` → load
  `SampleScene`, `quitButton` → `Application.Quit()` (stops Play mode in the editor).
- `ShiftTimer` (on `Shift` in the office): `const Duration` = `Balance.ShiftDuration` (600 s = 10 min,
  as designed; the user switched from 90 s on 2026-10-23), `TimeLeft` = `Duration − elapsed`, `clock` shows `m:ss` top-right, at 0
  loads `MainMenu`. The generator's fuel rate derives from `ShiftTimer.Duration` (150 per shift).
- `CameraLook`: office yaws −90 / 0 / +90 with A / D. `StepForward()` (W / ↑) at yaw −90 walks to
  `generatorSpot` (−4.65, 1.6, 0; pitch 20°) via SmoothDamp (`MoveTime` 0.35). `StepBack()` (S / ↓)
  or `StepRight()` (D) there returns. The user asked for W instead of A to enter the room.
  Commands are accepted only when settled. All tunables are consts.
- Walls with 1.2 × 2.2 doorways at x = ±3.5 (`BuildWallWithDoorway`). The left prop is removed
  (the generator stands there).
- `Generator` (root at x = −6.5 behind the left doorway; `InReach(eye)` ≤ `ReachDistance` 3 m gates
  the handle and refuelling): constants `TankSize` 75,
  `StartReserve` 145, `FuelPerShift` 150, `PourRate` 15, `MaxHealth` 100. States Running / Stalled /
  Broken. Methods `Pour`, `Drain`, `Stall`, `Damage`, `TryStart`; `event PowerChanged(bool)`.
  Burn rate = 150 / `ShiftTimer.Duration`. Debug keys F6 stall, F7 −20 HP, F8 −10 fuel.
  `GeneratorHandle` (3D, `IPointerDownHandler`): pull 0.25 m toward the player → `TryStart()`.
  `GeneratorPanel` + `HoldButton`: lamp, state text, HP/fuel bars (fill `anchorMax.x`), reserve,
  hold "ЗАЛИТЬ" → `Pour`.
- `CameraMonitor.generator`: no power → dark screen, `Select` ignored; on power change → idle.
- Deterrents: `Deterrent` base (`ActivationMode` Pulse / Hold / Toggle, `Press` / `Release`, `IsActive`,
  `Fired`, `ActiveChanged`, `NeedsPower` → auto-off on power loss, overridable `CanActivate`).
  Each subclass sets `const Activation`. `PressControl` (3D, pointer down/up → Press/Release, optional
  `cap` press visual). `DanceFloor` Hold (tiles via MaterialPropertyBlock `_BaseColor` + point light),
  `SoundSignal` Pulse, no power needed (beep clip generated in code), `Door` Toggle (`MaxClosedTime` 20 →
  auto-open + `LockTime` 10; power loss opens it; label shows the countdown). Desk at z ≈ 1.3 with the
  buttons; door lever on the right wall at z = −1.15. To test long timers in play-mode autotests,
  disable `ShiftTimer`, keep the generator fuelled (`Drain(-x)`), and set `Time.timeScale`.
- `SpaceAction`: Space acts where you look (`CameraLook.IsLooking(Side)`, real yaw within 30°, false
  at the generator). Forward: dance floor while Space is held and you look forward. Right: each Space
  press toggles the door. Mouse clicks still work.
- Keyboard in batchmode autotests: add a virtual `Keyboard` (`InputSystem.AddDevice`, `MakeCurrent`,
  `QueueStateEvent(new KeyboardState(Key.Space))`, wait 2 frames). Also set
  `InputSystem.settings.editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView` and
  `backgroundBehavior = IgnoreFocus`; otherwise keys are dropped because no Game view has focus.
- **Furries** (docs/FURRY_AI.md): pure logic in the `SSDFNC.Core` asmdef (`Assets/Scripts/Core`, no
  UnityEngine scene types): `Balance` (ALL game numbers; Generator/Door/ShiftTimer consts alias it),
  `Difficulty` (presets, `Custom`, ramp), `Room`/`Rooms`/`IOffice`, `Furry`/`Roamer`, `StrayAI`,
  `NovaAI`, `DaliAI`, `DoliAI`, `DirAI`, `Night`, `Sim/NightSimulator` (SimOffice + SimBot + Report).
  `[InternalsVisibleTo("SSDFNC.Tests.EditMode")]` lives in Balance.cs. `NightDirector` (scene)
  implements IOffice, avatars, death → reload scene, `MogBattle`. `CameraMonitor` has
  8 cams (`Rooms.Cameras`) and the Dir feed hold button.
- **Nova's attack (mog battle), user's spec 2026-10-23:** phases Intro (Nova turns the player to the
  dance floor: `CameraLook.InputLocked` + `FaceForward()`, capsule anim) → 5 circle → 5 bar → 5 sides →
  Outro (leaves / death). Phase text top right under the clock. Others idle (`Night.Frozen`).
  Skill checks are NOT hard-coded: the user will add many music tracks and hand-tune checks per track,
  so they are data — `MogChart` json in `Assets/StreamingAssets/MogCharts` (format `docs/MOG_CHARTS.md`,
  `examples/` is not loaded), run by `MogTimeline` (Core, scene-free, tested in `MogTests`; the clock is
  music time). Loss rule (user, 2026-10-23): "mog lives" start 3, hit +1 capped at 4, miss −1,
  0 = Nova kills at once, finish with ≥1 = survive (`Balance.MogStartLives/MaxLives/LivesPerHit`).
- **Skill checks (user's spec 2026-10-30, docs/MOG_CHARTS.md):** four types — circle (DbD needle, Space in
  the zone), button (osu ring shrinks onto it, LMB on the beat), arrow (right/up/left/down, swipe the mouse on
  the beat — changed by the user 2026-10-30: the arrow just WAITS for the swipe while its ring closes (ring =
  time left, deadline `hit + MogHitWindow`), wrong direction is ignored, too late = red miss burst and the next
  check; `Mouse.delta` summed while the arrow is active, `Balance.MogSwipeDistance` × screen height), bar (Space;
  speed changes via `speedKeys`, bounces at the ends). Button/arrow window ±`Balance.MogHitWindow`. Phases
  come from the chart: a run of same-type checks = one phase. One check on screen; the current one is missed
  when the next is already hittable (`MogCheck.ReadyTime`), not when the next merely starts — dense charts
  showed the next ring half-shrunk. Old `sides`/speedless bars still load. Window built by
  `MogBattleBuilder` (office `FurryHUD`, menu `MogTestHUD`, editor `MogPreviewHUD`); rings are `MogShapes`.
  Menu tab «Скилл-чеки» (`MogTestTab`, sees `examples/`; Nova's attack loads top-level files only) runs
  `MogBattle.BeginTest` (no Nova intro, starts 0.5 s before the first check).
  User's follow-up 2026-10-30: checks go one after another with NO «Приготовься» (the next check is drawn as
  soon as the previous resolves and waits, button faded); the button must look like osu (white border, ring
  3× fading in and closing exactly on the border, hit/miss burst); and the chart editor must be a
  **separate .exe program**, with the check edited right in its preview.
  **Chart editor** = runtime IMGUI scene `MogEditor` (`MogChartEditor`) built as its own player by
  `MogEditorBuilder.BuildApp` (SSDFNC → Chart Editor → Build App; temporarily swaps productName/windowed
  1600×900/resizable and restores them; writes the project charts folder into
  `<app>_Data/StreamingAssets/chart_editor_folder.txt`) → `Builds/ChartEditor/SSDFNC Chart Editor.exe`.
  In Unity: SSDFNC → Chart Editor → Open in Play Mode (`PlayFromMenu` skips the menu for that scene). Not in
  the game's Build Settings; no menu button. `MogCharts.Folder` is settable (app folder choice, PlayerPrefs
  `SSDFNC.ChartEditor.Folder`). Open/import use `NativeFileDialog` (comdlg32 `GetOpenFileNameW` in the player,
  `EditorUtility.OpenFilePanel` in Unity). The selected check is shown by the real `MogBattle` in a preview
  rect (`FitPanel`, `ShowStill`), mouse edits via `PlaceAt`/`CircleTurnAt`/`BarPosAt` (LMB place/zone, RMB
  zone width). Music import into `MogCharts/Music`, waveform from `GetData` peaks, BPM grid/snap, B adds a
  check, drag on the timeline, per-type inspector, dirty-check with double-click confirm, Ctrl+S. Structure
  changes in OnGUI go through `Changed` → `GUIUtility.ExitGUI()`; no full-screen IMGUI fill (it would hide
  the uGUI preview). IMGUI is not drawn in batchmode — to see it, build the app from the project copy with a
  driver using `ScreenCapture` (done 2026-10-30, all fine). Office builder version 15, menu builder version 6.
- **Client calls (user's spec 2026-10-23, docs/CALLS.md):** 4–6 calls a night; click the ringing phone →
  client line at the bottom → LMB anywhere → options (mouse or 1–3) → reaction, correct = money 2–10 $
  (`Wallet`, PlayerPrefs `SSDFNC.Money`, counter in the main menu). Answer from the schedule board (new
  every night, generated from `StreamingAssets/Calls/schedule.json`). Turning away hides the dialog (no
  answering) until you face forward again; everything else stays usable, the night keeps going; paused while
  `Night.Frozen`. Forward view has a camera lean (`CameraLook.ForwardPullBack`/`ForwardPitch`) so the
  dialog sits "under the desk". Dialogues are data (`Calls/Dialogues/*.json`, variables `{time}` `{furry}`
  `{furry_gen|dat|acc|ins}` `{activity}` `{room}`, gender `{he|she}`), logic in `Core/Calls`, tested in
  `CallTests`. The client does NOT know the right answer (user's rule): the reaction follows the player's
  choice (`Call.Reactions[i]`; `thanks`, and `thanksNo` for "no" in OrderFurry/VipFree); a wrong answer only
  means no money (green "+N $" on right). Next planned by the user: a dialogue editor app (options listed in CALLS.md). No penalty
  for wrong/missed calls yet. F1 panel has «Вызвать звонок» (`CallDirector.RingNow`). Builder version 10,
  menu builder version 3. Autotests must save/restore
  `SSDFNC.Money` (shared PlayerPrefs).
- **Nova model (2026-10-30, docs/MODELS.md):** `Art/Nova/nova wip.blend` (Blocky Rig + cat head `Cube.006`),
  exported by `Tools/Blender/export_nova.bat` → `Assets/Models/Nova/Nova.fbx`; `NovaModelImport` (no anims,
  `importVisibility=false` or the head comes in disabled). Builder `NovaAvatar` replaces Nova's capsule: height
  1.8 m, yaw 180° (imported model faces +Z, away from the player), URP materials (body texture missing —
  not packed in the .blend; body uses the fur colour until the user packs/sends `Furrypetfurry2_diff.png`).
  The user asked to keep the attack phases but remove my placeholder capsule animations — done; Nova has no
  animations yet. `NightDirector.AvatarSpacing` 1.2 m (Nova is wider). Phase label has a dark backing
  (`MogBattle.phaseBack`) because the schedule board sits behind it. Builder version 13.
- **Generator model (2026-10-27, docs/MODELS.md):** sources `Art/Generator/*.blend` (LFS), exported by
  `Tools/Blender/export_generator.bat` (Blender 5.2 from Steam, background mode, bakes constraint-driven
  bones, renames materials, packs textures for URP) into `Assets/Models/Generator`. `GeneratorModelImport`
  (AssetPostprocessor): Bake Axis Conversion, Legacy anims, strips foreign curves per clip (Unity merges the
  `generator_rig` node into the FBX root → its path is ""). Builder wraps the FBX in a `GeneratorModel`
  holder (the clips animate the FBX root), reuses the importer-added `Animation`, creates URP materials,
  falls back to primitives without the FBX. Starting takes `Balance.GeneratorStartTime` (frame 64 = 2.6 s);
  the sim uses it too — it made Normal noticeably harder. Builder version 11.
- Generator roles (user's decision 2026-10-22): Dali stalls or drains (`DaliStallChance`), Doli breaks at
  `BreakPerSecond` until scared by the sound, max `BreakTime` (= fully broken). Sound cues, not spatial on
  purpose: Doli's growl at the door AND the generator (same sound, the player must check), drain glug only
  while draining. The user prefers sound cues over on-screen warnings for these.
- Dali/Doli never both go to the generator (user's rule): Doli takes the door if `Dali.HeadingToGenerator`;
  Dali waits in Vent (8) while `Doli.HeadingToGenerator` (rivals linked in `Night`).
- Night settings live in the main menu tab «Настройка ночи» (`NightSettingsTab` → static `NightSettings`,
  PlayerPrefs key `SSDFNC.NightLevels`); `NightDirector` starts the night with `NightSettings.Levels`.
  `MainMenuBuilder` has its own `Version` (2) + rebuild prompt. PlayerPrefs are shared with test copies
  of the project (same company/product name): delete `SSDFNC.NightLevels` after menu autotests.
- F1 `DifficultyPanel` (debug builds): per-furry `SetBaseLevel`, presets, "Только Нова",
  `Night.ForceNovaBattle()`.
- **Fast balance iteration without Unity:** Core has no Unity deps, so a .NET 9 console project
  (`dotnet` is installed) can `<Compile Include="…\Assets\Scripts\Core\**\*.cs" />` with LangVersion 9 and
  call `NightSimulator.Report(nights, duration)` — 2700 nights in ~1 s. A what-if script that copies
  Core, regex-overrides constants and level tables, builds and runs was used on 2026-10-22.
- Tests: Unity Test Runner EditMode, `Assets/Tests/EditMode` (asmdef refs SSDFNC.Core + nunit).
  Batch: `-runTests -testPlatform EditMode -testResults <xml>` without `-quit` (~90 s on a copy).
