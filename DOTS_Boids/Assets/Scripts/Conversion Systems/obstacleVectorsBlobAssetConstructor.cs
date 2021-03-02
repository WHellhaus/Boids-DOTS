using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

[UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
public class obstacleVectorsBlobAssetConstructor : GameObjectConversionSystem
{
    const int numViewDirections = 300;
    public static BlobAssetReference<ObstacleVectorsBlobAsset> blobAssetReference;
    

    protected override void OnUpdate()
    {
        using (BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp))
        {
            ref ObstacleVectorsBlobAsset obsVectorsBlobAsset = ref blobBuilder.ConstructRoot<ObstacleVectorsBlobAsset>();
            BlobBuilderArray<float3> obsVectorArray = blobBuilder.Allocate(ref obsVectorsBlobAsset.vectorsArray, numViewDirections);

            float goldenRatio = (1 + math.sqrt(5)) / 2;
            float angleIncrement = math.PI * 2 * goldenRatio;

            for (int i = 0; i < numViewDirections; i++)
            {
                float t = (float)i / numViewDirections;
                float inclination = math.acos(1 - 2 * t);
                float azimuth = angleIncrement * i;

                float x = math.sin(inclination) * math.cos(azimuth);
                float y = math.sin(inclination) * math.sin(azimuth);
                float z = math.cos(inclination);
                //if(i < (int)(numViewDirections * 0.9f))
                obsVectorArray[i] = new float3(x, y, z);
            }

            blobAssetReference = blobBuilder.CreateBlobAssetReference<ObstacleVectorsBlobAsset>(Allocator.Persistent);
            //Debug.Log(blobAssetReference.Value.vectorsArray.Length);            
        }
    }
}
