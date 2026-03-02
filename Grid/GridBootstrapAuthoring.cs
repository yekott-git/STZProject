using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class GridBootstrapAuthoring : MonoBehaviour
{
    public int width = 128;
    public int height = 128;

    // 월드 유닛 기준: 2:1 비율만 지키면 됨
    public float tileW = 1.0f;
    public float tileH = 0.5f;

    public float zStep = 0.0001f;
    public Vector3 origin;

    class Baker : Baker<GridBootstrapAuthoring>
    {
        public override void Bake(GridBootstrapAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.None);

            AddComponent(e, new GridConfig
            {
                Size = new int2(a.width, a.height),
                TileW = a.tileW,
                TileH = a.tileH,
                ZStep = a.zStep,
                Origin = (float3)a.origin
            });

            AddComponent<GridOccupancy>(e);

            var buf = AddBuffer<OccCell>(e);
            buf.ResizeUninitialized(a.width * a.height);
            for (int i = 0; i < buf.Length; i++) buf[i] = new OccCell { Value = 0 };
        }
    }
}