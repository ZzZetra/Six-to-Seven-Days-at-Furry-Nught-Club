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
- Own speech model: `Tools/SpeechTrain` (Python 3.13 venv — torch cpu, piper-tts, onnx; NO Tk there, so the GUI
  always runs from `Tools/SpeechTest/.venv` (3.14), which only needs numpy + onnxruntime for it). Pipeline:
  `download.py` (Piper voices → `voices/`, openWakeWord mel/embedding onnx → `features/`), `generate.py`
  (Piper synth + augment → `data/{train,val}.npz`, ~65 min on 10 workers; `--add late,cut` appends kinds),
  `train.py` (MLP 16×96 → 4 classes → `models/ssdfnc_words.onnx`), `evaluate.py` (SAPI Zira/Irina test WAVs in
  `data/test`, compares with Vosk), `detect.py` (`WordDetector`: rolling 2 s window → `embed.Features` exactly as
  in training; fire at ≥0.8, re-arm when <0.3 and ≥0.35 s, other word ≥0.35 s). Generation runs detached via
  PowerShell `Start-Process` (Bash background has a 10 min cap); kill orphaned pool workers if a run is killed.
- Sounds (user 2026-11-05: «подключи их в игру и сделай в настройках смену этих звуков, чтобы прям во время игры
  менять»; «любой из звуков можно выбрать в любое место»): files in `Assets/StreamingAssets/Sounds` (Cyrillic names,
  LFS), loaded at runtime by `GameSounds` (slot table `Slots`, PlayerPrefs `SSDFNC.Sound.<slot>`, "" none / "*"
  builtin), `SoundBinding` for sources, pause → «Звуки» (`SoundSettingsPanel`, built at runtime). Answers: furry
  sound when it comes close, only Doli (bells) for now; «звук кнопки» = the red sound button; door hit = furry
  reaches the closed door / door closed on it — but not earlier than `NightDirector.DoorHitDelay` (2 s) after closing
  (user: «удар только после того как закрыл дверь, даже через пару секунд»); ambient starts after `AmbientDelay` 5 s;
  volumes `GameSounds.GeneratorVolume` 0.2 (start/loop/stop equal) and `AmbientVolume` 0.25 («эмбиэнт чуть громче гена»); the «+» files are two lock variants (user: «именно два вида замка») →
  slot `NightEndLock` played TOGETHER with `NightEnd` (user: «хотел их соединить, чтоб одновременно звучали»;
  `GameSounds.PlayPair`, `NightEndScreen.LockDelay` 0; leaving the end screen fades the jingle, a not-yet-started
  lock starts at once — `StopUi` in `SceneLoader.Load`); бааам / воу-воу / уууувоп / бип only in the lists. `Tools/Audio/`:
  `import_sounds.py` (Downloads → StreamingAssets with names, trims the bells), `generator_sounds.py` (start / loop /
  stop; run both with the SpeechTrain venv: soundfile + scipy; the mp3s have a broken last frame — read blockwise
  until `LibsndfileError`). Play test 2026-11-05: all 22 files load, generator stall/start, door, pause panel OK.
  Autotests must save/restore `SSDFNC.Sound.*`. Office builder 27.
  Night start (user: «звук начала ночи должен заканчиваться ровно в конец загрузки», player «заезжает в своё сиденье…
  проснулся и дёрнулся», «экран плющит, угол обзора увеличенный и возвращается»): `SceneLoader` plays the loading
  sound itself and times the fade-out to end with it (measured 0–20 ms); `CameraLook` intro consts `Intro…` (start
  5 cm forward, 2 cm up, head 4° up, FOV +22, 0.9 s EaseOutBack). Starting behind shows the chair back; head down
  shows the empty seat — checked with renders on the copy.
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
- **Only the office camera has an AudioListener** (Boot, MainMenu, MogEditor scenes have none). Without one nothing is
  audible (log spam «There are no audio listeners»). `GameSounds.EnsureListener` adds one to `Camera.main` after every
  scene load at runtime — keep it, or add listeners in the builders.
