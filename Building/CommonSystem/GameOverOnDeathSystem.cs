using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageEventSystem))]
public partial struct GameOverOnDeathSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var gameStateRW = SystemAPI.GetSingletonRW<GameState>();
        if (gameStateRW.ValueRO.IsGameOver != 0)
            return;

        foreach (var hp in SystemAPI.Query<RefRO<Health>>().WithAll<GameOverOnDeath>())
        {
            if (hp.ValueRO.Value > 0)
                continue;

            gameStateRW.ValueRW.IsGameOver = 1;
            break;
        }
    }
}