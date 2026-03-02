using Unity.Entities;
using UnityEngine;

public class ProjectileAuthoring : MonoBehaviour
{
    public float speed = 18f;
    public float lifetime = 1.5f;
    public int damage = 2;

    class Baker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.Renderable);
            AddComponent<ProjectileTag>(e);
            AddComponent(e, new Projectile
            {
                Velocity = default,
                Damage = a.damage,
                Lifetime = a.lifetime
            });
        }
    }
}