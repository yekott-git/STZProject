using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ZombieAuthoring : MonoBehaviour
{
    public float speed = 2.2f;
    public int hp = 100;
    public int damage = 10;
    public float attackCooldown = 0.5f;

    [Header("Move")]
    public float flowWeight = 1f;
    public float laneBiasStrength = 0.08f;
    public float separationRadius = 0.75f;
    public float separationWeight = 0.9f;

    class Baker : Baker<ZombieAuthoring>
    {
        public override void Bake(ZombieAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent<ZombieTag>(entity);

            AddComponent(entity, new Health
            {
                Value = authoring.hp
            });

            AddComponent(entity, new ZombieMove
            {
                Speed = authoring.speed,
                FlowWeight = authoring.flowWeight,
                LaneBiasStrength = authoring.laneBiasStrength,
                SeparationRadius = authoring.separationRadius,
                SeparationWeight = authoring.separationWeight,

                LastGridCell = int2.zero,
                StuckFrames = 0,
                LastMoveDir = float2.zero,

                // 과도기 호환용
                TargetCell = int2.zero,
                CurrentStepCell = int2.zero,
                HasStepCell = 0
            });

            AddComponent(entity, new ZombieAttack
            {
                Damage = authoring.damage,
                Cooldown = math.max(0.05f, authoring.attackCooldown),
                Timer = 0f
            });

            AddComponent(entity, new ZombieSeparation
            {
                Force = float2.zero
            });

            AddComponent(entity, new ZombieCurrentTarget
            {
                Value = Entity.Null
            });
        }
    }
}