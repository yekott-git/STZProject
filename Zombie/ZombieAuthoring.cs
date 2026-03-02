using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ZombieAuthoring : MonoBehaviour
{
    public float speed = 2f;

    class Baker : Baker<ZombieAuthoring>
    {
        public override void Bake(ZombieAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Renderable);

            AddComponent<ZombieTag>(e);
            AddComponent(e, new ZombieMove
            {
                Speed = authoring.speed,
                TargetCell = int2.zero
            });
            AddComponent(e, new ZombieAttack
            {
                Cooldown = 0.5f,
                Timer = 0f,
                Damage = 5
            });
            AddComponent(e, new Health { Value = 20 });
        }
    }
}