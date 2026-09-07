using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RepoLiveControl.Runtime
{
    /// <summary>
    /// Small, local-only previews of the real catalog assets. No prefab is ever
    /// instantiated: native icons are reused, otherwise mesh assets are submitted
    /// directly to a private render target without a camera or scene objects.
    /// All methods must run on Unity's main thread.
    /// </summary>
    internal sealed class RuntimeTargetPreviewService : IDisposable
    {
        internal const int TextureSize = 96;
        internal const int CacheCapacity = 192;
        internal const int QueueCapacity = 64;
        private const int MaximumRenderers = 64;
        private const int MaximumSubmeshes = 16;
        private const int MaximumVertices = 250000;
        private const float RetryDelaySeconds = 15f;

        private readonly Dictionary<string, Entry> cache =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<string> leastRecentlyUsed = new LinkedList<string>();
        private readonly Queue<string> pending = new Queue<string>();
        private readonly HashSet<string> queued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int observedSceneHandle = int.MinValue;
        private bool disposed;

        private sealed class Entry
        {
            internal Texture2D Texture;
            internal float RetryAt;
            internal LinkedListNode<string> Node;
        }

        private sealed class MeshPart
        {
            internal Mesh Mesh;
            internal Matrix4x4 Matrix;
            internal Material[] Materials;
        }

        internal string LastError { get; private set; }

        /// <summary>Cheap lookup/queue operation, safe during IMGUI repaint/layout.</summary>
        internal Texture GetOrQueue(string canonicalSelector)
        {
            if (disposed || !IsPreviewSelector(canonicalSelector))
                return null;

            Entry entry;
            if (cache.TryGetValue(canonicalSelector, out entry))
            {
                leastRecentlyUsed.Remove(entry.Node);
                leastRecentlyUsed.AddLast(entry.Node);
                if (entry.Texture != null || Time.realtimeSinceStartup < entry.RetryAt)
                    return entry.Texture;
            }

            if (pending.Count < QueueCapacity && queued.Add(canonicalSelector))
                pending.Enqueue(canonicalSelector);
            return null;
        }

        /// <summary>Generate at most one requested preview per normal game update.</summary>
        internal void Update()
        {
            if (disposed)
                return;
            int scene = SceneManager.GetActiveScene().handle;
            if (observedSceneHandle != scene)
            {
                Clear();
                observedSceneHandle = scene;
            }
            if (pending.Count == 0)
                return;

            string selector = pending.Dequeue();
            queued.Remove(selector);
            Texture2D texture = null;
            try
            {
                texture = Generate(selector);
                LastError = string.Empty;
            }
            catch (Exception error)
            {
                // Unsupported shaders/meshes do not prevent using the catalog.
                LastError = error.GetType().Name + ": " + error.Message;
            }
            Store(selector, texture);
        }

        internal void Clear()
        {
            foreach (Entry entry in cache.Values)
                DestroyOwned(entry.Texture);
            cache.Clear();
            leastRecentlyUsed.Clear();
            pending.Clear();
            queued.Clear();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            Clear();
            disposed = true;
        }

        private void Store(string selector, Texture2D texture)
        {
            Entry previous;
            if (cache.TryGetValue(selector, out previous))
            {
                DestroyOwned(previous.Texture);
                leastRecentlyUsed.Remove(previous.Node);
                cache.Remove(selector);
            }
            while (cache.Count >= CacheCapacity)
            {
                string oldest = leastRecentlyUsed.First.Value;
                Entry evicted = cache[oldest];
                DestroyOwned(evicted.Texture);
                cache.Remove(oldest);
                leastRecentlyUsed.RemoveFirst();
            }
            cache.Add(selector, new Entry
            {
                Texture = texture,
                RetryAt = Time.realtimeSinceStartup + RetryDelaySeconds,
                Node = leastRecentlyUsed.AddLast(selector)
            });
        }

        private static bool IsPreviewSelector(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            int colon = value.IndexOf(':');
            if (colon < 1 || colon == value.Length - 1 ||
                value.Substring(colon + 1).Equals("all", StringComparison.OrdinalIgnoreCase))
                return false;
            string prefix = value.Substring(0, colon);
            return prefix.Equals("item", StringComparison.OrdinalIgnoreCase) ||
                   prefix.Equals("valuable", StringComparison.OrdinalIgnoreCase) ||
                   prefix.Equals("enemy", StringComparison.OrdinalIgnoreCase);
        }

        private static Texture2D Generate(string selector)
        {
            GameObject prefab;
            Sprite icon;
            if (!RuntimeTargetCatalog.TryGetPreviewSource(selector, out prefab, out icon))
                return null;

            if (icon == null && prefab != null)
            {
                ItemAttributes attributes = prefab.GetComponentInChildren<ItemAttributes>(true);
                if (attributes != null)
                    icon = attributes.icon;
            }
            if (icon != null)
            {
                Texture2D nativePreview = TryRenderSprite(icon);
                if (nativePreview != null)
                    return nativePreview;
            }
            if (prefab == null)
                return null;

            // This is the same cache path used by ItemAttributes.GenerateIcon.
            // Read existing files only; never run that coroutine or SemiIconMaker.
            if (selector.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
            {
                Texture2D cached = null;
                try { cached = TryReadGameIcon(prefab.name); }
                catch { /* A stale/corrupt native cache must not block mesh previews. */ }
                if (cached != null)
                    return cached;
            }
            return RenderPrefabMeshes(prefab);
        }

        private static Texture2D TryRenderSprite(Sprite sprite)
        {
            try { return RenderSprite(sprite); }
            catch { return null; }
        }

        private static Texture2D TryReadGameIcon(string prefabName)
        {
            string name = (prefabName ?? string.Empty).Replace("(Clone)", string.Empty).ToLowerInvariant();
            if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0 || name == "." || name == "..")
                return null;
            string path = Path.Combine(Application.persistentDataPath, "Cache", "Icons", "Items", name + ".png");
            if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024)
                return null;

            Sprite loaded = null;
            Texture texture = null;
            try
            {
                loaded = SemiFunc.LoadSpriteFromFile(path);
                if (loaded == null)
                    return null;
                texture = loaded.texture;
                return TryRenderSprite(loaded);
            }
            finally
            {
                DestroyOwned(loaded);
                DestroyOwned(texture);
            }
        }

        private static Texture2D RenderSprite(Sprite sprite)
        {
            // Sprite vertices/UVs preserve packed/rotated atlas entries, unlike
            // drawing the whole texture or assuming a rectangular atlas region.
            var mesh = new Mesh { name = "RepoCommandConsole.IconPreview", hideFlags = HideFlags.HideAndDontSave };
            Material material = null;
            try
            {
                Vector2[] source = sprite.vertices;
                var vertices = new Vector3[source.Length];
                for (int i = 0; i < source.Length; i++)
                    vertices[i] = new Vector3(source[i].x, source[i].y, 0f);
                ushort[] sourceTriangles = sprite.triangles;
                var triangles = new int[sourceTriangles.Length];
                for (int i = 0; i < sourceTriangles.Length; i++)
                    triangles[i] = sourceTriangles[i];
                mesh.vertices = vertices;
                mesh.uv = sprite.uv;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                if (vertices.Length == 0 || !Finite(mesh.bounds.extents.sqrMagnitude) ||
                    mesh.bounds.extents.sqrMagnitude <= 0.0000001f)
                    return null;
                material = MakePreviewMaterial(null, sprite.texture, true);
                if (material == null)
                    return null;
                return Render(new List<MeshPart>
                {
                    new MeshPart { Mesh = mesh, Matrix = Matrix4x4.identity, Materials = new[] { material } }
                }, mesh.bounds, true, true);
            }
            finally
            {
                DestroyOwned(material);
                DestroyOwned(mesh);
            }
        }

        private static Texture2D RenderPrefabMeshes(GameObject prefab)
        {
            var parts = new List<MeshPart>();
            var ownedMeshes = new List<Mesh>();
            try
            {
                Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
                int remainingVertices = MaximumVertices;
                foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (parts.Count >= MaximumRenderers)
                        break;
                    if (!IsActiveChild(renderer.transform, prefab.transform))
                        continue;
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                        continue;
                    if (filter.sharedMesh.vertexCount > remainingVertices)
                        continue;
                    remainingVertices -= filter.sharedMesh.vertexCount;
                    parts.Add(new MeshPart { Mesh = filter.sharedMesh,
                        Matrix = toRoot * renderer.localToWorldMatrix, Materials = renderer.sharedMaterials });
                }
                foreach (SkinnedMeshRenderer renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (parts.Count >= MaximumRenderers)
                        break;
                    if (renderer.sharedMesh == null || !IsActiveChild(renderer.transform, prefab.transform))
                        continue;
                    if (renderer.sharedMesh.vertexCount > remainingVertices)
                        continue;
                    remainingVertices -= renderer.sharedMesh.vertexCount;
                    // Bake only mesh data into our own mesh; the source component
                    // and its bones are never modified or enabled.
                    Mesh baked = new Mesh { name = "RepoCommandConsole.SkinnedPreview", hideFlags = HideFlags.HideAndDontSave };
                    ownedMeshes.Add(baked);
                    Mesh mesh;
                    try
                    {
                        renderer.BakeMesh(baked);
                        mesh = baked.vertexCount == 0 ? renderer.sharedMesh : baked;
                    }
                    catch
                    {
                        mesh = renderer.sharedMesh;
                    }
                    parts.Add(new MeshPart { Mesh = mesh,
                        Matrix = toRoot * renderer.localToWorldMatrix, Materials = renderer.sharedMaterials });
                }
                if (parts.Count == 0)
                    return null;
                Bounds bounds;
                if (!TryGetBounds(parts, out bounds))
                    return null;
                return Render(parts, bounds, false, false);
            }
            finally
            {
                foreach (Mesh mesh in ownedMeshes)
                    DestroyOwned(mesh);
            }
        }

        private static bool IsActiveChild(Transform current, Transform root)
        {
            while (current != null && current != root)
            {
                if (!current.gameObject.activeSelf)
                    return false;
                current = current.parent;
            }
            return true;
        }

        private static bool TryGetBounds(List<MeshPart> parts, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (MeshPart part in parts)
            {
                if (part.Mesh.vertexCount == 0)
                    continue;
                Bounds local = part.Mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f,
                                    (corner & 2) == 0 ? -1f : 1f,
                                    (corner & 4) == 0 ? -1f : 1f));
                    point = part.Matrix.MultiplyPoint3x4(point);
                    if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z))
                        return false;
                    if (any)
                        bounds.Encapsulate(point);
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        any = true;
                    }
                }
            }
            return any && Finite(bounds.extents.sqrMagnitude) &&
                   bounds.extents.sqrMagnitude > 0.0000001f;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Texture2D Render(List<MeshPart> parts, Bounds bounds, bool frontOn, bool materialsAlreadyOwned)
        {
            RenderTexture target = RenderTexture.GetTemporary(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            var buffer = new CommandBuffer { name = "RepoCommandConsole.Thumbnail" };
            var materials = new List<Material>();
            Texture2D result = null;
            try
            {
                float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
                Vector3 offset = frontOn ? new Vector3(0f, 0f, -1f) : new Vector3(1f, 0.65f, -1.5f).normalized;
                Vector3 position = bounds.center + offset * radius * 3f;
                Quaternion rotation = Quaternion.LookRotation(bounds.center - position, Vector3.up);
                Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) *
                    Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
                float size = frontOn ? Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.08f : radius * 1.08f;
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(
                    Matrix4x4.Ortho(-size, size, -size, size, radius * 0.1f, radius * 6f), true);
                buffer.SetRenderTarget(target);
                buffer.ClearRenderTarget(true, true, Color.clear);
                buffer.SetViewProjectionMatrices(view, projection);

                int draws = 0;
                foreach (MeshPart part in parts)
                {
                    int submeshes = Math.Min(part.Mesh.subMeshCount, MaximumSubmeshes);
                    for (int submesh = 0; submesh < submeshes; submesh++)
                    {
                        Material source = part.Materials != null && submesh < part.Materials.Length ? part.Materials[submesh] : null;
                        Material material = materialsAlreadyOwned ? source : MakePreviewMaterial(source, null);
                        if (material == null)
                            continue;
                        if (!materialsAlreadyOwned)
                            materials.Add(material);
                        buffer.DrawMesh(part.Mesh, part.Matrix, material, submesh, 0);
                        draws++;
                    }
                }
                if (draws == 0)
                    return null;
                Graphics.ExecuteCommandBuffer(buffer);
                RenderTexture.active = target;
                result = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
                {
                    name = "RepoCommandConsole.Thumbnail",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                result.ReadPixels(new Rect(0, 0, TextureSize, TextureSize), 0, 0, false);
                bool visible = false;
                foreach (Color32 pixel in result.GetPixels32())
                {
                    if (pixel.a > 8)
                    {
                        visible = true;
                        break;
                    }
                }
                if (!visible)
                    return null;
                result.Apply(false, true);
                Texture2D completed = result;
                result = null;
                return completed;
            }
            finally
            {
                // Neither the active target nor temporary Unity assets escape.
                RenderTexture.active = previous;
                buffer.Release();
                RenderTexture.ReleaseTemporary(target);
                foreach (Material material in materials)
                    DestroyOwned(material);
                DestroyOwned(result);
            }
        }

        private static Material MakePreviewMaterial(Material source, Texture explicitTexture, bool sprite = false)
        {
            Texture texture = explicitTexture;
            string sourceTextureProperty = null;
            if (texture == null && source != null)
            {
                if (source.HasProperty("_BaseMap"))
                {
                    texture = source.GetTexture("_BaseMap");
                    if (texture != null)
                        sourceTextureProperty = "_BaseMap";
                }
                if (texture == null && source.HasProperty("_MainTex"))
                {
                    texture = source.GetTexture("_MainTex");
                    if (texture != null)
                        sourceTextureProperty = "_MainTex";
                }
            }
            // Verified in the shipped sharedassets0.assets. Unlike the sprite/UI
            // fallback, this unlit shader supports tint, textures AND depth
            // writes, so a back mesh cannot paint over a nearer opaque surface.
            Shader shader = UsableShader(sprite ? "Sprites/Default" : "Particles/Standard Unlit");
            if (shader == null)
                shader = UsableShader(texture == null ? "Unlit/Color" : "Unlit/Texture");
            if (shader == null)
                shader = UsableShader("Sprites/Default");
            if (shader == null)
                shader = UsableShader("UI/Default");
            if (shader == null)
                return null;
            var material = new Material(shader)
            {
                name = "RepoCommandConsole.PreviewMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture != null ? texture : Texture2D.whiteTexture);
                if (sourceTextureProperty != null)
                {
                    material.SetTextureScale("_MainTex", source.GetTextureScale(sourceTextureProperty));
                    material.SetTextureOffset("_MainTex", source.GetTextureOffset(sourceTextureProperty));
                }
            }
            if (!sprite && shader.name == "Particles/Standard Unlit")
            {
                material.SetFloat("_Mode", 0f);
                material.SetFloat("_ColorMode", 0f);
                material.SetFloat("_ZWrite", 1f);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_Cull", (float)CullMode.Back);
                material.SetFloat("_LightingEnabled", 0f);
                material.SetFloat("_SoftParticlesEnabled", 0f);
                material.SetFloat("_DistortionEnabled", 0f);
                material.SetFloat("_FlipbookMode", 0f);
                material.DisableKeyword("_SOFTPARTICLES_ON");
                material.DisableKeyword("_DISTORTION_ON");
                material.DisableKeyword("_FADING_ON");
                material.DisableKeyword("_REQUIRE_UV2");
            }
            Color color = Color.white;
            if (source != null)
            {
                if (source.HasProperty("_BaseColor"))
                    color = source.GetColor("_BaseColor");
                else if (source.HasProperty("_Color"))
                    color = source.GetColor("_Color");
            }
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            return material;
        }

        private static Shader UsableShader(string name)
        {
            Shader shader = Shader.Find(name);
            return shader != null && shader.isSupported ? shader : null;
        }

        private static void DestroyOwned(Object value)
        {
            if (value != null)
                Object.Destroy(value);
        }
    }
}
