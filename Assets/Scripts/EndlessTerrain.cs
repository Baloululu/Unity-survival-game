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
    int herbeProche;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainVisibleLastUpdate = new List<TerrainChunk>();
    List<GameObject> grassNear = new List<GameObject>();
    List<GameObject> grassFar = new List<GameObject>();

    bool firstTime = true;

    private void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();

        maxViewDist = detailLevel[detailLevel.Length - 1].visibleDisThreshold;
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunkVisibleInViewDist = Mathf.RoundToInt(maxViewDist / chunkSize);

        herbeProche = (int)maxViewDist / 2;
        herbeProche *= herbeProche;

        GameObject grassParent = new GameObject("Grass");

        UpdateVisibleChunks();

        for (int i = 0; i < 10000; ++i)
        {
            int choice;
            choice = Random.Range(0, mapGenerator.grass.Length - 1);
            GameObject herbe = Instantiate(mapGenerator.grass[choice], Vector3.one, Quaternion.AngleAxis(Random.Range(-180f, 180f), Vector3.up), grassParent.transform);
            herbe.transform.localScale = new Vector3(Random.Range(0.9f, 1.1f), Random.Range(0.7f, 1.3f), Random.Range(0.9f, 1.1f));
            grassNear.Add(herbe);
        }
    }

    void initHerbe()
    {
        Vector2 position = new Vector2();
        int herbeDep = (int)Mathf.Sqrt(herbeProche);
        for (int i = 0; i < grassNear.Count; ++i)
        {
            position = viewerPosition + Random.insideUnitCircle * herbeDep;

            Vector3 posGrass = new Vector3(position.x, GrassHeigh(position), position.y);

            grassNear[i].transform.position = posGrass;
        }
    }

    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / scale;

        if ((viewerPostionOld - viewerPosition).sqrMagnitude > sqrviewerMoveThresholdForChunkUpdate)
        {
            if (firstTime)
            {
                initHerbe();
                firstTime = false;
            }
                
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

        for (int i = 0; i < grassNear.Count; ++i)
        {
            Vector2 grassDist = new Vector2(grassNear[i].transform.position.x, grassNear[i].transform.position.z);
            if ((grassDist - viewerPosition).sqrMagnitude > herbeProche)
            {
                grassFar.Add(grassNear[i]);
                grassNear.RemoveAt(i);
            }
        }

        for (int i = 0; i < grassFar.Count; ++i)
        {
            Vector2 grassDist = new Vector2(grassFar[i].transform.position.x, grassFar[i].transform.position.z);
            if ((grassDist - viewerPosition).sqrMagnitude <= herbeProche)
            {
                grassNear.Add(grassFar[i]);
                grassFar.RemoveAt(i);
            }
        }

        Vector2 position = new Vector2();

        for (int i = 0; i < grassFar.Count; ++i)
        {
            do
            {
                position = viewerPosition + Random.insideUnitCircle * (maxViewDist - 100);
            } while ((position - viewerPosition).sqrMagnitude < herbeProche);

            Vector3 posGrass = new Vector3(position.x, GrassHeigh(position), position.y);

            grassFar[i].transform.position = posGrass;
        }
    }

    float GrassHeigh(Vector2 pos)
    {
        int chunkCoordX = Mathf.RoundToInt(pos.x / chunkSize);
        int chunkCoordY = Mathf.RoundToInt(pos.y / chunkSize);

        Vector2 key = new Vector2(chunkCoordX, chunkCoordY);

        TerrainChunk terrain = terrainChunkDictionary[key];

        int xHeigh = (int)pos.x - (chunkCoordX * chunkSize);
        int yHeigh = (int)pos.y - (chunkCoordY * chunkSize);

        return terrain.Evaluer(xHeigh, yHeigh);
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
        GameObject[] grass;

        MapData mapData;
        public bool mapDataReceived;
        int previousLODIndex = -1, center;
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

            center = mapData.heightMap.GetLength(0) / 2;

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
                            GenerateTrees(100);
                            GenerateRocks(50);
                            hasObject = true;
                        }
                        /*if (!hasGrass)
                        {
                            grassParent = new GameObject("Grass");
                            grassParent.transform.parent = meshObject.transform;
                            GenerateGrass(centre, 5000);
                            hasGrass = true;
                        }*/

                        if (collisionLODMesh.hasMesh)
                        {
                            meshCollider.sharedMesh = collisionLODMesh.mesh;
                        }
                        else if (!collisionLODMesh.hasMesh)
                        {
                            collisionLODMesh.RequestMesh(mapData);
                        }
                    }
                    /*else if (hasGrass)
                    {
                        Destroy(grassParent);
                        hasGrass = false;
                    }*/

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

        void GenerateTrees(int number)
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
                    posTree.y = Evaluer((int)xalea, (int)zalea) - 0.5f;
                    int choice;
                    if (hauteur > mapGenerator.regions[mapGenerator.regions.Length - 1].height)
                        choice = 3;
                    else
                        choice = Random.Range(0, trees.Length - 1);
                    GameObject tree = Instantiate(trees[choice], posTree, Quaternion.AngleAxis(Random.Range(-180f, 180f), Vector3.up), treesParent.transform);
                    tree.transform.localScale = new Vector3(Random.Range(0.9f, 1.1f), Random.Range(0.7f, 1.3f), Random.Range(0.9f, 1.1f));
                }
            }
        }

        void GenerateRocks(int number)
        {
            for (int i = 0; i < number; i++)
            {
                float xalea = Random.Range(-center, center);
                float zalea = Random.Range(-center, center);
                Vector3 posRock = meshObject.transform.position;
                posRock.x += xalea;
                posRock.z += zalea;
                posRock.y = Evaluer((int)xalea, (int)zalea) - 0.5f;
                int choice;
                choice = Random.Range(0, rocks.Length - 1);
                GameObject rock = Instantiate(rocks[choice], posRock, Random.rotation, rocksParent.transform);
                rock.transform.localScale = new Vector3(Random.Range(0.7f, 1.3f), Random.Range(0.7f, 1.3f), Random.Range(0.7f, 1.3f));
            }
            for (int i = 0; i < number / 20; i++)
            {
                float xalea = Random.Range(-center, center);
                float zalea = Random.Range(-center, center);
                Vector3 posRock = meshObject.transform.position;
                posRock.x += xalea;
                posRock.z += zalea;
                posRock.y = Evaluer((int)xalea, (int)zalea) - 0.5f;
                int choice;
                choice = Random.Range(0, rocks.Length - 1);
                GameObject rock = Instantiate(rocks[choice], posRock, Random.rotation, rocksParent.transform);
                rock.transform.localScale = new Vector3(Random.Range(5f, 11f), Random.Range(5f, 11f), Random.Range(5f, 11f));
            }
        }

        /*void GenerateGrass(int number)
        {
            for (int i = 0; i < number; i++)
            {
                float xalea = Random.Range(-center, center);
                float zalea = Random.Range(-center, center);
                float hauteur = mapData.heightMap[center + (int)xalea, center - (int)zalea];
                if (hauteur > mapGenerator.regions[2].height && hauteur < mapGenerator.regions[mapGenerator.regions.Length - 3].height)
                {
                    Vector3 posGrass = meshObject.transform.position;
                    posGrass.x += xalea;
                    posGrass.z += zalea;
                    posGrass.y = EvaluerCentre((int) xalea, (int) zalea) - 0.1f;
                    int choice;
                    choice = Random.Range(0, grass.Length - 1);
                    GameObject herbe = Instantiate(grass[choice], posGrass, Quaternion.AngleAxis(AngleAlea(), Vector3.up), grassParent.transform);
                    herbe.transform.localScale = new Vector3(Random.Range(0.9f, 1.1f), Random.Range(0.7f, 1.3f), Random.Range(0.9f, 1.1f));
                }
            }
        }*/

        public float Evaluer(int x, int z)
        {
            return mapGenerator.meshHeightCurve.Evaluate(mapData.heightMap[center + x, center - z]) * mapGenerator.meshHeightMultiplier;
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
