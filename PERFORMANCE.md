# Performance source audit: 0.5.3

The reported symptom was a large FPS drop while Special Mixing Terminal was enabled with Businesses and Deal Locations. This audit changes only Special Mixing Terminal. No frame-time capture or before/after gameplay benchmark was run, so the size of the FPS improvement remains unmeasured.

## Work removed from frequent paths

| Path in 0.5.2 | Change in 0.5.3 |
| --- | --- |
| `Mod.OnUpdate` searches for every TerminalConfigurable each frame | Iterate cached terminals only when clipboard equipped state changes |
| Three laundering refresh hooks search all laundering stations on every invocation | Constant-time lookup by native interface instance ID |
| Every-second station discovery and repeated AttachConfiguration/RegisterWithProperty | One recovery scan per loaded scene; native Initialize handles new placements |
| Every cached screen ticks each frame, even when closed | Only the active computer receives Tick; closing clears that reference |
| Employee lookups, picker and diagram use scene-wide FindObjectsOfType | Read EmployeeManager.AllEmployees into a shared, demand-refreshed lookup |
| Repeated GUID conversion and property-wide equipment traversal for labels/validation | Demand-refreshed per-terminal equipment dictionary |
| Diagram reads every handler route at the property | Include only routes whose destination is one of the assigned mixers |
| Diagnostic signature repeatedly concatenates strings and asks native filters for descriptions | Reuse StringBuilder; compare filter mode and item IDs directly |

No forced garbage collection is introduced. Unity objects and managed reference caches have explicit lifetimes. The original icon texture and sprite are decoded once and shared by all instances.

## Cache and callback lifetimes

- Terminal and interface entries are added on initialization and removed when the terminal is picked up/destroyed. Scene unload clears all terminal/interface caches and employee lookups.
- Employee and equipment caches refresh on demand at most once per second, or immediately when their source list count changes. Destroyed equipment and employees are rejected when looked up. Renamed equipment labels are read from the current native configuration.
- The live diagram still polls native fields twice per second while its page is open. It only rebuilds UI if its signature changes, and does no polling while the computer is closed.
- Destroying a custom clipboard panel removes its native refresh callbacks from the terminal-owned fields and reinstalls the persistent save callbacks. This prevents stale panel callbacks accumulating across repeated openings.
- Disposing a screen removes its exit listener and native state callback. Existing native worker AI and routes are unaffected by closing the UI.

## Remaining acceptance

Compare frame times on the same save, position, camera, worker load, graphics settings and frame cap, with only Special Mixing Terminal toggled. Include closed-UI gameplay, clipboard use, the open flow page, repeated opening/closing, and pickup/replacement. Keep Businesses and Deal Locations unchanged for the first comparison. Also check save reload, route ownership and new packaging/brick-press destinations. These results are pending; the source changes are not a substitute for measurement.
