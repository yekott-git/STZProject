using Unity.Entities;
using UnityEngine;

public class CoreAuthoring : MonoBehaviour
{
    public int hp = 200;

    class Baker : Baker<CoreAuthoring>
    {
        public override void Bake(CoreAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.Renderable);

            AddComponent<BuildingTag>(e);
            AddComponent<CoreTag>(e);
            AddComponent<Damageable>(e);
            AddComponent<GameOverOnDeath>(e);

            AddComponent(e, new Health
            {
                Value = a.hp
            });
        }
    }
}