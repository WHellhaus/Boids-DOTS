using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[GenerateAuthoringComponent]
public struct moveData : IComponentData
{
    public float3 velocity;
}
