# Changelog

## 0.6.0

- Add a searchable Saved mixes page with product/effect previews, read-only recipe details, loading and deletion. Save as new always creates an independent recipe.
- Save the whole connected terminal chain using logical mixer/supply positions. Loading retains the current physical equipment and staff, refuses running chains, and validates every segment before changing any recipe.
- Preview native mixing effects before Start. Ask once for an unknown chain's final name; new intermediate products use the global step number and the final product uses the name alone. Known products retain their existing names.
- Resolve numbering across connected terminals, including known stages. Optional first/final step numbers support partial chains. Ambiguous or cyclic shelf connections are rejected.
- Accept results through the game's native naming/output path only for terminal-owned completed batches. Verify actual ingredient, drug type and effects against the Start snapshot; report mismatches or occupied names instead of renaming unrelated products.
- Add a base-product selector and optional auto-accept toggle. Persist acceptance policies and the saved-recipe catalog with the active save; legacy plans load with compatible defaults.
- Use completion callbacks rather than new per-frame scene scans. Include only the production mod in the package.
- Release build and 61 game-independent checks passed. New native naming, recipe-library UI and complete production cycles await gameplay acceptance; the running game was not opened, closed, modified or redeployed during this update.

## 0.5.5

- Remove the exact-four-station requirement. Each terminal accepts one through four distinct mixers and one through four ingredient steps, with one mixer per step.
- One-step recipes send their mixer's output directly to the selected destination. Two- and three-step recipes chain only their actual stages.
- Keep the native handler route and chemist station capacity checks proportional to the assigned setup.
- Hide unassigned mixer slots in the flow diagram instead of displaying them as missing equipment.
- Support five-, six-, and seven-ingredient production chains as 4+1, 4+2 and 4+3 across terminals, without padding the second terminal to four stations.
- Shared supplies and automatic brick-press staffing remain supported. Self-routing/reusing a mixer for several ingredients remains unsupported.
- Build and 30 game-independent logistics checks passed; native gameplay acceptance remains pending.

## 0.5.4

- Verify one ingredient closet can feed four different mixers: four ingredient routes plus a separate product-feed route. Deduplicate only identical source/destination pairs.
- Automatically assign a brick-press output to the terminal's handler on Start through the native employee station list, without adding a delivery route.
- Persist ownership of handler stations; Stop clears the terminal-owned press assignment while preserving payment lockers. Changing output through Stop/edit/Start cannot retain the previous press.
- Reject presses assigned to another handler and station lists changed outside the terminal. Packaging-station output does not assign the handler to operate packaging stations.
- Show the output press's actual handler badge and native assignment status in diagnostics.
- Nine game-independent logistics/save checks passed; native brick-press operation awaits in-game acceptance.

## 0.5.3

- Output can be a storage shelf, Packaging Station Mk I / Mk II, or Brick Press, using the final mixer's native destination.
- Replace per-frame terminal scene searches and laundering refresh-hook scene searches with lifecycle-managed caches.
- Discover existing terminals once per load; register new placements through native initialization instead of repeating registration every second.
- Update terminal interactivity only when clipboard state changes; tick only the active computer screen.
- Resolve employees from the native employee manager, cache equipment lookups, and inspect only routes feeding this terminal's mixers.
- Reuse a flow-signature buffer and remove native filter-description generation from diagnostic polling.
- Release clipboard UI refresh listeners and screen exit/state callbacks with their owners.
- Add an original embedded terminal/flask inventory and shop icon, plus a package thumbnail.
- FPS comparison, new packaging destination delivery and icon rendering await in-game acceptance; no measured performance claim is made.

## 0.5.2

First published release.

- Purchasable, placeable computer terminal at Oscar's Store.
- Native clipboard assignments for product/output shelves, four mixers and four ingredient shelves.
- Ordered recipes with inline supply/mixer selection and direct reorder/remove controls.
- Threshold 20, ingredient whitelist and destination wiring on physical mixers.
- One handler and one chemist per terminal, using native routes and station assignments.
- Readiness checks before Start and live native-link diagnostics afterward.
- Colored staff selection states and compact portrait badges; chemists inside mixer headers.
- Stop clears owned jobs and preserves payment lockers.
- Native pickup eligibility, clipboard interaction gating and corrected empty-shelf graphics.
- Per-terminal saved configuration. Production package excludes all local development tools.
