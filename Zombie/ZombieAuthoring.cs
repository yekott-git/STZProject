using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ZombieAuthoring : MonoBehaviour
{
    public float speed = 2f;
    public float separationRadius = 1f;
    public float separationWeight = 2f;
    public float attackCooldown = 0.5f;
    public int attackDamage = 1;
    public int health = 20;
    public float FlowWeight = 0.9f;
    public float LaneBiasStrength = 0.22f;

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
                SeparationWeight = authoring.separationWeight,
                FlowWeight = authoring.FlowWeight,
                LaneBiasStrength = authoring.LaneBiasStrength
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

            AddComponent(e, new AttackSlotAssignment
            {
                Target = Entity.Null,
                SlotCell = default,
                SlotIndex = 0,
                HasSlot = 0
            });
        }
    }
}