using Unity.Entities;
using UnityEngine;

public class WallAuthoring : MonoBehaviour
{
    public int hp = 50;

    class Baker : Baker<WallAuthoring>
    {
        public override void Bake(WallAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);

            AddComponent<BuildingTag>(entity);
            AddComponent<DefenseStructureTag>(entity);
            AddComponent<WallTag>(entity);
            AddComponent<Damageable>(entity);
            AddComponent<DestroyOnDeath>(entity);

            AddComponent(entity, new DefenseTargetPriority
            {
                Value = 100
            });

            AddComponent(entity, new WallData
            {
                MaxHP = authoring.hp
            });

            AddComponent(entity, new Health
            {
                Value = authoring.hp
            });

        }
    }
}