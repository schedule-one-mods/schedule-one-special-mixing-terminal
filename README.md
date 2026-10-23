# Special Mixing Terminal

A Schedule I mod by **Dekode1859**. Buy a programmable mixing terminal from Oscar's Store for **$6,500** and use the game's native handler routes, chemist assignments and mixing-station settings to run a recipe.

## Install

Download through [Schedule I Mod Manager](https://github.com/schedule-one-mods/schedule-one-manager), or extract `Mods/SpecialMixingTerminal.dll` into the game's `Mods` directory with MelonLoader installed. Close the game before changing installed mods.

Supported integration target: **Schedule I 0.4.7f5, IL2CPP, Windows x64, MelonLoader 0.7.3**. The Schedule One API package is not required. Mono and multiplayer are unverified.

The release includes one production mod DLL. No editor, item-grant tools, game binaries, game assets, or saves are distributed.

## Set up a recipe

1. Place the terminal on an owned property. With clipboard **9** equipped, press **E** to manage its physical assignments.
2. Assign one product supply shelf, one output destination, **one to four distinct mixing stations**, and up to four ingredient supply shelves. Assign at least one mixer per ingredient step. Output accepts a storage shelf, **Packaging Station Mk I / Mk II**, or **Brick Press**. Native selection controls accept only the appropriate equipment types. Equipment names follow their in-game names.
3. Put away the clipboard and press **E** at the computer. Add one to four ingredients in mixing order. Use the arrows to reorder, the cross to remove, and inline dropdowns to choose shelves and mixers. Each ingredient needs a different mixer. Thresholds are fixed at **20**.
4. Select one handler and one chemist on **Staff & routing**. Both must belong to this property, have payment lockers, and have their previous jobs cleared using the native clipboard. Unavailable employees remain visible with a reason.
5. Check the flow diagram, then press **Start**. The handler gets one route per ingredient destination plus the product-feed route; the chemist gets the mixers you actually assigned. A one-ingredient setup needs one mixer and normally two routes; four ingredients need four mixers and five routes. The native AI performs loading, mixing and output transfers.

**One ingredient closet is enough.** Assign that closet once with the clipboard; all four ingredients can resolve to it automatically. The handler still gets four ingredient routes, one from that closet to each mixer, plus the product-feed route from the product shelf. Each mixer's whitelist selects the correct ingredient. You may also explicitly choose the same closet for several steps when multiple shelves are assigned.

If output is a **Brick Press**, Start also assigns that press to the same handler using the native employee station list. This uses a station slot, not an extra delivery route. The press must be unassigned or belong to this terminal's own handler. Output to a packaging station remains a delivery destination only; automatic operation is supported only for the brick press.

The screen uses the game's native computer camera transition and controls. **Escape** closes an open selector before closing the computer. Closing the computer saves the recipe and applies valid mixer filters and destinations; it does not start employees. Normal computer interaction is disabled while the clipboard is equipped.

## Automatic acceptance and product names

Before Start, the terminal previews each stage with the game's effect calculator and looks for existing products. Choose a **Base product** on the first terminal, or leave Auto selected with exactly one product type stocked on its product shelf.

If any result is unknown, Start asks once for a final product name. For a twelve-step chain named Megaslime, new intermediate products are named `1 Megaslime` through `11 Megaslime`; the final product is `Megaslime`. Existing products keep their names and still count toward the step number: after four known stages, the next new product is `5 Megaslime`.

Connect terminals through unique handoff shelves to resolve the whole chain and final stage automatically. Start each terminal separately with its own employees. **Naming** also provides first/final step overrides for partial chains, plus an auto-accept toggle. Stop every connected terminal before changing naming settings. Product registration happens only when a terminal-owned mix completes, using native naming and output methods. Manual mixers are unaffected. Unexpected actual effects or a conflicting name stop automatic acceptance and display an error; employee jobs remain assigned until Stop.

After upgrading an already-running setup, Stop and Start its terminals once to capture the acceptance policies. Products already registered in the game remain registered after Stop, deleting a saved recipe or removing this mod. Native multiplayer acceptance is unverified.

## Save and switch recipes

Use **Save as new** on the Recipe page to name a new saved copy. It captures the entire connected chain, base product, ingredients in order, mixer/supply positions, naming settings and an output/effect preview. The **Saved mixes** page provides search, a read-only detail view, **Load into setup**, and **Delete saved copy**. Loading does not start employees. Edit the loaded recipe and save another copy to keep both versions.

Stop all terminals in the chain before loading. The target must have the same number of connected terminals, enough distinct mixers and compatible supply selections. Recipes map to the current clipboard assignment order; one ingredient shelf can supply every step. Physical shelf connections, employees and payment lockers remain assigned to the current setup. A recipe contains no employee IDs, equipment GUIDs or native job leases. Check the routing diagram before Start after loading onto different equipment.

The catalog is local to the active save at `ModData/SpecialMixingTerminal/library.json` and survives native game saves. Deleting a saved copy leaves the active setup and native product discoveries intact. A damaged or unsupported catalog is reported and preserved instead of silently replaced. Saved output/effect previews are snapshots from the time the recipe was saved.

## Read the flow diagram

Ingredient supply shelves appear above their mixers. Product supply feeds the first mixer from below; the final output destination is below the rightmost mixer. The mixer chain runs left to right. Blue worker badges identify handlers; yellow badges inside mixer headers identify chemists. The selected handler shows its actual used/native route capacity and the pending recipe's route count. Packaging supplies and downstream packaging jobs still use the game's normal setup.

Before Start, green connections indicate assignments that pass readiness checks. After Start, the diagram reads the actual native routes, whitelists, thresholds, destinations and employee station lists. Missing or conflicting links show red. Readiness does not create employee jobs early.

The employee picker distinguishes selected staff in blue, available staff in green, missing payment lockers in yellow, and conflicting assignments in red.

## Stop, move and chain terminals

**Stop** releases only the terminal's employee routes and station assignments. Payment lockers and passive recipe wiring are retained. If jobs were changed externally, the terminal refuses to erase mismatched assignments and explains the conflict. Recipe and clipboard connections are locked while terminal jobs are active.

Stop also removes the terminal-owned brick-press assignment. To change output, Stop first, change the clipboard Output field, then Start: storage/packaging output leaves the handler without a press assignment; choosing a new brick press assigns only the new press. Existing running jobs from an older version should be stopped and started once to apply the new automatic press assignment.

With jobs stopped and the computer closed, use empty hands and the native hold-right-click action to pick up the terminal. Chain terminals by assigning the first terminal's output shelf as the next terminal's product supply. Each terminal has its own one-to-four-mixer pool and staff pair. A five-, six-, or seven-ingredient chain can use a four-step terminal followed by a one-, two-, or three-step terminal; no empty stages are required.

Recipes are saved per terminal under the active save's `ModData/SpecialMixingTerminal` folder. Back up saves before adding or removing mods. Removing the DLL does not restore mixer settings or employee jobs; stop and dismantle a setup first if you want to remove it.

## Validation and limitations

Native integration checks on isolated save copies verified threshold 20, ingredient whitelists, chained destinations, five handler routes, four chemist station assignments, Stop releasing jobs while preserving payment lockers, and saved mixer settings after restart. The UI was inspected during development. A complete production cycle and clean installation through Mod Manager remain pending user acceptance. These checks do not guarantee compatibility with other game versions or mods.

Version 0.5.3 removes scene searches from the per-frame update and laundering refresh hooks, repeated terminal re-registration, and employee scene searches. Terminal/interface caches are maintained on placement, pickup and scene unload. Clipboard state updates only on changes. Equipment/employee lookup caches refresh on demand, and closed computer screens do no diagnostic work. Clipboard refresh callbacks are released with their panels. See [PERFORMANCE.md](PERFORMANCE.md) for the source audit and remaining measurement work. No measured FPS improvement is claimed yet. New packaging-output delivery and the inventory icon still need in-game acceptance.

Version 0.5.4's game-independent logistics checks verify shared-closet resolution, five delivery routes, distinct destinations, repeated ingredients, older-plan compatibility and persisted handler-station ownership. These checks do not exercise native brick-press operation; an in-game production cycle remains pending acceptance.

The original 256px terminal/flask icon is embedded in the production DLL and reused by the game's item definition. The release thumbnail is the same artwork; no external asset bundle is required.

One to four physically assigned mixers are supported. Each ingredient needs a different mixer, so the recipe cannot have more steps than its assigned mixer pool. Extra assigned mixers remain assigned to the chemist with an empty ingredient whitelist; they do not add recipe stages. A single mixer supports a single ingredient step and sends its output directly to the chosen destination. Repeated use of a mixer for multiple steps is not supported. Native employee schedules, payment, inventory and pathfinding still apply. Start currently requires empty assigned mixers.

Version 0.5.5 passed 30 game-independent checks, including layouts and destinations for one through four mixers, insufficient/duplicate station rejection, and 4+1/4+2/4+3 terminal chains. These checks do not replace native gameplay acceptance.

Version 0.6.0 passed a Release build with zero warnings/errors and 61 game-independent checks covering naming, twelve-step chain discovery, forks/cycles, legacy plans, catalog isolation/serialization, atomic restore failures and deletion. The new library UI and native automatic naming have not been exercised in a running game. No game launch, save edit or live deployment was performed for this update.

## Build and release

Install a .NET SDK supporting `net6.0`, then run:

```powershell
.\build.ps1 -GameDir 'C:\Path\To\Schedule I'
.\package.ps1 -GameDir 'C:\Path\To\Schedule I'
```

Build references come from the local game's MelonLoader IL2CPP assemblies and are never shipped. See [RELEASING.md](RELEASING.md) for release and registry steps, [CHANGELOG](packaging/CHANGELOG.md) for changes, and [LICENSE](LICENSE) for the MIT license.
