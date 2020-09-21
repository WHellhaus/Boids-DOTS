using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Burst;

public struct QuadData
{
    public Entity entity;
    public quaternion rot;
    public float3 position;
}

[UpdateBefore(typeof(MovementSystem))]
public class QuadrantSystem : SystemBase
{
    public static NativeMultiHashMap<float3, QuadData> quadrantMultiHashMap;

    public readonly static int cellSize = 3;

    public static float3 GetPositionHMKey(float3 position)
    {
        return new float3(math.floor(position.x / cellSize), math.floor(position.y / cellSize), math.floor(position.z / cellSize));
    }

    public static void DebugDrawQuadrant(float3 position)
    {
        float3 lowerLeft = GetPositionHMKey(position) * cellSize;
        Debug.DrawLine(lowerLeft, lowerLeft + new float3(1, 0, 0) * cellSize);
        Debug.DrawLine(lowerLeft, lowerLeft + new float3(0, 1, 0) * cellSize);
        Debug.DrawLine(lowerLeft + new float3(1, 0, 0) * cellSize, lowerLeft + new float3(1, 1, 0) * cellSize);
        Debug.DrawLine(lowerLeft + new float3(0, 1, 0) * cellSize, lowerLeft + new float3(1, 1, 0) * cellSize);
        Debug.DrawLine(lowerLeft, lowerLeft + new float3(0, 0, 1) * cellSize);
        Debug.DrawLine(lowerLeft + new float3(0, 1, 0) * cellSize, lowerLeft + new float3(0, 1, 1) * cellSize);
        Debug.DrawLine(lowerLeft + new float3(1, 0, 0) * cellSize, lowerLeft + new float3(1, 0, 1) * cellSize);
        Debug.DrawLine(lowerLeft + new float3(1, 1, 0) * cellSize, lowerLeft + new float3(1, 1, 1) * cellSize);
        Debug.DrawLine(lowerLeft + new float3(0, 0, 1) * cellSize, lowerLeft + new float3(1, 0, 1) * cellSize);
        Debug.DrawLine(lowerLeft + new float3(0, 0, 1) * cellSize, lowerLeft + new float3(0, 1, 1) * cellSize);
        Debug.DrawLine(lowerLeft + new float3(1, 1, 1) * cellSize, lowerLeft + new float3(0, 1, 1) * cellSize);
        Debug.DrawLine(lowerLeft + new float3(1, 1, 1) * cellSize, lowerLeft + new float3(1, 0, 1) * cellSize);
    }

    protected override void OnCreate()
    {
        quadrantMultiHashMap = new NativeMultiHashMap<float3, QuadData>(0, Allocator.Persistent);
        base.OnCreate();
    }

    protected override void OnDestroy()
    {
        quadrantMultiHashMap.Dispose();
        base.OnDestroy();
    }
    
    [BurstCompile]
    protected override void OnUpdate()
    {
        EntityQuery entityQuery = GetEntityQuery(typeof(BoidData));

        var quadMultiHashMap = QuadrantSystem.quadrantMultiHashMap;
        quadMultiHashMap.Clear();
        if(entityQuery.CalculateEntityCount() > quadMultiHashMap.Capacity)
        {
            quadMultiHashMap.Capacity = entityQuery.CalculateEntityCount();
        }

        Entities.ForEach((in Entity entity, in Translation pos, in Rotation rot, in BoidData bData) =>
        {
            float3 entityPosKey = GetPositionHMKey(pos.Value);
            //DebugDrawQuadrant(pos.Value);
            quadMultiHashMap.Add(entityPosKey, new QuadData
            {
                entity = entity,
                rot = rot.Value,
                position = pos.Value
            });
        }).Run();
    }
}
