using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct HealthDeathSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WaveSpawner>();
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GridOccupancy>();
        state.RequireForUpdate<FlowFieldState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        GridConfig cfg = SystemAPI.GetSingleton<GridConfig>();
        var waveSpawnerRW = SystemAPI.GetSingletonRW<WaveSpawner>();

        Entity gridEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        DynamicBuffer<OccCell> occ = SystemAPI.GetBuffer<OccCell>(gridEntity);

        Entity flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
        var flowRW = SystemAPI.GetComponentRW<FlowFieldState>(flowEntity);

        foreach (var (hp, tr, entity) in SystemAPI
                     .Query<RefRO<Health>, RefRO<LocalTransform>>()
                     .WithNone<DeadTag>()
                     .WithEntityAccess())
        {
            if (hp.ValueRO.Value > 0)
                continue;

            ecb.AddComponent<DeadTag>(entity);

            // 1) 죽은 엔티티가 점유하던 셀 해제
            int2 cell = IsoGridUtility.WorldToGrid(cfg, tr.ValueRO.Position.xy);
            if (IsoGridUtility.InBounds(cfg, cell))
            {
                int idx = cell.y * cfg.Size.x + cell.x;
                if ((uint)idx < (uint)occ.Length)
                    occ[idx] = new OccCell { Value = 0 };
            }

            // 2) 좀비면 웨이브 생존 수 감소
            if (SystemAPI.HasComponent<ZombieTag>(entity))
            {
                waveSpawnerRW.ValueRW.ZombiesAlive =
                    math.max(0, waveSpawnerRW.ValueRO.ZombiesAlive - 1);
            }

            // 3) 길이 바뀌었을 수 있으니 flow field 재빌드 요청
            flowRW.ValueRW.Dirty = 1;

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}