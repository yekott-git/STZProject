using Unity.Entities;
using UnityEngine;

public class ImpactAuthoring : MonoBehaviour
{
    public float Lifetime = 0.3f;

    class Baker : Baker<ImpactAuthoring>
    {
        public override void Bake(ImpactAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(e, new Impact { Lifetime = authoring.Lifetime });
        }
    }
}