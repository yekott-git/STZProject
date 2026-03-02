using Unity.Entities;
using Unity.Mathematics;

public struct CmdBuild : IComponentData
{
    public int BuildingType;  // 0=Wall 등
    public int2 Cell;
}