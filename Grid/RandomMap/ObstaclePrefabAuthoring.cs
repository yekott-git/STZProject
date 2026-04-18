using Unity.Entities;
using UnityEngine;

public class ObstaclePrefabAuthoring : MonoBehaviour
{
    public GameObject prefab;

    class Baker : Baker<ObstaclePrefabAuthoring>
    {
        public override void Bake(ObstaclePrefabAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            Entity prefabEntity = Entity.Null;
            if (authoring.prefab != null)
                prefabEntity = GetEntity(authoring.prefab, TransformUsageFlags.Renderable);

            AddComponent(entity, new ObstaclePrefabRef
            {
                Prefab = prefabEntity
            });
        }
    }
}