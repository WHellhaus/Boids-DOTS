using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct accelerationHeadings : IComponentData
{
    public float3 acceleration;
}
