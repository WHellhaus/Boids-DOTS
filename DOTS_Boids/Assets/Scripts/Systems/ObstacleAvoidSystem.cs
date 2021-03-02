using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Jobs;
using Unity.Physics;
using Collider = Unity.Physics.Collider;
using SphereCollider = Unity.Physics.SphereCollider;
using Unity.Burst;
using UnityEngine;

[UpdateAfter(typeof(QuadrantSystem))]
[UpdateBefore(typeof(MovementSystem))]
public unsafe class ObstacleAvoidSystem : SystemBase
{
    //private EndSimulationEntityCommandBufferSystem m_EndSimulationEcbSystem;
    //const int numViewDirections = 300;

    protected override void OnStartRunning()
    {
        //m_EndSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        base.OnStartRunning();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        var filter = new CollisionFilter()
        {
            BelongsTo = (1 << 0),
            CollidesWith = (1 << 8), // all 1s, so all layers, collide with everything
            GroupIndex = 1
        };
        BlobAssetReference<Collider> Collider = CreateSphereCollider(new float3(0, 0, -0.2f), 0.5f, filter);
        //var ecb = m_EndSimulationEcbSystem.CreateCommandBuffer().ToConcurrent();

        var entityCount = GetEntityQuery(typeof(BoidData)).CalculateEntityCount();
        NativeArray<ColliderCastInput> ccInputs = new NativeArray<ColliderCastInput>(entityCount, Allocator.TempJob);
        NativeArray<ColliderCastHit> ccHits = new NativeArray<ColliderCastHit>(entityCount, Allocator.TempJob);
        PhysicsWorld world = World.GetExistingSystem<Unity.Physics.Systems.BuildPhysicsWorld>().PhysicsWorld;

        // Set up collider cast inputs for all boids to find if they are about to collide
        Entities.ForEach((ref accelerationObstacles obs, in Translation pos, in Rotation rot, in int entityInQueryIndex) =>
        {
            obs.acceleration = float3.zero;
            ccInputs[entityInQueryIndex] = new ColliderCastInput
            {
                Collider = (Collider*)Collider.GetUnsafePtr(),
                Orientation = rot.Value,
                Start = pos.Value,
                End = pos.Value + (math.forward(rot.Value) * 10f)
            };
        }).ScheduleParallel(Dependency).Complete();

        // actually cast colliders
        ScheduleBatchColliderCast(in world, in ccInputs, ref ccHits).Complete();

        // check if colliders hit anything; if they did add an avoidObstacleTag component to them
        Entities.WithReadOnly(ccHits).ForEach((ref accelerationObstacles accel, in int entityInQueryIndex, in BoidData bData, in moveData mData, in Translation pos, in Rotation rot) =>
        {
            if (!ccHits[entityInQueryIndex].Entity.Equals(Entity.Null))
            {
                //ecb.AddComponent<avoidObstacleTag>(entityInQueryIndex, entity);
                //Debug.Log("hit");
                float3 normal = ccHits[entityInQueryIndex].SurfaceNormal;
                float3 direction = math.forward(rot.Value);
                float3 reflection = direction - 2 * (math.dot(direction, normal)) * normal;
                accel.acceleration += math.clamp(math.normalizesafe(reflection) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * 15f;
            }
        }).ScheduleParallel(Dependency).Complete();
        //m_EndSimulationEcbSystem.AddJobHandleForProducer(Dependency);

        // dispose of native arrays for reuse
        ccInputs.Dispose();
        ccHits.Dispose();

        //var obsEntityCount = GetEntityQuery(new ComponentType[] { typeof(BoidData), typeof(avoidObstacleTag) }).CalculateEntityCount();
        ////Debug.Log(obsEntityCount);
        //NativeArray<ColliderCastInput> ccObsInputs = new NativeArray<ColliderCastInput>(obsEntityCount * 300, Allocator.TempJob);
        //NativeArray<ColliderCastHit> ccObsHits = new NativeArray<ColliderCastHit>(obsEntityCount * 300, Allocator.TempJob);
        ////NativeArray<float3> ccFreePositions = new NativeArray<float3>(obsEntityCount, Allocator.TempJob);

        //// set up inputs for collider casts to find free position to move towards
        //Entities.ForEach((ref ObstacleVectors obsVectors, in int entityInQueryIndex, in Translation pos, in Rotation rot, in avoidObstacleTag tag) =>
        //{
        //    int index = entityInQueryIndex * 300;
        //    ref ObstacleVectorsBlobAsset directions = ref obsVectors.blobAsset.Value;
        //    float4x4 trs = float4x4.TRS(pos.Value, rot.Value, new float3(0.5f));
        //    for (int i=0; i < directions.vectorsArray.Length; i++)
        //    {
        //        //float3 endpoint = math.transform(trs, directions.vectorsArray[i]);
        //        ccObsInputs[index+i] = new ColliderCastInput
        //        {
        //            Collider = (Collider*)Collider.GetUnsafePtr(),
        //            Orientation = rot.Value,
        //            Start = pos.Value,
        //            End = math.transform(trs, directions.vectorsArray[i])*3
        //        };
        //    }
        //}).ScheduleParallel(Dependency).Complete();

        //ScheduleBatchColliderCast(world, ccObsInputs, ref ccObsHits).Complete();

        //Entities.WithReadOnly(ccObsHits).WithReadOnly(ccObsInputs).ForEach((Entity entity, ref accelerationObstacles obs, in BoidData bData, in Translation pos, in Rotation rot, in moveData mData, in int entityInQueryIndex, in avoidObstacleTag tag) =>
        //{
        //    int index = entityInQueryIndex * 300;
        //    float3 moveTowards = float3.zero;
        //    for (int i = 0; i < 300; i++)
        //    {
        //        //Debug.Log(ccObsHits[index + i].Entity.Equals(Entity.Null) + ": " + (ccObsInputs[index + i].End / 5));
        //        if (ccObsHits[index+i].Entity.Equals(Entity.Null))
        //        {
        //            moveTowards = ccObsInputs[index + i].End;     
        //            break;
        //        }
        //    }
        //    obs.acceleration += math.clamp(math.normalizesafe(moveTowards) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * 10f;
        //    ecb.RemoveComponent<avoidObstacleTag>(entityInQueryIndex, entity);
        //}).ScheduleParallel(Dependency).Complete();
        //m_EndSimulationEcbSystem.AddJobHandleForProducer(Dependency);

        //ccObsInputs.Dispose();
        //ccObsHits.Dispose();
        //ccFreePositions.Dispose();
    }

    private BlobAssetReference<Collider> CreateSphereCollider(in float3 center, in float radius, in CollisionFilter filter)
    {
        return SphereCollider.Create(new SphereGeometry
        {
            Center = center,
            Radius = radius
        }, filter);
    }

    [BurstCompile]
    public struct ColliderCastJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ColliderCastInput> inputs;
        [ReadOnly] public PhysicsWorld World;
        public NativeArray<ColliderCastHit> results;

        public void Execute(int index)
        {
            if (World.CastCollider(inputs[index], out ColliderCastHit hit))
            {
                results[index] = hit;
            }
        }
    }

