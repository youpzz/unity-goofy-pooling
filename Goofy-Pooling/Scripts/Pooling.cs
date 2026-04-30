using System.Collections.Generic;
using UnityEngine;

public static class Pooling
{
    // int = instance.GetInstanceID(), GameObject = prefab
    private static readonly Dictionary<int, GameObject> prefabMap = new(1024);
    private static readonly Dictionary<GameObject, Stack<GameObject>> pools = new();
    private static Transform _poolRoot;

    private static Transform PoolRoot
    {
        get
        {
            if (_poolRoot != null) return _poolRoot;
            _poolRoot = new GameObject("[Pool]").transform;
            Object.DontDestroyOnLoad(_poolRoot.gameObject);
            return _poolRoot;
        }
    }

    private static Stack<GameObject> GetOrCreate(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<GameObject>(256);
            pools[prefab] = stack;
        }
        return stack;
    }

    public static void Prewarm(GameObject prefab, int count)
    {
        var stack = GetOrCreate(prefab);
        for (int i = 0; i < count; i++)
        {
            var instance = CreateInstance(prefab, PoolRoot);
            instance.SetActive(false);
            stack.Push(instance);
        }
    }

    public static GameObject Instantiate(
        GameObject prefab,
        Vector3 position = default,
        Quaternion rotation = default,
        Transform parent = null)
    {
        var stack = GetOrCreate(prefab);

        GameObject instance;
        if (stack.Count > 0)
        {
            instance = stack.Pop();
            instance.transform.SetPositionAndRotation(position, rotation);
            if (parent != null) instance.transform.SetParent(parent, true);
        }
        else
        {
            instance = CreateInstance(prefab, parent);
            instance.transform.SetPositionAndRotation(position, rotation);
        }

        instance.SetActive(true);
        return instance;
    }

    public static GameObject Instantiate(GameObject prefab, Transform parent) => Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);

    public static void Destroy(GameObject instance)
    {
        // int lookup — O(1), никаких компонентов, никакого ref comparison
        if (!prefabMap.TryGetValue(instance.GetInstanceID(), out var prefab)) return;
        if (!instance.activeSelf) return;

        instance.SetActive(false);
        GetOrCreate(prefab).Push(instance);
    }

    public static void Clear(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out var stack)) return;
        while (stack.Count > 0)
        {
            var instance = stack.Pop();
            prefabMap.Remove(instance.GetInstanceID());
            Object.Destroy(instance);
        }
        pools.Remove(prefab);
    }

    public static void ClearAll() { foreach (var prefab in new List<GameObject>(pools.Keys)) Clear(prefab); }

    private static GameObject CreateInstance(GameObject prefab, Transform parent)
    {
        var instance = Object.Instantiate(prefab, parent);
        prefabMap[instance.GetInstanceID()] = prefab;
        return instance;
    }
}