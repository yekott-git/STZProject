using Unity.Entities;

public struct AttackSlotConfig : IComponentData
{
    public byte Pattern;
    public byte MaxAttackers;
}