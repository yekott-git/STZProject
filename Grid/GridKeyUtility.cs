using Unity.Mathematics;

public static class GridKeyUtility
{
    // width는 GridConfig.Size.x
    public static int CellKey(int2 cell, int width) => cell.y * width + cell.x;
}