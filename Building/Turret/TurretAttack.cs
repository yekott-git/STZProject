using Unity.Entities;

public struct TurretAttack : IComponentData
{
    public float Range;
    public float Cooldown;
    public float Timer;
    public int Damage;
}