- **Never hide buttons with `CanvasGroup.interactable = false`** in menus: Selectables jump to `disabledColor`, Unity's default
  is light grey at 50% alpha → white flash (user: «осталось белое мигание»). Hide with `blocksRaycasts` + alpha; builders'
  `NewButton` set a dark `disabledColor`.
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
  `SampleScene` (via `SceneLoader`), `quitButton` → `Application.Quit()` (stops Play mode in the editor).
- **Flow (user, 2026-11-04):** loading screens, Esc pause, «Ночь окончена». `SceneLoader.Load(scene, caption)` —
  runtime-built persistent overlay (DontDestroyOnLoad, sorting 1000), async load, min 1.4 s (Boot 2 s), random
  tip; used for boot (new first scene `Boot` with `Boot.cs`, built by `MainMenuBuilder`, Build Settings
  [Boot, MainMenu, SampleScene]; `PlayFromMenu` starts at Boot), night start, restart, menu, death restart.
  `GameFlow` (Paused / NightOver / InputBlocked): timeScale 0 + AudioListener.pause; input readers guard on
  `InputBlocked`. `PauseMenu` + `NightEndScreen` on the office overlay `PauseHUD` (builder `BuildPauseHUD`);
  `ShiftTimer.nightEnd.Show()` at 0 (and F9). Stats: `NightDirector.Feeds`, `Nova.Wins`, calls, `Wallet.Money`.
  Office builder 19 (death panel moved to `PauseHUD` with buttons; 20 = club map), menu builder 7. Drivers: wait for `!SceneLoader.IsLoading` before calling StartNight.
- `ShiftTimer` (on `Shift` in the office): `const Duration` = `Balance.ShiftDuration` (600 s = 10 min,
  as designed; the user switched from 90 s on 2026-10-23), `TimeLeft` = `Duration − elapsed`, `clock` shows `m:ss` — since 2026-11-06 it is a Text in the bottom-right corner of the monitor screen
  (`CameraMonitor.clock`, hidden while the monitor is off / rebooting; user: no «Ночь N», nothing top-right), at 0
  shows `NightEndScreen` («Ночь окончена»). The generator's fuel rate derives from `ShiftTimer.Duration` (150 per shift).
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
  implements IOffice, avatars, death → red screen with «Начать заново» / «В меню» after 1 s (user, 2026-11-04;
  no auto-restart), `MogBattle`. `CameraMonitor` has
  8 cams (`Rooms.Cameras`) and the Dir feed hold button.
