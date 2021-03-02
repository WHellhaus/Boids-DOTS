using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Mathematics;
using Unity.Collections;
public class MapHelper
{
    // Start is called before the first frame updatepublic static PhysicsCollider CreateTerrainCollider(TerrainData terrainData, CollisionFilter filter)
    public static PhysicsCollider CreateTerrainCollider(TerrainData terrainData, CollisionFilter filter)
    {
        var physicsCollider = new PhysicsCollider();
        var size = new int2(terrainData.heightmapResolution, terrainData.heightmapResolution);
        var scale = terrainData.heightmapScale;

        var colliderHeights = new NativeArray<float>(terrainData.heightmapResolution * terrainData.heightmapResolution,
            Allocator.TempJob);

        var terrainHeights = terrainData.GetHeights(0, 0, terrainData.heightmapResolution,
            terrainData.heightmapResolution);


        for (int j = 0; j < size.y; j++)
            for (int i = 0; i < size.x; i++)
            {
                var h = terrainHeights[i, j];
                //colliderHeights[j + i * size.x] = h;
                var randomExtra = UnityEngine.Random.Range(0.00001f, 0.0001f); // NOTE: Added to original code to prevent flat terrain colliders not working

                colliderHeights[j + i * size.x] = h + randomExtra;
            }

        physicsCollider.Value = Unity.Physics.TerrainCollider.Create(colliderHeights, size, scale,
            Unity.Physics.TerrainCollider.CollisionMethod.Triangles, filter);

        colliderHeights.Dispose();

        return physicsCollider;
    }

}