using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
//using Unity.Collections;
using Unity.Jobs;
//using Unity.Physics;
//using Collider = Unity.Physics.Collider;
//using SphereCollider = Unity.Physics.SphereCollider;
using Unity.Burst;
//using UnityEngine;
//using Unity.Collections.LowLevel.Unsafe;

[UpdateAfter(typeof(FindAccelerationSystem))]
[UpdateAfter(typeof(ObstacleAvoidSystem))]
public unsafe class MovementSystem : SystemBase
{
    //private EndSimulationEntityCommandBufferSystem m_EndSimulationEcbSystem;
    //private float3[] directions;
    //const int numViewDirections = 300;

    //protected override void OnStartRunning()
    //{
    //    //m_EndSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    //    //directions = new float3[numViewDirections];
    //    //float goldenRatio = (1 + math.sqrt(5)) / 2;
    //    //float angleIncrement = math.PI * 2 * goldenRatio;

    //    //for (int i = 0; i < numViewDirections; i++)
    //    //{
    //    //    float t = (float)i / numViewDirections;
    //    //    float inclination = math.acos(1 - 2 * t);
    //    //    float azimuth = angleIncrement * i;

    //    //    float x = math.sin(inclination) * math.cos(azimuth);
    //    //    float y = math.sin(inclination) * math.sin(azimuth);
    //    //    float z = math.cos(inclination);
    //    //    //if(i < (int)(numViewDirections * 0.9f))
    //    //    directions[i] = new float3(x, y, z);
    //    //}

    //    //JobHandle One = Entities.ForEach((ref moveData mData, in BoidData bData, in Rotation rot, in Entity entity) =>
    //    //{
    //    //    float3 normalizedDir = math.forward(rot.Value);
    //    //    float speed = (bData.minSpeed + bData.maxSpeed) / 2;
    //    //    mData.velocity = normalizedDir * speed;
    //    //}).ScheduleParallel(this.Dependency);

    //    //base.OnStartRunning();

    //    //One.Complete();
    //}

