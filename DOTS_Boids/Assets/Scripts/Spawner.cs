using Unity.Entities;
using UnityEngine;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;

public class Spawner : MonoBehaviour
{
    [SerializeField] private GameObject gameObjectPrefab;

    [SerializeField] int xSize = 10;
    [SerializeField] int ySize = 10;
    [Range(0.1f, 2f)]
    [SerializeField] float spacing = 1f;
    [SerializeField] float spawnRadius = 3f;
    [SerializeField] int spawnAmount = 10;


    private Entity entityPrefab;
    private World defaultWorld;
    private EntityManager entityManager;

    private BlobAssetStore assetStore;

    // Start is called before the first frame update
    void Start()
    {
        defaultWorld = World.DefaultGameObjectInjectionWorld;
        entityManager = defaultWorld.EntityManager;
        assetStore = new BlobAssetStore();

        GameObjectConversionSettings settings = GameObjectConversionSettings.FromWorld(defaultWorld, assetStore);
        entityPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(gameObjectPrefab, settings);

        //InstantiateEntityGrid(xSize, ySize, spacing);
        InstantiateEntityRandom(spawnAmount);
    }

    private void InstantiateEntity(float3 position)
    {
        Entity myEntity = entityManager.Instantiate(entityPrefab);
        entityManager.SetComponentData(myEntity, new Translation
        {
            Value = position
        });
        entityManager.AddComponentData(myEntity, new ObstacleVectors
        {
            blobAsset = obstacleVectorsBlobAssetConstructor.blobAssetReference
        });
        entityManager.SetComponentData(myEntity, new Rotation
        {
            Value = quaternion.LookRotationSafe((position - new float3(gameObject.transform.position)), math.up())
        });
    }

    private void InstantiateEntityRandom(int numEntities)
    {
        for (int i=0; i<numEntities; i++)
        {
            InstantiateEntity(transform.position + (UnityEngine.Random.insideUnitSphere * spawnRadius));
        }
    }

    private void InstantiateEntityGrid(int dmX, int dmY, float spacing = 1f)
    {
        for (int i = dmX; i > 0; i--)
        {
            for (int j = dmY; j > 0; j--)
            {
                InstantiateEntity(new float3(i * spacing, j * spacing, 0f));
            }
        }
    }

    private void OnDestroy()
    {
        assetStore.Dispose();
    }
}
