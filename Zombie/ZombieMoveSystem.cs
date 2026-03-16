using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
public partial struct ZombieMoveSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<FlowFieldState>();
        state.RequireForUpdate<GridOccupancy>();
    }

    public void OnUpdate(ref SystemState state)
    {
        GridConfig cfg = SystemAPI.GetSingleton<GridConfig>();
        Entity flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
        DynamicBuffer<FlowFieldCell> flowCells = SystemAPI.GetBuffer<FlowFieldCell>(flowEntity);

        Entity occEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        DynamicBuffer<OccCell> occ = SystemAPI.GetBuffer<OccCell>(occEntity);

        int width = cfg.Size.x;
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (transform, move) in SystemAPI
                        .Query<RefRW<LocalTransform>, RefRO<ZombieMove>>()
                        .WithAll<ZombieTag>())
        {
            float2 worldPos = transform.ValueRO.Position.xy;
            int2 currentCell = IsoGridUtility.WorldToGrid(cfg, worldPos);

            if (!IsoGridUtility.InBounds(cfg, currentCell))
                continue;

            int index = currentCell.y * width + currentCell.x;
            FlowFieldCell flow = flowCells[index];

            int2 dir = new int2(flow.DirX, flow.DirY);
            if (dir.x == 0 && dir.y == 0)
                continue;

            int2 nextCell = currentCell + dir;
            if (!IsoGridUtility.InBounds(cfg, nextCell))
                continue;

            int nextIdx = nextCell.y * width + nextCell.x;

            // 막힌 셀이면 이동하지 않음
            if (occ[nextIdx].Value != 0)
                continue;

            float3 nextWorld = IsoGridUtility.GridToWorld(cfg, nextCell);
            float2 toNext = nextWorld.xy - worldPos;
            float dist = math.length(toNext);

            if (dist < 0.001f)
                continue;

            float2 moveDir = toNext / dist;
            float step = math.min(move.ValueRO.Speed * dt, dist);
            float2 nextPos = worldPos + moveDir * step;

            LocalTransform tr = transform.ValueRO;
            tr.Position.x = nextPos.x;
            tr.Position.y = nextPos.y;
            transform.ValueRW = tr;
        }
    }
}