    [BurstCompile]
    protected override void OnUpdate()
    {
        float deltaTime = Time.DeltaTime;

        //var quadrantMultiHashMap = QuadrantSystem.quadrantMultiHashMap;
        //var obstacleVectors = boidObstacleVectors.directions;

        //float3 alignHeading = float3.zero;
        //float3 cohesionHeading = float3.zero;
        //float3 separationHeading = float3.zero;

        //BlobAssetReference<Collider> Collider = CreateSphereCollider(new float3(0,0,-0.2f), 0.5f);

        //var entityCount = GetEntityQuery(typeof(BoidData)).CalculateEntityCount();
        //NativeArray<float3> headingAccelerations = new NativeArray<float3>(entityCount, Allocator.TempJob);
        //NativeArray<float3> obstacleAccelerations = new NativeArray<float3>(entityCount, Allocator.TempJob);

        //NativeArray<ColliderCastInput> ccInputs = new NativeArray<ColliderCastInput>(entityCount, Allocator.TempJob);
        //NativeArray<ColliderCastHit> ccHits = new NativeArray<ColliderCastHit>(entityCount, Allocator.TempJob);
        //PhysicsWorld world = World.GetExistingSystem<Unity.Physics.Systems.BuildPhysicsWorld>().PhysicsWorld;

        //var ecb = m_EndSimulationEcbSystem.CreateCommandBuffer().ToConcurrent();

        //Entities.WithReadOnly(quadrantMultiHashMap).ForEach((ref BoidData bData, in moveData mData, in Translation pos, in Rotation rot, in Entity entity, in int entityInQueryIndex) =>
        //{
        //    ccInputs[entityInQueryIndex] = new ColliderCastInput
        //    {
        //        Collider = (Collider*)Collider.GetUnsafePtr(),
        //        Orientation = rot.Value,
        //        Start = pos.Value,
        //        End = pos.Value + (math.forward(rot.Value)*5)
        //    };

        //    var alignHelp = new AlignmentHelper();
        //    alignHelp.avgHeadings(quadrantMultiHashMap, QuadrantSystem.GetPositionHMKey(pos.Value), entity, in pos.Value, in bData.sizeRadius, out alignHeading, out cohesionHeading, out separationHeading);

        //    bData.acceleration = float3.zero;

        //    if (!alignHeading.Equals(float3.zero))
        //    {
        //        bData.acceleration += math.clamp(math.normalizesafe(alignHeading) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * bData.alignmentWeight;
        //    }
        //    if (!cohesionHeading.Equals(float3.zero))
        //    {
        //        bData.acceleration += math.clamp(math.normalizesafe(cohesionHeading - pos.Value) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * bData.cohesionWeight;
        //    }
        //    if (!separationHeading.Equals(float3.zero))
        //    {
        //        bData.acceleration += math.clamp(math.normalizesafe(separationHeading) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * bData.separationWeight;
        //    }

        //}).ScheduleParallel(this.Dependency).Complete();

        //ScheduleBatchColliderCast(in world, in ccInputs, ref ccHits).Complete();

        //Entities.ForEach((Entity entity, in int entityInQueryIndex, in BoidData bData, in moveData mData) =>
        //{
        //    if (!ccHits[entityInQueryIndex].Entity.Equals(Entity.Null))
        //    {
        //        Debug.Log("wall hit");
        //        ecb.AddComponent(entityInQueryIndex, entity, new ComponentType(typeof(avoidObstacleTag), ComponentType.AccessMode.ReadOnly));
        //    }
        //}).WithoutBurst().ScheduleParallel(Dependency).Complete();

        //m_EndSimulationEcbSystem.AddJobHandleForProducer(this.Dependency);
        //ccInputs.Dispose();
        //ccHits.Dispose();

        //var obsEntityCount = GetEntityQuery(new ComponentType[] { typeof(BoidData), typeof(avoidObstacleTag) }).CalculateEntityCount();
        //NativeArray<ColliderCastInput> ccInputsObs = new NativeArray<ColliderCastInput>(obsEntityCount, Allocator.TempJob);
        //NativeArray<float3> ccFreePositions = new NativeArray<float3>(obsEntityCount, Allocator.TempJob);
        //Entities.ForEach((in int entityInQueryIndex, in BoidData bData, in Translation pos, in Rotation rot, in avoidObstacleTag tag) =>
        //{
        //    ccInputsObs[entityInQueryIndex] = new ColliderCastInput
        //    {
        //        Collider = (Collider*)Collider.GetUnsafePtr(),
        //        Orientation = rot.Value,
        //        Start = pos.Value,
        //        End = pos.Value
        //    };
        //}).ScheduleParallel(this.Dependency).Complete();

        //ScheduleBatchColliderCast(world, ccInputsObs, ref ccFreePositions).Complete();

        //Entities.ForEach((Entity entity, ref BoidData bData, in Translation pos, in Rotation rot, in moveData mData, in int entityInQueryIndex, in avoidObstacleTag tag) =>
        //{
        //    if(math.all(ccFreePositions[entityInQueryIndex] == float3.zero))
        //    {
        //        ccFreePositions[entityInQueryIndex] = math.forward(rot.Value);
        //        Debug.Log("no free position");
        //    }
        //    Debug.Log("free position");
        //    bData.acceleration += math.clamp(math.normalizesafe(ccFreePositions[entityInQueryIndex]) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * 10f;
        //    ecb.RemoveComponent(entityInQueryIndex, entity, new ComponentType(typeof(avoidObstacleTag), ComponentType.AccessMode.ReadOnly));
        //}).WithoutBurst().ScheduleParallel(this.Dependency).Complete();

        //ccInputsObs.Dispose();
        //ccFreePositions.Dispose();

        // Actual adjustments of position based on previous two jobs
        Entities.ForEach((ref Translation pos, ref moveData mData, ref Rotation rot, ref accelerationHeadings accel, ref accelerationObstacles obs, in BoidData bData) =>
        {
            float3 heading = (accel.acceleration + obs.acceleration) * deltaTime;
            //float3 heading = obs.acceleration * deltaTime;
            mData.velocity += heading;
            float speed = math.sqrt((mData.velocity.x * mData.velocity.x) + (mData.velocity.y * mData.velocity.y) + (mData.velocity.z * mData.velocity.z));
            float3 direction = mData.velocity / speed;
            speed = math.clamp(speed, bData.minSpeed, bData.maxSpeed);
            mData.velocity = direction * speed;

            rot.Value = quaternion.LookRotationSafe(direction, math.up());
            pos.Value += mData.velocity * deltaTime;
        }).ScheduleParallel(this.Dependency).Complete();
    }


    //private struct AlignmentHelper
    //{
    //    public void avgHeadings(NativeMultiHashMap<float3, QuadData> quadrantMap, float3 hashKey, Entity entity, in float3 entPos, in float avoidRadius, out float3 alignH, out float3 cohesionH, out float3 separationH)
    //    {
    //        QuadData item;
    //        NativeMultiHashMapIterator<float3> iter;
    //        int count = 0;
    //        alignH = float3.zero;
    //        cohesionH = float3.zero;
    //        separationH = float3.zero;
    //        int cellSize = 3;

    //        for(int x=-1; x<2; x++)
    //        {
    //            for(int y=-1; y<2; y++)
    //            {
    //                for(int z=-1; z<2; z++)
    //                {
    //                    //Debug.Log("Quadrant: " + (hashKey.x + x, hashKey.y + y, hashKey.z + z));
                        
