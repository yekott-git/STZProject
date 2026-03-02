using Unity.Entities;
using UnityEngine;

public class WallPrefabAuthoring : MonoBehaviour
{
    public GameObject prefab;

    class Baker : Baker<WallPrefabAuthoring>
    {
        public override void Bake(WallPrefabAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var prefabEntity = GetEntity(authoring.prefab, TransformUsageFlags.Renderable);

            AddComponent(entity, new WallPrefabRef
            {
                Prefab = prefabEntity
            });
        }
    }
}