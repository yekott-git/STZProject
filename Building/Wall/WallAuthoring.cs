using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

public class WallAuthoring : MonoBehaviour
{
    class Baker : Baker<WallAuthoring>
    {
        public override void Bake(WallAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent<WallTag>(entity);
            AddComponent(entity, new WallData { MaxHP = 50 });
            AddComponent(entity, new Health { Value = 50 });
        }
    }
}