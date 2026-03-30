using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

public partial struct CoreDamageSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameState>();
        state.RequireForUpdate<CoreTag>();
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<DamageEventQueueTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var gsRW = SystemAPI.GetSingletonRW<GameState>();
        if (gsRW.ValueRO.IsGameOver != 0)
            return;

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var dt = SystemAPI.Time.DeltaTime;

        var coreEntity = SystemAPI.GetSingletonEntity<CoreTag>();
        var coreCell = SystemAPI.GetComponent<GridCell>(coreEntity).Value;

        var queueEntity = SystemAPI.GetSingletonEntity<DamageEventQueueTag>();
        var damageBuffer = SystemAPI.GetBuffer<DamageEvent>(queueEntity);

        foreach (var (atk, tr) in SystemAPI.Query<RefRW<ZombieAttack>, RefRO<LocalTransform>>().WithAll<ZombieTag>())
        {
            atk.ValueRW.Timer -= dt;
            if (atk.ValueRO.Timer > 0f)
                continue;

            var zCell = IsoGridUtility.WorldToGrid(cfg, tr.ValueRO.Position.xy);
            if (zCell.x != coreCell.x || zCell.y != coreCell.y)
                continue;

            damageBuffer.Add(new DamageEvent
            {
                Target = coreEntity,
                Value = atk.ValueRO.Damage
            });

            atk.ValueRW.Timer = atk.ValueRO.Cooldown;
        }
    }
}