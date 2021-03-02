using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Authoring;
public class PhysicsTerrain : MonoBehaviour, IConvertGameObjectToEntity
{


    [SerializeField] PhysicsCategoryTags belongsTo;
    [SerializeField] PhysicsCategoryTags collidesWith;
    [SerializeField] int groupIndex;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var terrain = GetComponent<Terrain>();

        if (terrain == null)
        {
            Debug.LogError("No terrain found!");
            return;
        }

        CollisionFilter collisionFilter = new CollisionFilter
        {
            BelongsTo = belongsTo.Value,
            CollidesWith = collidesWith.Value,
            GroupIndex = groupIndex
        };

        dstManager.AddComponentData(entity,
            MapHelper.CreateTerrainCollider(terrain.terrainData, collisionFilter));
    }

    void Start()
    {
        var Coll = Terrain.activeTerrain.GetComponent<UnityEngine.TerrainCollider>();
        Coll.enabled = true;
    }
}