using Unity.Entities;

public struct ZombieAttack : IComponentData
{
    public float Cooldown;
    public float Timer;
    public int Damage;
}