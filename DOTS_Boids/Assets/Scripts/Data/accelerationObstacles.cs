﻿using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct accelerationObstacles : IComponentData
{
    public float3 acceleration;
}

