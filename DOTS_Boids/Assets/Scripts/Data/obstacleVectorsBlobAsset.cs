using Unity.Entities;
using Unity.Mathematics;

public struct ObstacleVectorsBlobAsset : IComponentData
{
    public BlobArray<float3> vectorsArray;
}

public struct ObstacleVectors : IComponentData
{
    public BlobAssetReference<ObstacleVectorsBlobAsset> blobAsset;
}