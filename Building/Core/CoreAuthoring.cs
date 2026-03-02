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
            AddComponent<CoreTag>(e);
            AddComponent(e, new Health { Value = a.hp });
        }
    }
}