- **Nova's attack (mog battle), user's spec 2026-10-23:** phases Intro (Nova turns the player to the
  dance floor: `CameraLook.InputLocked` + `FaceForward()`, capsule anim) → 5 circle → 5 bar → 5 sides →
  Outro (leaves / death). No phase text and no «Мог: ■■■□» lives line on screen (user 2026-11-06: removed for good —
  `phaseLabel`/`phaseBack`/`score` are gone); only the check title and hint. Others idle (`Night.Frozen`).
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
  speed changes via `speedKeys`, bounces at the ends; `side: "right"` (user, 2026-11-02) starts the marker at the
  right edge — `BarPos` mirrored, `pos` still from the left). Button/arrow window ±`Balance.MogHitWindow`. Phases
  come from the chart: a run of same-type checks = one phase. One check on screen; the current one is missed
  when the next is already hittable (`MogCheck.ReadyTime`), not when the next merely starts — dense charts
  showed the next ring half-shrunk. Old `sides`/speedless bars still load. Window built by
  `MogBattleBuilder` (office `FurryHUD`, menu `MogTestHUD`, editor `MogPreviewHUD`); rings are `MogShapes`.
  User 2026-11-06: no dark frame behind the checks («прямо в воздухе») — the `MogBattlePanel` Image is transparent and
  disabled, texts get a black `Outline`; the whole window sits lower (`PanelPos`, y −250) so Nova stays visible above it.
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
  `Night.Frozen`. Player is SEATED in all directions (user 2026-11-05 «ниже, чтобы было видно рычаг, ощущение что сидит
  на стуле, во всех сторонах»): builder puts the camera at `CameraLook.SeatedEyeHeight` 0.85 on the map (1.6 without;
  the author's own `Camera.002` in the .blend is at 0.77), `FieldOfView` 70 (set in CameraLook.Start), forward lean
  `ForwardPullBack` 0.45 + `ForwardPitch` 3 (no more ForwardDrop); the generator spot stays standing (1.6). Builder's
  `Eye` is set per build; FurryAvatar labels face Camera.main. Lever moved (user: «рычаг убери в правую сторону под
  право» → answered «под стол справа, под правую руку»): `DoorLeverMount` (static, with `DoorLeverBase`) + `DoorLever`
  pivot, tip at `MapLeverTip` (0.49, –, 0.05; the right slab edge is at x 0.50, knee space to 0.87), yaw 90 so the handle
  points at the chair; right view pitched down `CameraLook.RightPitch` 15; door panel y `MapDoorPanelY` 1.45. Builder 25.
  Generator posed like the author's (user: «поверни как на карте стоит, чтоб так же прям»): export writes `generator`
  {center, front = glass-material centroid − bbox center (the glass is the dial: same model EN2500, verified in the rig
  file), size} into map_materials.json; `BuildGeneratorModel` rotates the dial to `Layout.generatorFront` and scales by the
  long side (≈1.0 instead of `GeneratorModelScale` 1.4; no `GeneratorModelYaw`). Starter handle ends up on the side facing
  the back wall — still clickable from the spot (raycast checked). Builder 26. Forward view has a camera lean so the
  dialog sits "under the desk". Dialogues are data (`Calls/Dialogues/*.json`, variables `{time}` `{furry}`
  `{furry_gen|dat|acc|ins}` `{activity}` `{room}`, gender `{he|she}`), logic in `Core/Calls`, tested in
  `CallTests`. The client does NOT know the right answer (user's rule): the reaction follows the player's
  choice (`Call.Reactions[i]`; `thanks`, and `thanksNo` for "no" in OrderFurry/VipFree); a wrong answer only
  means no money (green "+N $" on right). Next planned by the user: a dialogue editor app (options listed in CALLS.md). No penalty
  for wrong/missed calls yet. F1 panel has «Вызвать звонок» (`CallDirector.RingNow`). Builder version 10,
  menu builder version 3. Autotests must save/restore
  `SSDFNC.Money` (shared PlayerPrefs).
- **Passage G = the vent (user 2026-11-06):** no camera, but visible physically: Dali there → `daliHand` (user corrected:
  the hand is DALI's, not Doli's; Doli in G is not shown). Doli is only active from night 4 (by design). **Dali drains until the sound button** (`DaliAI.HearSound`: PassageG or
  Draining → home; `DrainPerSecond` 3, no DrainTime); stall still can't be stopped. The sim bot presses sound for a
  draining Dali too.
- **Stray / Doli / Dali models (user, 2026-11-06, docs/MODELS.md):** `Art/Furries/{Strey,Dolie,Dalie}.blend`, each
  holds 2–3 variants; `Tools/Blender/export_furries.bat` picks the rig per file («Blocky Rig (calico full)»,
  «Blocky Rig (Dolie alt)», «Dalie alt» — the non-alt Doli/Dali rigs lie on the floor) and `export_furry.py` strips
  the variant suffix from mesh names (Head, Hair, Piercing, Right Arm, Fingers.R…) and renames materials
  `<Name>Body/Head/Hair/Tail/Piercing`. Textures are NOT packed in the .blend — the user sent them separately
  (2026-11-06) → `Art/Furries/textures/<Stray|Doli|Dali>/` (Doli and Dali share file names, hence folders; Stray =
  «calico body/head color»). Never invent textures; `FurryNoTexture` is the fallback. Pitfall: `os.path.basename`
  of Blender's «//file.png» is '' on Windows (UNC) — split on «/». Death flow: screamer `ScreamerTime` → fade to
  black `DeathFadeTime` → reason «WHO\nhow» + buttons (GameFlow.EndNight then); Dir → black «СКОНЧАЛСЯ».
  `FurryModelImport` (was NovaModelImport) covers all four folders. No name labels over furries (user). Placement
  (NightDirector.PlaceAvatars): Doli at Door — hidden; Stray at Door — `strayPeek` (behind the shutter jamb nearest
  the eye line, leaning `StrayPeekLean` into the opening; jamb from `map.shutter` — Find() fails, the door builder
  re-parents it); Dali in PassageG — `daliHand` (Dali copy with only Right Arm + Fingers.R, out of the airduct end
  `MapHandVent`). Death = `DeathScreamer`: head copies 60 m under the map, own camera → RenderTexture → RawImage
  over a 0.55-black panel, unscaled time; Dir → black + «СКОНЧАЛСЯ». Sound slots `DanceFloorMusic` (plays while
  the floor is held, not when Nova forces it) and `Screamer`. Office builder 38.
- **Nova model (2026-10-30, docs/MODELS.md):** `Art/Nova/nova wip.blend` (Blocky Rig + cat head `Cube.006`),
  exported by `Tools/Blender/export_nova.bat` → `Assets/Models/Nova/Nova.fbx`; `NovaModelImport` (no anims,
  `importVisibility=false` or the head comes in disabled). Builder `NovaAvatar` replaces Nova's capsule: height
  1.8 m, URP materials. (2026-11-06: now `ModelAvatar`/`FurryModel` for all four, model faces +Z and
  `FurryAvatar.Show(position)` turns the root to Camera.main every frame — no yaw constant.) Body texture (not packed
  in the .blend) was sent via Telegram 2026-10-30 as a JPEG: `Art/Nova/Furrypetfurry2_diff.jpg` → export_nova.py
  also looks next to the .blend (.png first) → `Assets/Models/Nova/Textures/Nova_Body.png` → NovaBody _BaseMap
  (UV0 = «Faceless», the render UV). Office builder version 16.
  The user asked to keep the attack phases but remove my placeholder capsule animations — done; Nova has no
  animations yet. `NightDirector.AvatarSpacing` 1.2 m (Nova is wider). Phase label has a dark backing
  (`MogBattle.phaseBack`) because the schedule board sits behind it. Builder version 13.
- **Club map (user, 2026-11-04, docs/MODELS.md):** `Art/Map/map 67dfn wip.blend` (Telegram; 296 MB, LFS) →
  `Tools/Blender/export_map.bat/.py` → `Assets/Models/Map/{Map.fbx, Textures, map_materials.json}`. Export bakes
  every render-visible mesh (incl. viewport-hidden ceiling/outside), skips camera, Building Plan, wdg_*, the map's
  generator; opens the gen door −90° around its hinge; splits the disco floor into `DanceTile_000…119`; shutter
  curtain → `ShutterCurtain` (double-sided); ceiling faces down / floors up per face (the ceiling has mixed
  normals); n-gons triangulated; Keys decimated; markers `Marker_Seat`, `Marker_Generator`; materials renamed
  `Map_*`, described in the json (URP Lit built by `MapBuilder` every build). **Unity imports Blender (x,y,z) as
  (x,z,y)** → `MapBuilder` rotates the map 180° (office looks at Blender −Y) and shifts the seat to the origin;
  `Layout.FromBlender` = `model.TransformPoint(x, z, y)`. Renderers cast TwoSided shadows (else the sun passes the
  down-facing ceiling). 5 point lamps. `TestPlatformBuilder` uses the layout (generator at the marker, spot 1.7 m;
  Door.slab = shutter, open = +2.5 m; dance tiles; desk rim 0.64; board flat on the front wall; furry spots in
  Blender coords) — consts `Map…` at the top; no FBX → the old primitive polygon (`BuildPrimitiveOffice`).
  Textures: only 15 are packed; the rest link the author's Downloads (29 Poly Haven assets). The user said
  «качай, самого низкого качества 1к» → `Tools/Blender/download_map_textures.bat/.py` (Blender, Poly Haven API
  `api.polyhaven.com/files/<asset>`, needs a User-Agent or 403; jpg → png fallback, jpg of worn_metal_rack_metal
  gives 500; skips disp) → `Art/Map/textures` (115 files, 54 MB, LFS rule in .gitattributes). export_map finds them
  by stem without `_Nk` and extension; `MAX_TEXTURE` 1024; composed `_MS`/`_BaseAlpha` deduped by hash. Office builder 20.
  **Camera monitor on the desk monitor (user 2026-11-04 «вставить камеру внутрь монитора на столе… кнопки выбора
  и камеры»):** export writes `monitorScreen` (center/normal/up/width/height, Blender coords; faces of `Material.005`
  on `Monitor`) into map_materials.json; `BuildMonitor` puts one 760×425 canvas 4 mm in front of the glass
  (screen texts + feed + the camera button row at the bottom); no screen → the old floating canvases. Schedule board
  moved to the front wall left of the doorway (`MapBoardX/Y` −1.85 / 1.65). The call dialog covers the monitor's
  lower half during calls (clicks pass through its panel). Office builder 21.
  **Map v3 (2026-11-05, «map 67dfn wip (3).blend», 537 MB):** gen door already open (export now rotates only if the
  leaf is thin along x), `MAX_TRIS` 20000 (2.1 M tris in the file), 52 Poly Haven assets downloaded (203 files), 9
  non-Poly-Haven images missing. The author put real controls on the desk; user chose to use them: pink button =
  dance (`Cylinder.001/.002` → `DanceButton/Cap`), red = sound (`Cylinder/.003` → `SoundButton/Cap`), lever under the
  desk = door (`Lever` → `DoorLeverHandle`; wall lever removed, the status panel stays by the shutter), white phone =
  game phone (`Phone` → `DeskPhone`, `Phone.lift` 0, lamp sphere added) — `RENAME` in export_map.py. `PressControl`
  got `pressDirection`/`pressDepth`, `Door` got serialized `leverOpen/leverClosed`. Button colliders reach up/toward
  the player so the overhanging phone doesn't steal clicks (checked with Physics + EventSystem raycasts). The lever is
  barely visible while open (under the desktop). Schedule board is sized to the wall piece left of the doorway
  (`MapBoardWallFrom/To` 2.36 / 3.63 Blender x) — it was wider and its time column went into the side wall. Builder 23.
- **Map v4 (2026-11-05, «67dfn Final Visible map.blend», 486 MB)** copied over `Art/Map/map 67dfn wip.blend` (name kept for
  export_map.bat): author removed everything not visible from the office (server room, chill/eating rooms, main hall walls,
  `Ceiling` → one flat `Plane` at z 3 handled by the flat-plane rule, floor `1Floor`). All RENAME/marker objects present;
  596 meshes, FBX 25 MB; same 9 missing images. Office builder 29. Compare maps with a Blender dump script
  (objects/bboxes/used images) before exporting. User: «почему там дырка, должна быть изнутри текстура» (airduct) →
  export makes meshes with ≥ `OPEN_SHEET` (8%) boundary edges double-sided (Blender renders backfaces, Unity culls);
  Decimate limit is per side for those (`doubled`). The Telegram zip (airduct Poly Haven asset) was not needed. Builder 30.
- **Boss calls / cameras / N3 (user 2026-11-06):** real boss texts only at the start of nights 1, 2, 4 (`Calls/Boss/night_N.json`;
  no file = no call; tests check this); placeholders and `Triggered/test.json` removed. Cameras are 1–7: `Room.Camera1`
  (new enum value at the end — no room, «НЕТ СИГНАЛА», `MonitorStatic.SetNoSignal`), PassageG and Vent are no longer cameras
  (AI still uses them; the sim bot still "sees" PassageG, so balance numbers are a bit optimistic). Night 3 Stray/Dali 7/7,
  night 4 back to 5/5 + Doli 4 (`HarderEachNight` allows that one drop). Office builder 35.
- **Monitor static (user 2026-11-06, refs: VHS scanlines + broken-LCD lines):** `MonitorStatic` on the `Static` child of the
  monitor canvas (between `Feed` and the texts): scanlines (1×4 tex, uvRect repeat), snow (256² noise, random uvRect each
  frame), roll bar, glitch lines + feed jitter, vignette; `CameraMonitor.Select` → `Burst()`, `Redraw` → `SetOn`. Batchmode
  frames are slow — glitches shorter than a frame used to vanish (`glitchStarted`). Office builder 34.
- **Camera feeds (user 2026-11-06, files labelled by the user):** `StreamingAssets/Cameras/<label>/<names joined by « и »>.jpg|png|mp4`
  («пусто» = empty room; names may also be joined by «, » — «Стрей, Нова и Доли»; author's renders «Renders 67FDN» →
  jpg 1280×720 via import_cameras.py `RENDERS`, 24 frames on 2026-11-06); `CameraFeeds.Find(room, furries)` = exact set match, else the monitor shows text as before (empty
  rooms, Dir, missing combos). Images via `Texture2D.LoadImage`, mp4 via the monitor's `VideoPlayer` (Url, loop, GameTime) into
  a RenderTexture on the `Feed` RawImage (between the screen background and the texts/buttons; title has a Shadow). Telegram
  «GIFs» arrive as .mkv (Unity can't import) → `Tools/Cameras/convert_video.py` (Blender 5: `image_settings.media_type =
  'VIDEO'` before `FFMPEG`, 50% size → 960×540). Play test on the copy: all 11 cases right. Office builder 33.
- **Night lighting (user 2026-11-06):** darker, dark corners, gen off → flicker then dark, rare 1–2 lamp flickers, generator
  room flickers from 50% fuel (very strong ≤20%), front corridor barely visible (on) / almost invisible (off), neon variant
  from the ceiling corners as a separate option. `OfficeLights` (consts at top) on `MapLamps`; `MapBuilder.Lamps` has roles;
  `TestPlatformBuilder.SetupLighting` disables the template Directional Light (outside builder roots), Flat ambient
  `MapAmbient`, reflections 0.15, vignette 0.38, bloom 0.6 (edits `Settings/SampleSceneProfile.asset`). Neon = strips + 12
  corner lamps found by raycasts on temporary MeshColliders; toggle menu «Свет» / F4 (PlayerPrefs `SSDFNC.Neon`, autotests
  restore it). World-space UI (schedule board, hint labels) is unlit and still glows when dark. Office builder 31, menu 15.
- **Desk fan spins** (user 2026-11-05 «заставь вентилятор крутиться»): map objects `Desk Fan` (empty) → `SM_Fan.002` (body) +
  `Fan Rig` (armature, bone `spin` rotates about its Y) → `Inner Fan` (blades, bone-parented; baked origin is off the hub).
  `TestPlatformBuilder.BuildDeskFan`: axis = thinnest PCA direction of blade vertices (away from the body), centre = vertex
  average (0.05 cm from the bone head; bbox/edge centres are ~1 cm off for 3 blades), pivot `DeskFanSpin` + `DeskFan`
  (`Speed` 1080°/s). Office builder 28.
- **Monitor glitch (user, 2026-11-02):** 10%/min chance (rolled once a second with its own `Random`, see
  `Night.UpdateMonitor`) the monitor reboots for 3 s: sound slot (`NightDirector.monitorRebootClip`, builder
  loads `Assets/Audio/MonitorReboot.*` — the user will add the sound later), screen goes dark then a boot
  screen (`CameraMonitor.DrawReboot`); cameras and feeding are off. Only if Dir's food ≥ 40% and the monitor
  is powered (`IOffice.MonitorPowered`); not during the mog battle. F1 «Сбой монитора». Sim bot can't feed
  while rebooting. Numbers `Balance.MonitorReboot*`. Office builder version 17.
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
- **Nights 1–7 (user's spec 2026-11-05):** `Core/Nights.cs` (`NightConfig`: levels, monitor reboot chance, client
  calls min/max) — N1 Stray/Dali/Nova, N2–3 + Dir, N4+ all five; reboot 18%/min from N3 (`Balance.MonitorRebootChanceHard`);
  calls up to 9; levels tuned with `NightSimulator.NightsReport` (Average bot 86%→16%). Shift 6 min (`ShiftDuration` 360,
  fuel still 150/shift). `NightProgress` (scene, PlayerPrefs `SSDFNC.NightsPassed` — autotests must save/restore it like
  `SSDFNC.Money`): `Current` 1–7 or 0 = «Своя ночь» (NightSettings levels), unlock = previous passed, N7 also Wallet ≥ 50
  (`Balance.Night7Money`, not spent — user's answer). `Night` takes the per-night reboot chance (ctor param). Menu main tab:
  «Продолжить» + 7 night buttons (menu builder 8); `MainMenu.StartNight()` (no arg, used by drivers) = last unlocked.
  `NightEndScreen`: «НОЧЬ N ПРОЙДЕНА», `MarkPassed`, «Следующая ночь» (locked → reason). Clock — only «m:ss» on the monitor.
  No world labels at the desk buttons / door lever / phone (user 2026-11-06: «текст про кнопки, про дэнспол» removed).
  **Boss calls** (user: «звонок от начальства, можно пропустить, туториал, текст пока не пиши») = `PhoneTalk` monologues
  (`Core/Calls/PhoneTalk.cs`, `BossCallPlan`: `when` start 4–15 s / middle 35–65%; default first start, rest middle) from
  `StreamingAssets/Calls/Boss/night_N.json` (placeholders; N2/4/6 have a middle one). **Triggered calls** (user: «пока просто
  добавь вызов звонка по условию») = `CallDirector.Request(trigger)` → `Calls/Triggered/<trigger>.json` or a placeholder,
  rings after `CallTriggerDelay` when idle. `CallDirector` keeps one time-sorted queue (clients + boss + triggered); talk
  state: LMB → next line; missed talk doesn't count as Missed. F1: «Начальство», «По условию» (office builder 22).
- **Main menu (user 2026-11-05):** «Играть» / «Настройки» / «Выход». «Играть» slides LEFT, level with the first row (`PlayAside`; user changed his mind
  from right), unlocked nights drop in one by one to its right as TWO COLUMNS OF THREE (user was angry at 3×2; `Cell`: 1 2 / 3 4 / 5 6,
  `ByColumns` for 1-2-3 / 4-5-6, N7 one row lower in the middle; buttons 200×110 = Play height; passed `ItemPassed` = the Play button colour (user), not passed darker `ItemNew`, selected `ItemSelected`) (answers: only unlocked shown; N7 shown once N6 passed, grey without 50 $; «Своя ночь» after N7 is a SEPARATE button under
  the list (user: «отдельной кнопкой, которая открывает настройки, остальные ночи пропадают») → `Screen.Custom`: nights
  fade out, the button glides to the first-night spot, levels panel under it, «Играть» starts it, Back → list); select, «Играть» again starts. «Настройки» → «Громкость» (Master + groups Music /
  Ambient / Generator / Effects — `GameSounds.VolumeGroup`, PlayerPrefs `SSDFNC.Volume.*`, autotests must restore),
  «Звуки» (`SoundSettingsPanel`), «Скилл-чеки» (= «настройки мог-батла», `MogTestTab`). `Item`s in `MainMenu` only fade (user: «анимации в меню попроще прям» — no sliding/stagger),
  consts at its top. Menu builder 13. «Продолжить» and the tabs are gone; `MainMenu.StartNight()` (no arg) still works.
- Night settings: `NightSettingsTab` → static `NightSettings`,
  PlayerPrefs key `SSDFNC.NightLevels`; `NightDirector` starts the night with `NightSettings.Levels`.
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
