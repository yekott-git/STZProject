using Unity.Entities;
using UnityEngine;

public class ProjectilePrefabAuthoring : MonoBehaviour
{
    public GameObject prefab;

    class Baker : Baker<ProjectilePrefabAuthoring>
    {
        public override void Bake(ProjectilePrefabAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.None);
            var p = GetEntity(a.prefab, TransformUsageFlags.Renderable);
            AddComponent(e, new ProjectilePrefabRef { Prefab = p });
        }
    }
}