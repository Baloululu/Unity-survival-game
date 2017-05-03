using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour
{

    const float scale = 1f;

    const float viewerMoveThresholdForChunkUpdate = 25f;
    const float sqrviewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

    public LODInfo[] detailLevel;
    public static float maxViewDist = 600;
    public Transform viewer;
    public Material mapMaterial;

    public static Vector2 viewerPosition;
    Vector2 viewerPostionOld;
    static MapGenerator mapGenerator;
    int chunkSize;
    int chunkVisibleInViewDist;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainVisibleLastUpdate = new List<TerrainChunk>();

    private void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();

        maxViewDist = detailLevel[detailLevel.Length - 1].visibleDisThreshold;
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunkVisibleInViewDist = Mathf.RoundToInt(maxViewDist / chunkSize);

        UpdateVisibleChunks();
    }

    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / scale;

        if ((viewerPostionOld - viewerPosition).sqrMagnitude > sqrviewerMoveThresholdForChunkUpdate)
        {
            UpdateVisibleChunks();
            viewerPostionOld = viewerPosition;
        }
    }

    void UpdateVisibleChunks()
    {
        for (int i = 0; i < terrainVisibleLastUpdate.Count; i++)
        {
            terrainVisibleLastUpdate[i].SetVisible(false);
        }
        terrainVisibleLastUpdate.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunkVisibleInViewDist; yOffset <= chunkVisibleInViewDist; yOffset++)
        {
            for (int xOffset = -chunkVisibleInViewDist; xOffset <= chunkVisibleInViewDist; xOffset++)
            {
                Vector2 viewerChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (terrainChunkDictionary.ContainsKey(viewerChunkCoord))
                {
                    terrainChunkDictionary[viewerChunkCoord].UpdateCoordChunk();
                }
                else
                {
                    terrainChunkDictionary.Add(viewerChunkCoord, new TerrainChunk(viewerChunkCoord, chunkSize, detailLevel, transform, mapMaterial, mapGenerator.trees, mapGenerator.rocks));
                }
            }
        }
    }

    public class TerrainChunk
    {
        Vector2 position;
        GameObject meshObject, treesParent, rocksParent;
        Bounds bounds;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        MeshCollider meshCollider;

        LODInfo[] detailLevels;
        LODMesh[] lodMeshes;
        LODMesh collisionLODMesh;

        GameObject[] trees;
        GameObject[] rocks;

        MapData mapData;
        bool mapDataReceived;
        int previousLODIndex = -1;
        bool hasObject = false;

        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material, GameObject[] trees, GameObject[] rocks)
        {
            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);
            this.detailLevels = detailLevels;

            this.trees = trees;
            this.rocks = rocks;

            meshObject = new GameObject("Terrain Chunk");
            treesParent = new GameObject("Trees");
            rocksParent = new GameObject("Rocks");

            treesParent.transform.parent = meshObject.transform;
            rocksParent.transform.parent = meshObject.transform;

            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshRenderer.material = material;
            meshObject.transform.position = positionV3 * scale;
            meshObject.transform.localScale = Vector3.one * scale;
            meshObject.transform.parent = parent;
            SetVisible(false);

            lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++)
            {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateCoordChunk);
                if (detailLevels[i].useForCollider)
                {
                    collisionLODMesh = lodMeshes[i];
                }
            }

            mapGenerator.RequestMapData(position, OnMapDataReceived);
        }

        void OnMapDataReceived(MapData mapData)
        {
            this.mapData = mapData;
            mapDataReceived = true;

            Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
            meshRenderer.material.mainTexture = texture;

            UpdateCoordChunk();
        }

        public void UpdateCoordChunk()
        {
            if (mapDataReceived)
            {
                float viewerDistFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
                bool visible = viewerDistFromNearestEdge <= maxViewDist;

                if (visible)
                {
                    int lodIndex = 0;

                    for (int i = 0; i < detailLevels.Length; i++)
                    {
                        if (viewerDistFromNearestEdge > detailLevels[i].visibleDisThreshold)
                            lodIndex = i + 1;
                        else
                            break;
                    }

                    if (lodIndex != previousLODIndex)
                    {
                        LODMesh lodMesh = lodMeshes[lodIndex];
                        if (lodMesh.hasMesh)
                        {
                            meshFilter.mesh = lodMesh.mesh;
                        }
                        else if (!lodMesh.hasRequestedMesh)
                        {
                            lodMesh.RequestMesh(mapData);
                        }
                    }

                    if (lodIndex == 0)
                    {
                        if (!hasObject)
                        {
                            int centre = mapData.heightMap.GetLength(0) / 2;
                            GenerateTrees(centre, 100);
                            GenerateRocks(centre, 50);
                            hasObject = true;
                        }

                        if (collisionLODMesh.hasMesh)
                        {
                            meshCollider.sharedMesh = collisionLODMesh.mesh;
                        }
                        else if (!collisionLODMesh.hasMesh)
                        {
                            collisionLODMesh.RequestMesh(mapData);
                        }
                    }

                    terrainVisibleLastUpdate.Add(this);
                }

                SetVisible(visible);
            }
        }

        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }

        void GenerateTrees(int center, int number)
        {
            for (int i = 0; i < number; i++)
            {
                float xalea = Random.Range(-center, center);
                float zalea = Random.Range(-center, center);
                float hauteur = mapData.heightMap[center + (int)xalea, center - (int)zalea];
                if (hauteur > mapGenerator.regions[1].height)
                {
                    Vector3 posTree = meshObject.transform.position;
                    posTree.x += xalea;
                    posTree.z += zalea;
                    posTree.y = mapGenerator.meshHeightCurve.Evaluate(mapData.heightMap[center + (int)xalea, center - (int)zalea]) * mapGenerator.meshHeightMultiplier - 0.5f;
                    int choice;
                    if (hauteur > mapGenerator.regions[mapGenerator.regions.Length - 1].height)
                        choice = 3;
                    else
                        choice = Random.Range(0, trees.Length - 1);
                    GameObject tree = Instantiate(trees[choice], posTree, Quaternion.AngleAxis(AngleAlea(), Vector3.up), treesParent.transform);
                    tree.transform.localScale = new Vector3(Random.Range(9f, 11f) / 10f, Random.Range(7f, 13f) / 10f, Random.Range(9f, 11f) / 10f);
                }
            }
        }

        void GenerateRocks(int center, int number)
        {
            for (int i = 0; i < number; i++)
            {
                float xalea = Random.Range(-center, center);
                float zalea = Random.Range(-center, center);
                float hauteur = mapData.heightMap[center + (int)xalea, center - (int)zalea];
                Vector3 posRock = meshObject.transform.position;
                posRock.x += xalea;
                posRock.z += zalea;
                posRock.y = mapGenerator.meshHeightCurve.Evaluate(mapData.heightMap[center + (int)xalea, center - (int)zalea]) * mapGenerator.meshHeightMultiplier - 0.5f;
                int choice;
                choice = Random.Range(0, rocks.Length - 1);
                GameObject rock = Instantiate(rocks[choice], posRock, Quaternion.Euler(AngleAlea(), AngleAlea(), AngleAlea()), rocksParent.transform);
                rock.transform.localScale = new Vector3(Random.Range(7f, 13f) / 10f, Random.Range(7f, 13f) / 10f, Random.Range(7f, 13f) / 10f);
            }
        }

        float AngleAlea()
        {
            return Random.Range(-180f, 180f);
        }
    }

    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        int lod;
        System.Action updateCallback;

        public LODMesh(int lod, System.Action updateCallback)
        {
            this.lod = lod;
            this.updateCallback = updateCallback;
        }

        void OnMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;

            updateCallback();
        }

        public void RequestMesh(MapData mapData)
        {
            hasRequestedMesh = true;
            mapGenerator.RequetMeshData(mapData, lod, OnMeshDataReceived);
        }
    }

    [System.Serializable]
    public struct LODInfo
    {
        public int lod;
        public float visibleDisThreshold;
        public bool useForCollider;
    }
}
