using Unity.Entities;
using UnityEngine;

public class CorePrefabAuthoring : MonoBehaviour
{
    public GameObject prefab;

    class Baker : Baker<CorePrefabAuthoring>
    {
        public override void Bake(CorePrefabAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.None);
            var p = GetEntity(a.prefab, TransformUsageFlags.Renderable);
            AddComponent(e, new CorePrefabRef { Prefab = p });
        }
    }
}