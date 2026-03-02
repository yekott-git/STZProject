using Unity.Entities;
using UnityEngine;

public class ProjectileAuthoring : MonoBehaviour
{
    [Header("Stats")]
    public float Speed = 18f;
    public int Damage = 10;
    public float Lifetime = 3f;

    [Header("Optional Impact Prefab")]
    public GameObject ImpactPrefab;

    class Baker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);

            Entity impactEntity = Entity.Null;
            if (authoring.ImpactPrefab != null)
                impactEntity = GetEntity(authoring.ImpactPrefab, TransformUsageFlags.Dynamic);

            AddComponent(e, new Projectile
            {
                Velocity = default,
                Speed = authoring.Speed,
                Damage = authoring.Damage,
                Lifetime = authoring.Lifetime,
                ImpactPrefab = impactEntity
            });

            AddComponent<ProjectileTag>(e);
        }
    }
}