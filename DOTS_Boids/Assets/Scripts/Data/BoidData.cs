using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[GenerateAuthoringComponent]
public struct BoidData : IComponentData
{
    public float minSpeed;
    public float maxSpeed;
    public float maxTurnSpeed;
    public float perceptionRadius;
    public float sizeRadius;

    public float alignmentWeight;
    public float cohesionWeight;
    public float separationWeight;
}
