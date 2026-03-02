using Unity.Entities;
using Unity.Mathematics;

public struct Projectile : IComponentData
{
    public float2 Velocity;   // 월드 기준 속도
    public int Damage;
    public float Lifetime;    // 남은 수명(초)
}

public struct ProjectileTag : IComponentData {}