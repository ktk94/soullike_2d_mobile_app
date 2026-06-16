using UnityEngine;
using System.Collections.Generic;

namespace SoulCraft.Core
{
    public class ObjectPool : MonoBehaviour
    {
        public static ObjectPool Instance { get; private set; }

        private readonly Dictionary<string, Queue<GameObject>> _pools = new();
        private readonly Dictionary<string, GameObject> _prefabs = new();

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void RegisterPool(string key, GameObject prefab, int initialCount = 10)
        {
            if (_pools.ContainsKey(key)) return;

            _prefabs[key] = prefab;
            _pools[key] = new Queue<GameObject>();

            for (int i = 0; i < initialCount; i++)
            {
                var obj = Instantiate(prefab, transform);
                obj.SetActive(false);
                _pools[key].Enqueue(obj);
            }
        }

        public GameObject Spawn(string key, Vector3 position, Quaternion rotation)
        {
            if (!_pools.ContainsKey(key)) return null;

            GameObject obj;
            if (_pools[key].Count > 0)
            {
                obj = _pools[key].Dequeue();
            }
            else
            {
                obj = Instantiate(_prefabs[key], transform);
            }

            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);
            return obj;
        }

        public void Despawn(string key, GameObject obj, float delay = 0f)
        {
            if (delay > 0f)
                StartCoroutine(DespawnDelayed(key, obj, delay));
            else
                ReturnToPool(key, obj);
        }

        private System.Collections.IEnumerator DespawnDelayed(string key, GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool(key, obj);
        }

        private void ReturnToPool(string key, GameObject obj)
        {
            obj.SetActive(false);
            if (_pools.ContainsKey(key))
                _pools[key].Enqueue(obj);
        }
    }
}
