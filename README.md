# Special Mixing Terminal

A Schedule I mod by **Dekode1859**. Buy a programmable mixing terminal from Oscar's Store for **$6,500** and use the game's native handler routes, chemist assignments and mixing-station settings to run a recipe.

## Install

Download through [Schedule I Mod Manager](https://github.com/schedule-one-mods/schedule-one-manager), or extract `Mods/SpecialMixingTerminal.dll` into the game's `Mods` directory with MelonLoader installed. Close the game before changing installed mods.

Supported integration target: **Schedule I 0.4.7f5, IL2CPP, Windows x64, MelonLoader 0.7.3**. The Schedule One API package is not required. Mono and multiplayer are unverified.

The release includes one production mod DLL. No editor, item-grant tools, game binaries, game assets, or saves are distributed.

## Set up a recipe

1. Place the terminal on an owned property. With clipboard **9** equipped, press **E** to manage its physical assignments.
2. Assign one product supply shelf, one output destination, **four distinct mixing stations**, and up to four ingredient supply shelves. Output accepts a storage shelf, **Packaging Station Mk I / Mk II**, or **Brick Press**. Native selection controls accept only the appropriate equipment types. Equipment names follow their in-game names.
3. Put away the clipboard and press **E** at the computer. Add one to four ingredients in mixing order. Use the arrows to reorder, the cross to remove, and inline dropdowns to choose shelves and mixers. Each ingredient needs a different mixer. Thresholds are fixed at **20**.
4. Select one handler and one chemist on **Staff & routing**. Both must belong to this property, have payment lockers, and have their previous jobs cleared using the native clipboard. Unavailable employees remain visible with a reason.
5. Check the flow diagram, then press **Start**. A four-ingredient recipe creates five handler routes and assigns all four mixers to the chemist. The native AI performs loading, mixing and output transfers.

The screen uses the game's native computer camera transition and controls. **Escape** closes an open selector before closing the computer. Closing the computer saves the recipe and applies valid mixer filters and destinations; it does not start employees. Normal computer interaction is disabled while the clipboard is equipped.

## Read the flow diagram

Ingredient supply shelves appear above their mixers. Product supply feeds the first mixer from below; the final output destination is below the rightmost mixer. The mixer chain runs left to right. Blue worker badges identify handlers; yellow badges inside mixer headers identify chemists. The selected handler shows its actual used/native route capacity and the pending recipe's route count. Packaging supplies and downstream packaging jobs still use the game's normal setup.

Before Start, green connections indicate assignments that pass readiness checks. After Start, the diagram reads the actual native routes, whitelists, thresholds, destinations and employee station lists. Missing or conflicting links show red. Readiness does not create employee jobs early.

The employee picker distinguishes selected staff in blue, available staff in green, missing payment lockers in yellow, and conflicting assignments in red.

## Stop, move and chain terminals

**Stop** releases only the terminal's employee routes and station assignments. Payment lockers and passive recipe wiring are retained. If jobs were changed externally, the terminal refuses to erase mismatched assignments and explains the conflict. Recipe and clipboard connections are locked while terminal jobs are active.

With jobs stopped and the computer closed, use empty hands and the native hold-right-click action to pick up the terminal. Chain terminals by assigning the first terminal's output shelf as the next terminal's product supply. Each terminal requires its own four mixers and staff pair.

Recipes are saved per terminal under the active save's `ModData/SpecialMixingTerminal` folder. Back up saves before adding or removing mods. Removing the DLL does not restore mixer settings or employee jobs; stop and dismantle a setup first if you want to remove it.

## Validation and limitations

Native integration checks on isolated save copies verified threshold 20, ingredient whitelists, chained destinations, five handler routes, four chemist station assignments, Stop releasing jobs while preserving payment lockers, and saved mixer settings after restart. The UI was inspected during development. A complete production cycle and clean installation through Mod Manager remain pending user acceptance. These checks do not guarantee compatibility with other game versions or mods.

Version 0.5.3 removes scene searches from the per-frame update and laundering refresh hooks, repeated terminal re-registration, and employee scene searches. Terminal/interface caches are maintained on placement, pickup and scene unload. Clipboard state updates only on changes. Equipment/employee lookup caches refresh on demand, and closed computer screens do no diagnostic work. Clipboard refresh callbacks are released with their panels. See [PERFORMANCE.md](PERFORMANCE.md) for the source audit and remaining measurement work. No measured FPS improvement is claimed yet. New packaging-output delivery and the inventory icon still need in-game acceptance.

The original 256px terminal/flask icon is embedded in the production DLL and reused by the game's item definition. The release thumbnail is the same artwork; no external asset bundle is required.

Four physically assigned mixers are required even for recipes with fewer ingredients. Unused mixers remain assigned to the chemist, with an empty ingredient whitelist. Repeated use of a single mixer is not supported. Native employee schedules, payment, inventory and pathfinding still apply. Start currently requires empty mixers.

## Build and release

Install a .NET SDK supporting `net6.0`, then run:

```powershell
.\build.ps1 -GameDir 'C:\Path\To\Schedule I'
.\package.ps1 -GameDir 'C:\Path\To\Schedule I'
```

Build references come from the local game's MelonLoader IL2CPP assemblies and are never shipped. See [RELEASING.md](RELEASING.md) for release and registry steps, [CHANGELOG](packaging/CHANGELOG.md) for changes, and [LICENSE](LICENSE) for the MIT license.
