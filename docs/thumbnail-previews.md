# Compact catalog thumbnails

`RuntimeTargetPreviewService` supplies a 96 × 96 cached texture for each visible canonical catalog selector. The console can draw it into a 24 × 24 slot inside an existing row; thumbnails do not need taller rows or fewer visible results.

Call `GetOrQueue(CompletionItem.Value)` for visible target rows, `Update()` once per normal main-thread console update, and `Dispose()` with the console. The lookup does no rendering. Each update generates at most one queued preview. A 192-entry LRU cache and 64-request queue bound memory/work; failed entries retry after 15 seconds. A scene change clears the cache and queue. Unsupported entries return null and keep their text and selection behavior.

The source resolver is shared with `RuntimeTargetCatalog`. Preview selection tries a prefab's assigned native item sprite, the game's existing equipment icon cache, then the actual prefab mesh/material assets. The icon cache path was verified in the installed `ItemAttributes.GenerateIcon` IL: `Application.persistentDataPath/Cache/Icons/Items/{lowercase prefab name}.png`. The preview service only reads existing cached files; it does not invoke the game's icon-generation coroutine or `SemiIconMaker`.

Mesh previews use `CommandBuffer.DrawMesh` into a private 96 × 96 render texture. No gameplay prefab, GameObject, camera, Photon object, collider, AI, or lifecycle script is instantiated or enabled. Static mesh assets are read directly; skinned renderers bake mesh data into a temporary owned mesh, with their shared mesh as a fallback. Existing material textures/colors are copied into temporary preview materials. No source material, transform, or component is edited. Sprite vertices and UVs preserve tight/rotated atlas packing. The render target, temporary meshes/materials, and cached textures are released when their work or lifetime ends.

Mesh materials use the game's shipped `Standard` forward color pass with opaque depth writes and a minimum brightness supplied by the source albedo through emission. This works with the game's GPU-only mesh assets; the particle shader can silently produce empty renders for those meshes. The source texture/color names used by Standard, Hurtable, and Fresnel materials and texture scale/offset are preserved. Native sprites use the sprite shader so atlas alpha remains intact. Disabled/inactive meshes, known default-material debug primitives, and vehicle thrust-effect geometry are excluded. No global lighting or shader state is changed.

These are compact visual references, not simulated live appearances: complex shader effects, particles, runtime attachments, inactive cosmetic variants, or models requiring initialization may not appear. Unsupported shader/mesh cases leave the row's canonical category/name text usable. The service does not claim every modded prefab is previewable.

## Validation

The standard test command builds the Release plugin and runs the 25 preview isolation/API checks against that exact DLL and the installed Unity/game APIs:

```powershell
.\scripts\Test-All.ps1
```

The checks reject prefab instantiation, scene-object creation/activation, component enabling, network/spawn calls, source game-field writes, and icon-cache writes. They also verify that IMGUI lookup does not render and that the installed Unity APIs match. They inspect metadata only and do not exercise the renderer.

For regression checks, open `/spawn`, verify that the original row count fits with thumbnails, scroll the full catalog, and check an equipment icon, a valuable, and a skinned enemy. Confirm browsing creates no world/network objects and that text remains usable if a preview is unavailable. Move between scenes and repeat, checking that temporary assets are released and the console still accepts the selected exact target.

A read-only live sweep of the vanilla 255-target catalog on September 7, 2026 produced 254 previews: all 59 equipment targets, all 167 valuables, and 28 enemies. Hidden has no visible model mesh (only an inactive helper plane and particles), so its category placeholder remains. Representative exported previews were inspected for upright orientation, readable albedo, model bounds, and visibility. This diagnostic exercised the actual provider source against the running game without spawning or activating world objects; its temporary hook removed itself after the sweep.
