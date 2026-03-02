using Unity.Entities;
using Unity.Collections;

public partial class WallIndexBootstrapSystem : SystemBase
{
    protected override void OnCreate()
    {
        // 이미 있으면 만들지 않음
        if (SystemAPI.HasSingleton<WallIndexState>())
            return;

        var e = EntityManager.CreateEntity(typeof(WallIndex), typeof(WallIndexState));

        EntityManager.SetComponentData(e, new WallIndexState
        {
            // 초반 용량은 대충 (그리드 크기/예상 벽 수) 정도로. 4096이면 넉넉
            Map = new NativeParallelHashMap<int, Entity>(4096, Allocator.Persistent)
        });
    }

    protected override void OnDestroy()
    {
        if (!SystemAPI.HasSingleton<WallIndexState>())
            return;

        var s = SystemAPI.GetSingleton<WallIndexState>();
        if (s.Map.IsCreated)
            s.Map.Dispose();
    }

    protected override void OnUpdate() { }
}