using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct WaveSpawnSystem : ISystem
{
    Unity.Mathematics.Random rng;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<ZombiePrefabRef>();
        state.RequireForUpdate<WaveSpawner>();
        state.RequireForUpdate<GameState>();
        state.RequireForUpdate<CoreTag>();

        rng = new Unity.Mathematics.Random(0x6E624EB7u);
    }

    public void OnUpdate(ref SystemState state)
    {
        var gs = SystemAPI.GetSingleton<GameState>();
        if (gs.IsGameOver != 0)
            return;

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var prefab = SystemAPI.GetSingleton<ZombiePrefabRef>().Prefab;
        var coreEntity = SystemAPI.GetSingletonEntity<CoreTag>();
        var coreCell = SystemAPI.GetComponent<GridCell>(coreEntity).Value;

        var dt = SystemAPI.Time.DeltaTime;
        var spawner = SystemAPI.GetSingletonRW<WaveSpawner>();
        var sp = spawner.ValueRW;

        if (sp.State == 0)
        {
            sp.BreakTimer -= dt;

            if (sp.BreakTimer <= 0f)
            {
                sp.Wave += 1;

                // 테스트용
                sp.ZombiesToSpawn = 100;
                sp.ZombiesSpawned = 0;
                sp.ZombiesAlive = 0;

                sp.SpawnInterval = 0.01f;
                sp.Timer = 0f;
                sp.State = 1;
                sp.SpawnSide = rng.NextInt(0, 4);

                Debug.Log("Wave Start: " + sp.Wave);
            }

            spawner.ValueRW = sp;
            return;
        }

        sp.Timer -= dt;

        if (sp.ZombiesSpawned < sp.ZombiesToSpawn && sp.Timer <= 0f)
        {
            sp.Timer = sp.SpawnInterval;

            var burstCount = 100;
            var remain = sp.ZombiesToSpawn - sp.ZombiesSpawned;
            var spawnCount = math.min(burstCount, remain);

            for (var i = 0; i < spawnCount; i++)
            {
                if (TrySpawnZombie(ref state, cfg, prefab, coreCell, sp.SpawnSide))
                {
                    sp.ZombiesSpawned++;
                    sp.ZombiesAlive++;
                }
            }

            Debug.Log("Alive: " + sp.ZombiesAlive + " / Spawned: " + sp.ZombiesSpawned);
        }

        if (sp.ZombiesSpawned >= sp.ZombiesToSpawn && sp.ZombiesAlive <= 0)
        {
            Debug.Log("Wave Clear: " + sp.Wave);
            sp.State = 0;
            sp.BreakTimer = sp.BreakDuration;
        }

        spawner.ValueRW = sp;
    }

    bool TrySpawnZombie(ref SystemState state, GridConfig cfg, Entity prefab, int2 coreCell, int spawnSide)
    {
        if (prefab == Entity.Null)
            return false;

        var width = cfg.Size.x;
        var gridEntity = SystemAPI.GetSingletonEntity<GridConfig>();
        var occBuf = SystemAPI.GetBuffer<OccCell>(gridEntity);

        for (var attempt = 0; attempt < 32; attempt++)
        {
            int2 cell = spawnSide switch
            {
                0 => new int2(0, rng.NextInt(0, cfg.Size.y)),
                1 => new int2(cfg.Size.x - 1, rng.NextInt(0, cfg.Size.y)),
                2 => new int2(rng.NextInt(0, cfg.Size.x), 0),
                _ => new int2(rng.NextInt(0, cfg.Size.x), cfg.Size.y - 1),
            };

            if (!IsoGridUtility.InBounds(cfg, cell))
                continue;

            var idx = cell.y * width + cell.x;
            if (occBuf[idx].Value != 0)
                continue;

            var zombie = state.EntityManager.Instantiate(prefab);
            var pos = IsoGridUtility.GridToWorld(cfg, cell);

            state.EntityManager.SetComponentData(zombie, LocalTransform.FromPosition(pos));

            var move = state.EntityManager.GetComponentData<ZombieMove>(zombie);
            move.TargetCell = coreCell;
            move.CurrentStepCell = int2.zero;
            move.HasStepCell = 0;
            move.SeparationRadius = 0.55f;
            move.SeparationWeight = 0.35f;
            state.EntityManager.SetComponentData(zombie, move);

            state.EntityManager.SetComponentData(zombie, new ZombieSeparation
            {
                Force = float2.zero
            });
            return true;
        }

        return false;
    }
}