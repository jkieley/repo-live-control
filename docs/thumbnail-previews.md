# Compact catalog thumbnails

`RuntimeTargetPreviewService` supplies a 96 × 96 cached texture for each visible canonical catalog selector. The console can draw it into a 24 × 24 slot inside an existing row; thumbnails do not need taller rows or fewer visible results.

Call `GetOrQueue(CompletionItem.Value)` for visible target rows, `Update()` once per normal main-thread console update, and `Dispose()` with the console. The lookup does no rendering. Each update generates at most one queued preview. A 192-entry LRU cache and 64-request queue bound memory/work; failed entries retry after 15 seconds. A scene change clears the cache and queue. Unsupported entries return null and keep their text and selection behavior.

The source resolver is shared with `RuntimeTargetCatalog`. Preview selection tries a prefab's assigned native item sprite, the game's existing equipment icon cache, then the actual prefab mesh/material assets. The icon cache path was verified in the installed `ItemAttributes.GenerateIcon` IL: `Application.persistentDataPath/Cache/Icons/Items/{lowercase prefab name}.png`. The preview service only reads existing cached files; it does not invoke the game's icon-generation coroutine or `SemiIconMaker`.

Mesh previews use `CommandBuffer.DrawMesh` into a private 96 × 96 render texture. No gameplay prefab, GameObject, camera, Photon object, collider, AI, or lifecycle script is instantiated or enabled. Static mesh assets are read directly; skinned renderers bake mesh data into a temporary owned mesh, with their shared mesh as a fallback. Existing material textures/colors are copied into temporary unlit preview materials. No source material, transform, or component is edited. Sprite vertices and UVs preserve tight/rotated atlas packing. The render target, temporary meshes/materials, and cached textures are released when their work or lifetime ends.

Mesh materials prefer the game's shipped `Particles/Standard Unlit` shader, explicitly configured for opaque depth writes, texture tint, and no particle effects. Texture scale/offset are preserved. Native sprites use the sprite shader so atlas alpha remains intact. Generic unlit and sprite/UI shaders are fallbacks if an installation lacks the preferred shader.

These are compact visual references, not simulated live appearances: complex shader effects, particles, runtime attachments, inactive cosmetic variants, or models requiring initialization may not appear. Unsupported shader/mesh cases leave the row's canonical category/name text usable. The service does not claim every modded prefab is previewable.

## Validation

Build the plugin, then run the metadata checks against the compiled service and installed Unity/game APIs:

```powershell
dotnet build src/RepoLiveControl/RepoLiveControl.csproj
dotnet run --project tests/RepoLiveControl.PreviewSafetyTests/RepoLiveControl.PreviewSafetyTests.csproj
```

The checks reject prefab instantiation, scene-object creation/activation, component enabling, network/spawn calls, source game-field writes, and icon-cache writes. They also verify that IMGUI lookup does not render and that the installed Unity APIs match. They inspect metadata only and do not exercise the renderer.

For live acceptance, open `/spawn `, verify that the original row count fits with thumbnails, scroll the full catalog, and check an existing native equipment icon, a valuable, and a skinned enemy. Confirm no new world/network objects appear from browsing and that the text remains usable if a preview is unavailable. Move between scenes and repeat, checking that temporary assets are released and the console still accepts the selected exact target.
