using Unity.Mathematics;

public static class IsoGridUtility
{
    // Grid(int2) -> World(float3)
    public static float3 GridToWorld(in GridConfig cfg, int2 cell)
    {
        float halfW = cfg.TileW * 0.5f;
        float halfH = cfg.TileH * 0.5f;

        float x = (cell.x - cell.y) * halfW + cfg.Origin.x;
        float y = (cell.x + cell.y) * halfH + cfg.Origin.y;

        // 아이소 정렬: 앞쪽이 더 위에 그려지게 z를 살짝 내림(음수로)
        float z = cfg.Origin.z - (cell.x + cell.y) * cfg.ZStep;

        return new float3(x, y, z);
    }

    // World(float2) -> Grid(int2)  (world.z 무시)
    public static int2 WorldToGrid(in GridConfig cfg, float2 worldXY)
    {
        float halfW = cfg.TileW * 0.5f;
        float halfH = cfg.TileH * 0.5f;

        float wx = worldXY.x - cfg.Origin.x;
        float wy = worldXY.y - cfg.Origin.y;

        // 역변환
        float gx = (wy / halfH + wx / halfW) * 0.5f;
        float gy = (wy / halfH - wx / halfW) * 0.5f;

        return new int2((int)math.floor(gx), (int)math.floor(gy));
    }

    public static bool InBounds(in GridConfig cfg, int2 c)
        => (uint)c.x < (uint)cfg.Size.x && (uint)c.y < (uint)cfg.Size.y;
}