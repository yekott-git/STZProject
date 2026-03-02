using Unity.Entities;
using Unity.Mathematics;

public partial struct BuildSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<GridConfig>()) return;

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridConfig>();

        var occBuf = SystemAPI.GetBuffer<OccCell>(gridEntity);
        int width = cfg.Size.x;

        var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

        foreach (var (cmd, cmdEntity) in SystemAPI.Query<RefRO<CmdBuild>>().WithEntityAccess())
        {
            int2 c = cmd.ValueRO.Cell;
            if (!IsoGridUtility.InBounds(cfg, c))
            {
                ecb.DestroyEntity(cmdEntity);
                continue;
            }

            int idx = c.y * width + c.x;

            // 비어있으면 배치 성공
            if (occBuf[idx].Value == 0)
            {
                occBuf[idx] = new OccCell { Value = 1 };

                // TODO: 실제 Building 엔티티 생성은 다음 스텝에서
                // (BuildingType에 따른 프리팹 instantiate + GridPos 붙이기)
            }

            ecb.DestroyEntity(cmdEntity);
        }

        ecb.Playback(state.EntityManager);
    }
}