    //                    if (quadrantMap.TryGetFirstValue(new float3(hashKey.x+(x*cellSize), hashKey.y+(y*cellSize), hashKey.z+(z*cellSize)), out item, out iter))
    //                    {
    //                        //Debug.Log("boid cell: " + new float3(hashKey.x, hashKey.y, hashKey.z) + "\nexternal boid cell: " + new float3(hashKey.x + x, hashKey.y + y, hashKey.z + z));
    //                        do
    //                        {
    //                            if (!item.entity.Equals(entity))
    //                            {
    //                                float3 offset = entPos - item.position;
    //                                float dst = math.sqrt(math.pow(offset.x, 2) + math.pow(offset.y, 2) + math.pow(offset.z, 2));
    //                                //float dst = math.pow(offset.x, 2) + math.pow(offset.y, 2) + math.pow(offset.z, 2);

    //                                alignH += math.forward(item.rot);
    //                                cohesionH += item.position;

    //                                if(dst < avoidRadius)
    //                                {
    //                                    // distance formula is the magnitude of the offset vector
    //                                    separationH += offset / dst;
    //                                }

    //                                count++;
    //                            }

    //                        } while (quadrantMap.TryGetNextValue(out item, ref iter));
    //                    }

    //                }
    //            }
    //        }
    //        if (count != 0)
    //        {
    //            cohesionH /= count;
    //        }
    //    }
    //}

    //private BlobAssetReference<Collider> CreateSphereCollider(in float3 center, in float radius)
    //{
    //    return SphereCollider.Create(new SphereGeometry
    //        {
    //            Center = center,
    //            Radius = radius
    //        }
    //    );
    //}

    //[BurstCompile]
    //public struct ColliderCastJob : IJobParallelFor
    //{
    //    [ReadOnly] public NativeArray<ColliderCastInput> inputs;
    //    [ReadOnly] public PhysicsWorld World;
    //    public NativeArray<ColliderCastHit> results;

    //    public void Execute(int index)
    //    {
    //        if (World.CastCollider(inputs[index], out ColliderCastHit hit))
    //        {
    //            results[index] = hit;
    //        } 
    //    }
    //}

    //[BurstCompile]
    //public struct MultiColliderCastJob : IJobParallelFor
    //{
    //    [ReadOnly] public NativeArray<ColliderCastInput> inputs;
    //    [ReadOnly] public PhysicsWorld World;
    //    //[ReadOnly] public float3[] directions;
    //    public NativeArray<float3> results;

    //    public void Execute(int index)
    //    {
    //        float goldenRatio = (1 + math.sqrt(5)) / 2;
    //        float angleIncrement = math.PI * 2 * goldenRatio;

    //        results[index] = float3.zero;

    //        for (int i = 0; i < 300; i++)
    //        {
    //            float t = (float)i / 300;
    //            float inclination = math.acos(1 - 2 * t);
    //            float azimuth = angleIncrement * i;

    //            float3 direction = new float3(math.sin(inclination) * math.cos(azimuth), math.sin(inclination) * math.sin(azimuth), math.cos(inclination));

    //            ColliderCastInput colliderCastInput = new ColliderCastInput
    //            {
    //                Collider = inputs[index].Collider,
    //                Orientation = inputs[index].Orientation,
    //                Start = inputs[index].Start,
    //                End = inputs[index].Start + (direction * 5)
    //            };

    //            World.CastCollider(colliderCastInput, out ColliderCastHit hit);
    //            if (hit.Entity.Equals(Entity.Null))
    //            {
    //                results[index] = direction;
    //                break;
    //            }
    //        }
            
    //    }
    //}

    //public static JobHandle ScheduleBatchColliderCast(in PhysicsWorld world, in NativeArray<ColliderCastInput> inputs, ref NativeArray<ColliderCastHit> results)
    //{
    //    JobHandle ccj = new ColliderCastJob
    //    {
    //        inputs = inputs,
    //        World = world,
    //        results = results
    //    }.Schedule(inputs.Length, 4);

    //    return ccj;
    //}

    //public static JobHandle ScheduleBatchColliderCast(in PhysicsWorld world, in NativeArray<ColliderCastInput> inputs,  ref NativeArray<float3> results)
    //{
    //    JobHandle ccj = new MultiColliderCastJob
    //    {
    //        inputs = inputs,
    //        World = world,
    //        //directions = directions,
    //        results = results
    //    }.Schedule(inputs.Length, 4);

    //    return ccj;
    //}

    //public static void SingleColliderCast(in PhysicsWorld world, in ColliderCastInput input, ref ColliderCastHit result)
    //{
    //    var ccInput = new NativeArray<ColliderCastInput>(1, Allocator.TempJob);
    //    var ccResult = new NativeArray<ColliderCastHit>(1, Allocator.TempJob);
    //    ccInput[0] = input;
    //    JobHandle handle = ScheduleBatchColliderCast(in world, in ccInput, ref ccResult);
    //    handle.Complete();
    //    result = ccResult[0];
    //    ccInput.Dispose();
    //    ccResult.Dispose();
    //}

}
