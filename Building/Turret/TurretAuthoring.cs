using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class TurretAuthoring : MonoBehaviour
{
    [Header("Attack")]
    public float Range = 6f;
    public float FireInterval = 0.5f;
    public int Damage = 100;

    [Header("Projectile Prefab (Baked Entity)")]
    public GameObject ProjectilePrefab;

    [Header("Aim")]
    public float TurnSpeed = 10f;

    class Baker : Baker<TurretAuthoring>
    {
        public override void Bake(TurretAuthoring authoring)
        {
            var turretEntity = GetEntity(TransformUsageFlags.Dynamic);

            Entity projectileEntity = Entity.Null;
            if (authoring.ProjectilePrefab != null)
                projectileEntity = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic);

            AddComponent<TurretTag>(turretEntity);

            AddComponent(turretEntity, new TurretAttack
            {
                Range = authoring.Range,
                Cooldown = math.max(0.01f, authoring.FireInterval),
                Timer = 0f,
                Damage = authoring.Damage,
                ProjectilePrefab = projectileEntity
            });

            AddComponent(turretEntity, new TurretTarget
            {
                Target = Entity.Null,
                AimDir = new float2(0f, 1f),
                TurnSpeed = math.max(0f, authoring.TurnSpeed)
            });
        }
    }
}