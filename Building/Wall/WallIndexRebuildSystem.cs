using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(RandomMapApplySystem))]
public partial struct WallIndexRebuildSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WallIndexState>();
        state.RequireForUpdate<GridConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var width = cfg.Size.x;

        var stateRW = SystemAPI.GetSingletonRW<WallIndexState>();
        var map = stateRW.ValueRW.Map;

        map.Clear();

        foreach (var (cell, entity) in
                 SystemAPI.Query<RefRO<GridCell>>()
                     .WithAll<DefenseStructureTag>()
                     .WithEntityAccess())
        {
            int key = GridKeyUtility.CellKey(cell.ValueRO.Value, width);
            map[key] = entity;
        }

        state.Enabled = false;
    }
}