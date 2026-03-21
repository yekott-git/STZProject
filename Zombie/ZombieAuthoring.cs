using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ZombieAuthoring : MonoBehaviour
{
    public float speed = 2f;
    public float separationRadius = 0.55f;
    public float separationWeight = 0.35f;
    public float attackCooldown = 0.5f;
    public int attackDamage = 5;
    public int health = 20;

    class Baker : Baker<ZombieAuthoring>
    {
        public override void Bake(ZombieAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Renderable);

            AddComponent<ZombieTag>(e);

            AddComponent(e, new ZombieMove
            {
                Speed = authoring.speed,
                TargetCell = int2.zero,
                CurrentStepCell = int2.zero,
                HasStepCell = 0,
                SeparationRadius = authoring.separationRadius,
                SeparationWeight = authoring.separationWeight
            });

            AddComponent(e, new ZombieSeparation
            {
                Force = float2.zero
            });

            AddComponent(e, new ZombieAttack
            {
                Cooldown = authoring.attackCooldown,
                Timer = 0f,
                Damage = authoring.attackDamage
            });

            AddComponent(e, new Health
            {
                Value = authoring.health
            });
        }
    }
}