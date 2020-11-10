using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

[UpdateAfter(typeof(QuadrantSystem))]
[UpdateBefore(typeof(MovementSystem))]
public class FindAccelerationSystem : SystemBase
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

    [BurstCompile]
    protected override void OnUpdate()
    {
        float deltaTime = Time.DeltaTime;

        var quadrantMultiHashMap = QuadrantSystem.quadrantMultiHashMap;
        //var obstacleVectors = boidObstacleVectors.directions;

        float3 alignHeading = float3.zero;
        float3 cohesionHeading = float3.zero;
        float3 separationHeading = float3.zero;

        var entityCount = GetEntityQuery(typeof(BoidData)).CalculateEntityCount();

        Entities.WithReadOnly(quadrantMultiHashMap).ForEach((ref accelerationHeadings accel, in BoidData bData, in moveData mData, in Translation pos, in Rotation rot, in Entity entity, in int entityInQueryIndex) =>
        {
            var alignHelp = new AlignmentHelper();
            alignHelp.avgHeadings(quadrantMultiHashMap, QuadrantSystem.GetPositionHMKey(pos.Value), entity, in pos.Value, in bData.sizeRadius, out alignHeading, out cohesionHeading, out separationHeading);

            accel.acceleration = float3.zero;

            if (!alignHeading.Equals(float3.zero))
            {
                accel.acceleration += math.clamp(math.normalizesafe(alignHeading) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * bData.alignmentWeight;
            }
            if (!cohesionHeading.Equals(float3.zero))
            {
                accel.acceleration += math.clamp(math.normalizesafe(cohesionHeading - pos.Value) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * bData.cohesionWeight;
            }
            if (!separationHeading.Equals(float3.zero))
            {
                accel.acceleration += math.clamp(math.normalizesafe(separationHeading) * bData.maxSpeed - mData.velocity, -bData.maxTurnSpeed, bData.maxTurnSpeed) * bData.separationWeight;
            }
        }).ScheduleParallel();
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

            for (int x = -1; x < 2; x++)
            {
                for (int y = -1; y < 2; y++)
                {
                    for (int z = -1; z < 2; z++)
                    {
                        //Debug.Log("Quadrant: " + (hashKey.x + x, hashKey.y + y, hashKey.z + z));

                        if (quadrantMap.TryGetFirstValue(new float3(hashKey.x + (x * cellSize), hashKey.y + (y * cellSize), hashKey.z + (z * cellSize)), out item, out iter))
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

                                    if (dst < avoidRadius)
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

}
