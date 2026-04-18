using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(ZombieMoveSystem))]
public partial struct ZombieNextCellClaimClearSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ZombieNextCellClaimState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        var claimStateRW = SystemAPI.GetSingletonRW<ZombieNextCellClaimState>();
        claimStateRW.ValueRW.Map.Clear();
    }
}