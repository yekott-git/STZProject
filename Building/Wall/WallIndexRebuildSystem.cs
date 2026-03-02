using Unity.Entities;
using Unity.Collections;

public partial class WallIndexRebuildSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<WallIndexState>();
        RequireForUpdate<GridConfig>();
    }

    protected override void OnStartRunning()
    {
        // 월드가 돌아가기 시작할 때 한 번, 현재 존재하는 벽으로 맵 재구축
        Rebuild();
    }

    void Rebuild()
    {
        var cfg = SystemAPI.GetSingleton<GridConfig>();
        int width = cfg.Size.x;

        var stateRW = SystemAPI.GetSingletonRW<WallIndexState>();
        var map = stateRW.ValueRW.Map;

        map.Clear();

        // 현재 월드에 존재하는 모든 벽을 다시 등록
        Entities
            .WithAll<WallTag>()
            .ForEach((Entity e, in GridCell cell) =>
            {
                int key = GridKeyUtility.CellKey(cell.Value, width);
                map[key] = e;
            }).Run();
    }

    protected override void OnUpdate() { }
}