using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(ZombieSpawnSystem))]
public partial struct WaveSpawnSystem : ISystem
{
    const byte StateSpawning = 0;
    const byte StateBreak = 1;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GameState>();
        state.RequireForUpdate<WaveSpawner>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var gameState = SystemAPI.GetSingleton<GameState>();
        if (gameState.IsGameOver != 0)
            return;

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var spawnerRW = SystemAPI.GetSingletonRW<WaveSpawner>();
        var spawner = spawnerRW.ValueRW;

        var dt = SystemAPI.Time.DeltaTime;

        if (spawner.State == StateBreak)
        {
            spawner.BreakTimer -= dt;

            if (spawner.BreakTimer <= 0f)
            {
                StartNextWave(ref spawner);
            }

            spawnerRW.ValueRW = spawner;
            return;
        }

        spawner.Timer -= dt;

        if (spawner.Timer > 0f)
        {
            spawnerRW.ValueRW = spawner;
            return;
        }

        int remaining = spawner.ZombiesToSpawn - spawner.ZombiesSpawned;
        if (remaining <= 0)
        {
            if (spawner.ZombiesAlive <= 0)
            {
                spawner.State = StateBreak;
                spawner.BreakTimer = math.max(0.1f, spawner.BreakDuration);
            }

            spawnerRW.ValueRW = spawner;
            return;
        }

        int burstCount = 1;

        if (spawner.DebugOverrideBurstCount > 0)
            burstCount = spawner.DebugOverrideBurstCount;

        burstCount = math.min(burstCount, remaining);

        for (int i = 0; i < burstCount; i++)
        {
            var spawnPos = PickSpawnPosition(cfg, spawner.SpawnSide, spawner.Wave, spawner.ZombiesSpawned + i);

            var cmd = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(cmd, new CmdSpawnZombie
            {
                Position = spawnPos
            });
        }

        spawner.ZombiesSpawned += burstCount;
        spawner.ZombiesAlive += burstCount;
        spawner.Timer = math.max(0.05f, spawner.SpawnInterval);

        spawnerRW.ValueRW = spawner;
    }

    static void StartNextWave(ref WaveSpawner spawner)
    {
        spawner.Wave += 1;
        spawner.State = StateSpawning;
        spawner.Timer = 0f;
        spawner.ZombiesSpawned = 0;

        int defaultCount = 6 + (spawner.Wave - 1) * 3;
        spawner.ZombiesToSpawn = spawner.DebugOverrideSpawnCount > 0
            ? spawner.DebugOverrideSpawnCount
            : defaultCount;

        // 0:left, 1:right, 2:bottom, 3:top
        spawner.SpawnSide = spawner.Wave % 4;
    }

    static float3 PickSpawnPosition(GridConfig cfg, int spawnSide, int wave, int seedOffset)
    {
        uint seed = (uint)(wave * 73856093) ^ (uint)(seedOffset * 19349663) ^ 0x9E3779B9u;
        var random = new Unity.Mathematics.Random(math.max(1u, seed));

        int2 cell;

        switch (spawnSide)
        {
            case 0:
                cell = new int2(0, random.NextInt(0, cfg.Size.y));
                break;

            case 1:
                cell = new int2(cfg.Size.x - 1, random.NextInt(0, cfg.Size.y));
                break;

            case 2:
                cell = new int2(random.NextInt(0, cfg.Size.x), 0);
                break;

            case 3:
                cell = new int2(random.NextInt(0, cfg.Size.x), cfg.Size.y - 1);
                break;

            default:
                {
                    int side = random.NextInt(0, 4);

                    switch (side)
                    {
                        case 0:
                            cell = new int2(0, random.NextInt(0, cfg.Size.y));
                            break;
                        case 1:
                            cell = new int2(cfg.Size.x - 1, random.NextInt(0, cfg.Size.y));
                            break;
                        case 2:
                            cell = new int2(random.NextInt(0, cfg.Size.x), 0);
                            break;
                        default:
                            cell = new int2(random.NextInt(0, cfg.Size.x), cfg.Size.y - 1);
                            break;
                    }

                    break;
                }
        }

        return IsoGridUtility.GridToWorld(cfg, cell);
    }
}