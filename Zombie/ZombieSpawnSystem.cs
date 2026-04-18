using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct CmdSpawnZombie : IComponentData
{
    public float3 Position;
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ZombieSpawnSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ZombiePrefabRef>();
        state.RequireForUpdate<GridConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var prefab = SystemAPI.GetSingleton<ZombiePrefabRef>().Prefab;
        if (prefab == Entity.Null)
            return;

        var cfg = SystemAPI.GetSingleton<GridConfig>();

        var query = SystemAPI.QueryBuilder().WithAll<CmdSpawnZombie>().Build();
        if (query.IsEmpty)
            return;

        using var cmdEntities = query.ToEntityArray(Allocator.Temp);
        using var cmds = query.ToComponentDataArray<CmdSpawnZombie>(Allocator.Temp);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        for (int i = 0; i < cmdEntities.Length; i++)
        {
            var cmdEntity = cmdEntities[i];
            var cmd = cmds[i];

            var spawned = state.EntityManager.Instantiate(prefab);

            state.EntityManager.SetComponentData(
                spawned,
                LocalTransform.FromPosition(cmd.Position)
            );

            var cell = IsoGridUtility.WorldToGrid(cfg, cmd.Position.xy);

            if (state.EntityManager.HasComponent<GridCell>(spawned))
            {
                state.EntityManager.SetComponentData(spawned, new GridCell
                {
                    Value = cell
                });
            }
            else
            {
                state.EntityManager.AddComponentData(spawned, new GridCell
                {
                    Value = cell
                });
            }

            if (state.EntityManager.HasComponent<ZombieCurrentTarget>(spawned))
            {
                state.EntityManager.SetComponentData(spawned, new ZombieCurrentTarget
                {
                    Value = Entity.Null
                });
            }
            else
            {
                state.EntityManager.AddComponentData(spawned, new ZombieCurrentTarget
                {
                    Value = Entity.Null
                });
            }

            if (state.EntityManager.HasComponent<ZombieMove>(spawned))
            {
                var move = state.EntityManager.GetComponentData<ZombieMove>(spawned);
                move.LastGridCell = cell;
                move.StuckFrames = 0;
                move.LastMoveDir = float2.zero;
                move.TargetCell = int2.zero;
                move.CurrentStepCell = int2.zero;
                move.HasStepCell = 0;
                state.EntityManager.SetComponentData(spawned, move);
            }

            if (state.EntityManager.HasComponent<ZombieAttack>(spawned))
            {
                var attack = state.EntityManager.GetComponentData<ZombieAttack>(spawned);
                attack.Timer = 0f;
                state.EntityManager.SetComponentData(spawned, attack);
            }

            if (state.EntityManager.HasComponent<ZombieSeparation>(spawned))
            {
                state.EntityManager.SetComponentData(spawned, new ZombieSeparation
                {
                    Force = float2.zero
                });
            }

            ecb.DestroyEntity(cmdEntity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}