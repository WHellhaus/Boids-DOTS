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
using Unity.Collections.LowLevel.Unsafe;

[UpdateAfter(typeof(QuadrantSystem))]
public unsafe class MovementSystem : SystemBase
{
    private World defaultWorld;
    private EntityManager entityManager;

    protected override void OnStartRunning()
    {
        defaultWorld = World.DefaultGameObjectInjectionWorld;
        entityManager = defaultWorld.EntityManager;

        JobHandle One = Entities.ForEach((ref moveData mData, in BoidData bData, in Rotation rot, in Entity entity) =>
        {
            float3 normalizedDir = math.forward(rot.Value);
            float speed = (bData.minSpeed + bData.maxSpeed) / 2;
            mData.velocity = normalizedDir * speed;
        }).ScheduleParallel(this.Dependency);

        base.OnStartRunning();

        One.Complete();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        float deltaTime = Time.DeltaTime;

        var quadrantMultiHashMap = QuadrantSystem.quadrantMultiHashMap;
        //var obstacleVectors = boidObstacleVectors.directions;

        float3 alignHeading = float3.zero;
        float3 cohesionHeading = float3.zero;
        float3 separationHeading = float3.zero;

        BlobAssetReference<Collider> Collider = CreateSphereCollider(new float3(0,0,-0.2f), 0.5f);

        var entityCount = GetEntityQuery(typeof(BoidData)).CalculateEntityCount();
        NativeArray<float3> headingAccelerations = new NativeArray<float3>(entityCount, Allocator.TempJob);
        NativeArray<float3> obstacleAccelerations = new NativeArray<float3>(entityCount, Allocator.TempJob);

        NativeArray<ColliderCastInput> ccInputs = new NativeArray<ColliderCastInput>(entityCount, Allocator.TempJob);
        NativeArray<ColliderCastHit> ccHits = new NativeArray<ColliderCastHit>(entityCount, Allocator.TempJob);
        PhysicsWorld world = World.GetExistingSystem<Unity.Physics.Systems.BuildPhysicsWorld>().PhysicsWorld;

        Entities.WithReadOnly(quadrantMultiHashMap).ForEach((in BoidData bData, in moveData mData, in Translation pos, in Rotation rot, in Entity entity, in int entityInQueryIndex) =>
        {
            ccInputs[entityInQueryIndex] = new ColliderCastInput
            {
                Collider = (Collider*)Collider.GetUnsafePtr(),
                Orientation = rot.Value,
                Start = pos.Value,
                End = pos.Value + (math.forward(rot.Value)*5)
            };

            var alignHelp = new AlignmentHelper();
            alignHelp.avgHeadings(quadrantMultiHashMap, QuadrantSystem.GetPositionHMKey(pos.Value), entity, in pos.Value, in bData.sizeRadius, out alignHeading, out cohesionHeading, out separationHeading);

            headingAccelerations[entityInQueryIndex] = float3.zero;

            if (!alignHeading.Equals(float3.zero))
            {
                headingAccelerations[entityInQueryIndex] += math.clamp(math.normalizesafe(alignHeading) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * bData.alignmentWeight;
            }
            if (!cohesionHeading.Equals(float3.zero))
            {
                headingAccelerations[entityInQueryIndex] += math.clamp(math.normalizesafe(cohesionHeading - pos.Value) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * bData.cohesionWeight;
            }
            if (!separationHeading.Equals(float3.zero))
            {
                headingAccelerations[entityInQueryIndex] += math.clamp(math.normalizesafe(separationHeading) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * bData.separationWeight;
            }

        }).ScheduleParallel(this.Dependency).Complete();

        ScheduleBatchColliderCast(in world, in ccInputs, ref ccHits).Complete();

        Entities.ForEach((in Translation pos, in Rotation rot, in ObstacleVectors obsVectors, in int entityInQueryIndex, in BoidData bData, in moveData mData) =>
        {
            obstacleAccelerations[entityInQueryIndex] = float3.zero;
            if (!ccHits[entityInQueryIndex].Entity.Equals(Entity.Null))
            {
                Debug.Log("hit wall");
                ref BlobArray<float3> vectors = ref obsVectors.blobAsset.Value.vectorsArray;
                Debug.Log("wall pos: " + ccHits[entityInQueryIndex].Position);
                for (int i = 50; i < vectors.Length; i++)
                {
                    ColliderCastInput input = new ColliderCastInput
                    {
                        Collider = (Collider*)Collider.GetUnsafePtr(),
                        Orientation = rot.Value,
                        Start = pos.Value,
                        End = pos.Value + (vectors[i] * 5)
                    };
                    ColliderCastHit result = new ColliderCastHit { };
                    world.CastCollider(input, out ColliderCastHit hit);
                    result = hit;
                    if(result.Entity.Equals(Entity.Null)){
                        Debug.Log("vector pos: " + (pos.Value + (vectors[i] * 5)));
                        obstacleAccelerations[entityInQueryIndex] = math.clamp(math.normalizesafe(pos.Value + vectors[i]) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * 15f;
                        //Debug.Log(obstacleAccelerations[entityInQueryIndex]);
                        break;    
                    }
                }
            }
        }).Run();

        // Actual adjustments of position based on previous two jobs
        Entities.ForEach((ref Translation pos, ref moveData mData, ref Rotation rot, in BoidData bData, in int entityInQueryIndex) =>
        {
            float3 heading = (headingAccelerations[entityInQueryIndex] + obstacleAccelerations[entityInQueryIndex]) * deltaTime;
            mData.velocity += heading;
            float speed = math.sqrt((mData.velocity.x * mData.velocity.x) + (mData.velocity.y * mData.velocity.y) + (mData.velocity.z * mData.velocity.z));
            float3 direction = mData.velocity / speed;
            speed = math.clamp(speed, bData.minSpeed, bData.maxSpeed);
            mData.velocity = direction * speed;

            rot.Value = quaternion.LookRotationSafe(direction, math.up());
            pos.Value += mData.velocity * deltaTime;
        }).WithDeallocateOnJobCompletion(headingAccelerations).ScheduleParallel(this.Dependency).Complete();
        obstacleAccelerations.Dispose();
        ccHits.Dispose();
        ccInputs.Dispose();
    }


    private struct AlignmentHelper
    {
        public void avgHeadings(NativeMultiHashMap<float3, QuadData> quadrantMap, float3 hashKey, Entity entity, in float3 entPos, in float avoidRadius, out float3 alignH, out float3 cohesionH, out float3 separationH)
        {
            QuadData item;
            NativeMultiHashMapIterator<float3> iter;
            int count = 0;
            alignH = float3.zero;
            cohesionH = float3.zero;
            separationH = float3.zero;
            int cellSize = 3;

            for(int x=-1; x<2; x++)
            {
                for(int y=-1; y<2; y++)
                {
                    for(int z=-1; z<2; z++)
                    {
                        //Debug.Log("Quadrant: " + (hashKey.x + x, hashKey.y + y, hashKey.z + z));
                        
                        if (quadrantMap.TryGetFirstValue(new float3(hashKey.x+(x*cellSize), hashKey.y+(y*cellSize), hashKey.z+(z*cellSize)), out item, out iter))
                        {
                            //Debug.Log("boid cell: " + new float3(hashKey.x, hashKey.y, hashKey.z) + "\nexternal boid cell: " + new float3(hashKey.x + x, hashKey.y + y, hashKey.z + z));
                            do
                            {
                                if (!item.entity.Equals(entity))
                                {
                                    float3 offset = entPos - item.position;
                                    float dst = math.sqrt(math.pow(offset.x, 2) + math.pow(offset.y, 2) + math.pow(offset.z, 2));
                                    //float dst = math.pow(offset.x, 2) + math.pow(offset.y, 2) + math.pow(offset.z, 2);

                                    alignH += math.forward(item.rot);
                                    cohesionH += item.position;

                                    if(dst < avoidRadius)
                                    {
                                        // distance formula is the magnitude of the offset vector
                                        separationH += offset / dst;
                                    }

                                    count++;
                                }

                            } while (quadrantMap.TryGetNextValue(out item, ref iter));
                        }

                    }
                }
            }
            if (count != 0)
            {
                cohesionH /= count;
            }
        }
    }

    private BlobAssetReference<Collider> CreateSphereCollider(in float3 center, in float radius)
    {
        return SphereCollider.Create(new SphereGeometry
            {
                Center = center,
                Radius = radius
            }
        );
    }

    [BurstCompile]
    public struct ColliderCastJob : IJobParallelFor
    {
        //[NativeDisableUnsafePtrRestriction] public Collider* Collider;
        //public quaternion Orientation;
        //public float3 Start;
        //public float3 End;
        //public NativeArray<ColliderCastHit> ColliderCastHits;
        [ReadOnly] public NativeArray<ColliderCastInput> inputs;
        [ReadOnly] public PhysicsWorld World;
        public NativeArray<ColliderCastHit> results;


        public void Execute(int index)
        {
            //ColliderCastInput colliderCastInput = new ColliderCastInput
            //{
            //    Collider = Collider,
            //    Orientation = Orientation,
            //    Start = Start,
            //    End = End
            //};

            if (World.CastCollider(inputs[index], out ColliderCastHit hit))
            {
                results[index] = hit;
            } 
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

    public static void SingleColliderCast(in PhysicsWorld world, in ColliderCastInput input, ref ColliderCastHit result)
    {
        var ccInput = new NativeArray<ColliderCastInput>(1, Allocator.TempJob);
        var ccResult = new NativeArray<ColliderCastHit>(1, Allocator.TempJob);
        ccInput[0] = input;
        JobHandle handle = ScheduleBatchColliderCast(in world, in ccInput, ref ccResult);
        handle.Complete();
        result = ccResult[0];
        ccInput.Dispose();
        ccResult.Dispose();
    }

}
