using Unity.Entities;
using Unity.Mathematics;

public struct GridConfig:IComponentData
{
    public int2 Size;
    public float TileW;
    public float TileH;
    public float ZStep;
    public float3 Origin;

}
