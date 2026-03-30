using Unity.Entities;
using Unity.Mathematics;

public struct AttackSlotAssignment : IComponentData
{
    public Entity Target;

    public int2 SlotCell;
    public byte SlotIndex;
    public byte HasSlot;

    public int2 StandbyCell;
    public byte HasStandby;
}