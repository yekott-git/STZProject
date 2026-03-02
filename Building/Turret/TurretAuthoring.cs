using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace STZProject.Building.Turret
{
    public class TurretAuthoring : MonoBehaviour
    {
        [Header("Attack")]
        public float Range = 6f;
        public float FireInterval = 0.5f;   // TurretAttack.Cooldown에 들어감
        public int Damage = 10;

        [Header("Projectile Prefab (Baked Entity)")]
        public GameObject ProjectilePrefab;

        [Header("Aim")]
        public float TurnSpeed = 10f;       // 0이면 회전 안함

        class Baker : Baker<TurretAuthoring>
        {
            public override void Bake(TurretAuthoring authoring)
            {
                var turretEntity = GetEntity(TransformUsageFlags.Dynamic);

                // ✅ 투사체 프리팹 엔티티로 변환
                Entity projEntity = Entity.Null;
                if (authoring.ProjectilePrefab != null)
                {
                    projEntity = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic);
                }

                AddComponent(turretEntity, new TurretAttack
                {
                    Range = authoring.Range,
                    Cooldown = math.max(0.01f, authoring.FireInterval),
                    Timer = 0f,                 // 시작하자마자 쏘게 하려면 0
                    Damage = authoring.Damage,
                    ProjectilePrefab = projEntity
                });

                // ✅ 타겟/에임 기본값 (Acquire/Aim 시스템이 이걸 씀)
                AddComponent(turretEntity, new TurretTarget
                {
                    Target = Entity.Null,
                    AimDir = new float2(0, 1),
                    TurnSpeed = math.max(0f, authoring.TurnSpeed)
                });
            }
        }
    }
}