using Unity.Entities;
using Unity.Mathematics;

public struct Projectile : IComponentData
{
    public float2 Velocity;     // 런타임에서 MoveSystem이 사용
    public float Speed;         // ✅ 프리팹에서 세팅해둘 값
    public int Damage;
    public float Lifetime;

    public Entity ImpactPrefab; // ✅ (선택) 맞았을 때 스폰할 임팩트 프리팹
}

public struct ProjectileTag : IComponentData {}