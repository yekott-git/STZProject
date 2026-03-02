using Unity.Entities;
using UnityEngine;

public class TurretPrefabAuthoring : MonoBehaviour
{
    public GameObject prefab;

    class Baker : Baker<TurretPrefabAuthoring>
    {
        public override void Bake(TurretPrefabAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.None);
            var p = GetEntity(a.prefab, TransformUsageFlags.Renderable);
            AddComponent(e, new TurretPrefabRef { Prefab = p });
        }
    }
}