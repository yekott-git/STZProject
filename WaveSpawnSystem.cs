using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial class WaveSpawnSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GridConfig>();
        RequireForUpdate<ZombiePrefabRef>();
        RequireForUpdate<WaveSpawner>();
        RequireForUpdate<GameState>();
    }

    protected override void OnUpdate()
    {
        var gs = SystemAPI.GetSingleton<GameState>();
        if (gs.IsGameOver != 0)
            return;

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var prefab = SystemAPI.GetSingleton<ZombiePrefabRef>().Prefab;
        var coreEntity = SystemAPI.GetSingletonEntity<CoreTag>();
        int2 coreCell = SystemAPI.GetComponent<GridCell>(coreEntity).Value;

        float dt = SystemAPI.Time.DeltaTime;
        var spRW = SystemAPI.GetSingletonRW<WaveSpawner>();
        var sp = spRW.ValueRW;

        // Break 상태
        if (sp.State == 0)
        {
            sp.BreakTimer -= dt;

            if (sp.BreakTimer <= 0f)
            {
                sp.Wave += 1;
                int wave = sp.Wave;

                sp.ZombiesToSpawn = 5 + wave * 3;
                sp.ZombiesSpawned = 0;
                sp.ZombiesAlive = 0;

                sp.SpawnInterval = math.max(0.12f, 0.5f - wave * 0.02f);
                sp.Timer = 0f;
                sp.State = 1;

                Debug.Log("Wave Start: " + wave);
            }

            spRW.ValueRW = sp;
            return;
        }

        // Spawning 상태
        sp.Timer -= dt;

        if (sp.ZombiesSpawned < sp.ZombiesToSpawn && sp.Timer <= 0f)
        {
            sp.Timer = sp.SpawnInterval;

            if (TrySpawnZombie(cfg, prefab, coreCell, sp.Wave))
            {
                sp.ZombiesSpawned++;
                sp.ZombiesAlive++;
            }
        }

        // 이번 웨이브 전부 스폰했고, 남아있는 좀비도 없으면 클리어
        if (sp.ZombiesSpawned >= sp.ZombiesToSpawn && sp.ZombiesAlive <= 0)
        {
            Debug.Log("Wave Clear: " + sp.Wave);
            sp.State = 0;
            sp.BreakTimer = sp.BreakDuration;
        }

        spRW.ValueRW = sp;
    }

    private bool TrySpawnZombie(GridConfig cfg, Entity prefab, int2 coreCell, int wave)
    {
        var rng = new Unity.Mathematics.Random((uint)(1234 + wave * 999 + (int)(SystemAPI.Time.ElapsedTime * 1000)));

        int side = rng.NextInt(4);
        int2 cell = side switch
        {
            0 => new int2(0, rng.NextInt(0, cfg.Size.y)),
            1 => new int2(cfg.Size.x - 1, rng.NextInt(0, cfg.Size.y)),
            2 => new int2(rng.NextInt(0, cfg.Size.x), 0),
            _ => new int2(rng.NextInt(0, cfg.Size.x), cfg.Size.y - 1),
        };

        var z = EntityManager.Instantiate(prefab);

        float3 pos = IsoGridUtility.GridToWorld(cfg, cell);
        EntityManager.SetComponentData(z, LocalTransform.FromPosition(pos));

        var mv = EntityManager.GetComponentData<ZombieMove>(z);
        mv.TargetCell = coreCell;
        EntityManager.SetComponentData(z, mv);

        return true;
    }
}