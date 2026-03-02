using Unity.Entities;
using UnityEngine;

public class ZombiePrefabAuthoring : MonoBehaviour
{
    public GameObject prefab;

    class Baker : Baker<ZombiePrefabAuthoring>
    {
        public override void Bake(ZombiePrefabAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.None);
            var prefabEntity = GetEntity(authoring.prefab, TransformUsageFlags.Renderable);

            AddComponent(e, new ZombiePrefabRef
            {
                Prefab = prefabEntity
            });
        }
    }
}