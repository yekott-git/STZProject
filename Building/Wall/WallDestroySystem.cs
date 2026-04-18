using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GameOverOnDeathSystem))]
public partial struct BuildingDeathCleanupSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GridOccupancy>();
        state.RequireForUpdate<WallIndexState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        var occ = SystemAPI.GetBuffer<OccCell>(gridEntity);
        var width = cfg.Size.x;

        var defenseMap = SystemAPI.GetSingleton<WallIndexState>().Map;

        using var deadBuildings = new NativeList<Entity>(Allocator.Temp);

        foreach (var (cell, hp, entity) in
                 SystemAPI.Query<RefRO<GridCell>, RefRO<Health>>()
                     .WithAll<BuildingTag, DestroyOnDeath>()
                     .WithEntityAccess())
        {
            if (hp.ValueRO.Value > 0)
                continue;

            var c = cell.ValueRO.Value;

            if ((uint)c.x < (uint)cfg.Size.x && (uint)c.y < (uint)cfg.Size.y)
            {
                var idx = c.y * width + c.x;
                occ[idx] = new OccCell { Value = 0 };
            }

            var key = GridKeyUtility.CellKey(c, width);
            defenseMap.Remove(key);

            deadBuildings.Add(entity);
        }

        for (int i = 0; i < deadBuildings.Length; i++)
            state.EntityManager.DestroyEntity(deadBuildings[i]);
    }
}