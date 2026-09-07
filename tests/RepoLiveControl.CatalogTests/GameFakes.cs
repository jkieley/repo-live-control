namespace UnityEngine
{
    public class Object { public string name = ""; }
    public class Sprite : Object { }
    public class GameObject : Object
    {
        private readonly Dictionary<Type, object> components = new();
        public GameObject With<T>() where T : new() { components[typeof(T)] = new T(); return this; }
        public T GetComponent<T>() where T : class => components.TryGetValue(typeof(T), out var value) ? (T)value : null;
        public T GetComponentInChildren<T>(bool includeInactive) where T : class => GetComponent<T>();
    }
    public static class Resources
    {
        public static readonly Dictionary<string, Object> Values = new(StringComparer.OrdinalIgnoreCase);
        public static Item[] ResourceItems = Array.Empty<Item>();
        public static T Load<T>(string path) where T : Object => Values.TryGetValue(path, out var value) ? value as T : null;
        public static T[] LoadAll<T>(string path) where T : Object => ResourceItems.OfType<T>().ToArray();
    }
}
namespace Photon.Pun { public class PhotonView { } }
public class RunManager : UnityEngine.Object { public static RunManager instance; }
public class ValuableObject { }
public class CosmeticWorldObject { }
public class Item : UnityEngine.Object
{
    public string itemName;
    public bool disabled;
    public bool physicalItem = true;
    public PrefabRef prefab;
}
public class PrefabRef
{
    public string ResourcePath { get; private set; }
    public bool Broken;
    public UnityEngine.GameObject Prefab => Broken ? throw new InvalidOperationException("Broken third-party prefab") : UnityEngine.Resources.Load<UnityEngine.GameObject>(ResourcePath);
    public void SetPrefab(UnityEngine.GameObject prefab, string path) { ResourcePath = path; }
}
public class EnemyParent { public string enemyName; public UnityEngine.GameObject gameObject; }
public class EnemySetup { public EnemyParent Parent; }
namespace REPOLib.Modules
{
    public static class Items { public static List<Item> AllItems = new(); }
    public static class Valuables { public static List<PrefabRef> AllValuables = new(); }
    public static class Enemies { public static List<EnemySetup> AllEnemies = new(); }
}
namespace RepoLiveControl
{
    public static class Bridge { public static EnemyParent GetEnemyParent(EnemySetup setup) => setup.Parent; }
}