    [BurstCompile]
    public struct MultiColliderCastJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ColliderCastInput> inputs;
        [ReadOnly] public PhysicsWorld World;
        //[ReadOnly] public float3[] directions;
        public NativeArray<float3> results;

        public void Execute(int index)
        {
            float goldenRatio = (1 + math.sqrt(5)) / 2;
            float angleIncrement = math.PI * 2 * goldenRatio;

            results[index] = float3.zero;
            float4x4 trs = float4x4.TRS(inputs[index].Start, inputs[index].Orientation, new float3(1));

            for (int i = 0; i < 300; i++)
            {
                float t = (float)i / 300f;
                float inclination = math.acos(1 - 2 * t);
                float azimuth = angleIncrement * i;

                float3 direction = new float3(math.sin(inclination) * math.cos(azimuth), math.sin(inclination) * math.sin(azimuth), math.cos(inclination));
                float3 point = math.transform(trs, direction*5);
                //Debug.Log(i + "direction: " + direction + "pnt: " + point + " pos: " + inputs[index].Start);

                ColliderCastInput colliderCastInput = new ColliderCastInput
                {
                    Collider = inputs[index].Collider,
                    Orientation = inputs[index].Orientation,
                    Start = inputs[index].Start,
                    End = inputs[index].Start + point
                };

                World.CastCollider(colliderCastInput, out ColliderCastHit hit);
                if (math.all(hit.Position == float3.zero))
                {
                    //Debug.Log("empty point found");
                    results[index] = math.transform(trs, direction);
                    break;
                }
            }
            //Debug.Log("no direction found");
        }
    }

    public static JobHandle ScheduleBatchColliderCast(in PhysicsWorld world, in NativeArray<ColliderCastInput> inputs, ref NativeArray<ColliderCastHit> results)
    {
        JobHandle ccj = new ColliderCastJob
        {
            inputs = inputs,
            World = world,
            results = results
        }.Schedule(inputs.Length, 4);

        return ccj;
    }

    public static JobHandle ScheduleBatchColliderCast(in PhysicsWorld world, in NativeArray<ColliderCastInput> inputs, ref NativeArray<float3> results)
    {
        JobHandle ccj = new MultiColliderCastJob
        {
            inputs = inputs,
            World = world,
            //directions = directions,
            results = results
        }.Schedule(inputs.Length, 4);

        return ccj;
    }
}
