using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using Unity.Physics;
using Unity.Burst;

[UpdateAfter(typeof(QuadrantSystem))]
public class MovementSystem : SystemBase
{
    protected override void OnStartRunning()
    {
        JobHandle One = Entities.ForEach((ref moveData mData, in BoidData bData, in Rotation rot, in Entity entity) =>
        {
            float3 normalizedDir = math.forward(rot.Value);
            float speed = (bData.minSpeed + bData.maxSpeed) / 2;
            mData.velocity = normalizedDir * speed;



        }).ScheduleParallel(this.Dependency);

        base.OnStartRunning();

        One.Complete();
    }

    protected override void OnUpdate()
    {
        float deltaTime = Time.DeltaTime;

        var quadrantMultiHashMap = QuadrantSystem.quadrantMultiHashMap;
        //var obstacleVectors = boidObstacleVectors.directions;

        float3 alignHeading = float3.zero;
        float3 cohesionHeading = float3.zero;
        float3 separationHeading = float3.zero;

        var entityCount = GetEntityQuery(typeof(BoidData)).CalculateEntityCount();
        NativeArray<float3> headingAccelerations = new NativeArray<float3>(entityCount, Allocator.TempJob);
        //NativeArray<float3> obstacleAccelerations = new NativeArray<float3>(entityCount, Allocator.TempJob);

        JobHandle One = Entities.WithReadOnly(quadrantMultiHashMap).ForEach((in BoidData bData, in moveData mData, in Translation pos, in Entity entity, in int entityInQueryIndex) =>
        {
            var alignHelp = new AlignmentHelper();
            alignHelp.avgHeadings(quadrantMultiHashMap, QuadrantSystem.GetPositionHMKey(pos.Value), entity, in pos.Value, in bData.sizeRadius, out alignHeading, out cohesionHeading, out separationHeading);

            headingAccelerations[entityInQueryIndex] = float3.zero;

            if (!alignHeading.Equals(float3.zero))
            {
                headingAccelerations[entityInQueryIndex] += math.clamp(math.normalizesafe(alignHeading) * bData.maxSpeed - mData.velocity, 0, bData.maxTurnSpeed) * bData.alignmentWeight;
            }
            if (!cohesionHeading.Equals(float3.zero))
            {
                headingAccelerations[entityInQueryIndex] += math.clamp(math.normalizesafe(cohesionHeading - pos.Value) * bData.maxSpeed - mData.velocity, 0, bData.maxTurnSpeed) * bData.cohesionWeight;
            }
            if (!separationHeading.Equals(float3.zero))
            {
                headingAccelerations[entityInQueryIndex] += math.clamp(math.normalizesafe(separationHeading) * bData.maxSpeed - mData.velocity, 0, bData.maxTurnSpeed) * bData.separationWeight;
            }

        }).ScheduleParallel(this.Dependency);

        Entities.ForEach((in Translation pos, in Rotation rot, in ObstacleVectors obsVectors, in int entityInQueryIndex, in BoidData bData) =>
        {
            for(int i=0; i<obsVectors.blobAsset.Value.vectorsArray.Length; i++)
            {
                //Debug.Log(obsVectors.blobAsset.Value.vectorsArray[i]);
            }
            
        }).ScheduleParallel();

        One.Complete();

        // Actual adjustments of position based on previous two jobs
        Entities.ForEach((ref Translation pos, ref moveData mData, ref Rotation rot, in BoidData bData, in int entityInQueryIndex) =>
        {
            mData.velocity += (headingAccelerations[entityInQueryIndex] * deltaTime);
            float speed = math.sqrt((mData.velocity.x * mData.velocity.x) + (mData.velocity.y * mData.velocity.y) + (mData.velocity.z * mData.velocity.z));
            speed = math.clamp(speed, bData.minSpeed, bData.maxSpeed);
            float3 direction = mData.velocity / speed;
            mData.velocity = direction * speed;

            rot.Value = quaternion.LookRotationSafe(direction, math.up());
            pos.Value += mData.velocity * deltaTime;
        }).WithDeallocateOnJobCompletion(headingAccelerations).ScheduleParallel();
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

            for(int x=-1; x<2; x++)
            {
                for(int y=-1; y<2; y++)
                {
                    for(int z=-1; z<2; z++)
                    {
                        //Debug.Log("Quadrant: " + (hashKey.x + x, hashKey.y + y, hashKey.z + z));
                        
                        if (quadrantMap.TryGetFirstValue(new float3(hashKey.x+x, hashKey.y+y, hashKey.z+z), out item, out iter))
                        {
                            
                            do
                            {
                                if (item.entity != entity)
                                {
                                    float3 offset = entPos - item.position;
                                    float dst = math.sqrt(math.pow(offset.x, 2) + math.pow(offset.y, 2) + math.pow(offset.z, 2));

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
            if(count != 0)
            {
                cohesionH /= count;
            }
        }
    }

    

}
