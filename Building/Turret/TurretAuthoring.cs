using Unity.Entities;
using UnityEngine;

public class TurretAuthoring : MonoBehaviour
{
    public float range = 6f;
    public float cooldown = 0.25f;
    public int damage = 2;
    public int hp = 80;

    class Baker : Baker<TurretAuthoring>
    {
        public override void Bake(TurretAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.Renderable);

            AddComponent<TurretTag>(e);
            AddComponent(e, new TurretAttack
            {
                Range = a.range,
                Cooldown = a.cooldown,
                Timer = 0f,
                Damage = a.damage
            });

            AddComponent(e, new Health { Value = a.hp });
        }
    }
}