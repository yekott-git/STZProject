using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
public partial struct ZombieAttackSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<FlowFieldState>();
        state.RequireForUpdate<GridOccupancy>();
        state.RequireForUpdate<WallIndexState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        GridConfig cfg = SystemAPI.GetSingleton<GridConfig>();
        float dt = SystemAPI.Time.DeltaTime;
        int width = cfg.Size.x;

        Entity flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
        DynamicBuffer<FlowFieldCell> flowCells = SystemAPI.GetBuffer<FlowFieldCell>(flowEntity);

        var map = SystemAPI.GetSingleton<WallIndexState>().Map;
        var healthLookup = state.GetComponentLookup<Health>(false);

        NativeArray<int2> dirs = new NativeArray<int2>(4, Allocator.Temp);
        dirs[0] = new int2(1, 0);
        dirs[1] = new int2(-1, 0);
        dirs[2] = new int2(0, 1);
        dirs[3] = new int2(0, -1);

        foreach (var (atk, tr) in SystemAPI
                        .Query<RefRW<ZombieAttack>, RefRO<LocalTransform>>()
                        .WithAll<ZombieTag>())
        {
            atk.ValueRW.Timer -= dt;
            if (atk.ValueRO.Timer > 0f)
                continue;

            int2 myCell = IsoGridUtility.WorldToGrid(cfg, tr.ValueRO.Position.xy);
            if (!IsoGridUtility.InBounds(cfg, myCell))
                continue;

            Entity targetEntity = Entity.Null;

            // 1) flow 방향 앞칸 우선 검사
            int idx = myCell.y * width + myCell.x;
            FlowFieldCell flow = flowCells[idx];
            int2 flowDir = new int2(flow.DirX, flow.DirY);

            if (!(flowDir.x == 0 && flowDir.y == 0))
            {
                int2 frontCell = myCell + flowDir;
                if (IsoGridUtility.InBounds(cfg, frontCell))
                {
                    int key = GridKeyUtility.CellKey(frontCell, width);
                    if (map.TryGetValue(key, out Entity e) && e != Entity.Null)
                    {
                        targetEntity = e;
                    }
                }
            }

            // 2) 앞칸에 없으면 주변 4칸 검사
            if (targetEntity == Entity.Null)
            {
                for (int i = 0; i < dirs.Length; i++)
                {
                    int2 adj = myCell + dirs[i];
                    if (!IsoGridUtility.InBounds(cfg, adj))
                        continue;

                    int key = GridKeyUtility.CellKey(adj, width);
                    if (map.TryGetValue(key, out Entity e) && e != Entity.Null)
                    {
                        targetEntity = e;
                        break;
                    }
                }
            }

            if (targetEntity == Entity.Null)
                continue;

            if (!healthLookup.HasComponent(targetEntity))
                continue;

            Health hp = healthLookup[targetEntity];
            hp.Value -= atk.ValueRO.Damage;
            healthLookup[targetEntity] = hp;

            atk.ValueRW.Timer = atk.ValueRO.Cooldown;
        }

        dirs.Dispose();
    }
}