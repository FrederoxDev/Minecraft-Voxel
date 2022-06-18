using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk
{
    public Vector2Int coord;

    GameObject chunkObject;
    MeshRenderer meshRenderer;
    MeshFilter meshFilter;

    Material[] materials = new Material[2];

    int vertexIndex = 0;
    List<Vector3> verticies = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Vector2> uvs = new List<Vector2>();

    List<int> transparentTriangles = new List<int>();


    public byte[,,] voxelMap = new byte[VoxelData.chunkWidth, VoxelData.chunkHeight, VoxelData.chunkWidth];
    World world;

    public bool isVoxelMapPopulated = false;
    private bool _isActive;

    public Queue<VoxelMod> modifications = new Queue<VoxelMod>();

    public Chunk (World world, Vector2Int coord, bool generateOnLoad)
    {
        this.world = world;
        this.coord = coord;
        isActive = true;

        if (generateOnLoad) Init();
    }

    public void Init()
    {
        chunkObject = new GameObject();
        meshFilter = chunkObject.AddComponent<MeshFilter>();
        meshRenderer = chunkObject.AddComponent<MeshRenderer>();

        materials[0] = world.material;
        materials[1] = world.transparentMaterial;
        meshRenderer.materials = materials;

        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(coord.x * VoxelData.chunkWidth, 0f, coord.y * VoxelData.chunkWidth);
        chunkObject.name = $"Chunk: ({coord.x}, {coord.y})";

        PopulateVoxelMap();
        UpdateChunk();
    }

    void PopulateVoxelMap()
    {
        for (int y = 0; y < VoxelData.chunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.chunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.chunkWidth; z++)
                {
                    voxelMap[x, y, z] = world.GetVoxel(new Vector3Int(x, y, z) + position);
                }
            }
        }

        isVoxelMapPopulated = true;
    }

    public bool isActive
    {
        get { return _isActive; }
        set
        {
            _isActive = value;
            if (chunkObject != null)
                chunkObject.SetActive(value);
        }
    }

    public void UpdateChunk()
    {
        while (modifications.Count > 0)
        {
            VoxelMod v = modifications.Dequeue();
            Vector3Int pos = v.position -= position;
            voxelMap[pos.x, pos.y, pos.z] = v.id;
        }

        ClearMeshData();

        for (int y = 0; y < VoxelData.chunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.chunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.chunkWidth; z++)
                {
                    if (world.blockTypes[voxelMap[x, y, z]].isSolid) UpdateMeshData(new Vector3Int(x, y, z));
                }
            }
        }

        CreateMesh();
    }

    public void EditVoxel(Vector3Int pos, byte newId)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= position.x;
        zCheck -= position.z;

        voxelMap[xCheck, yCheck, zCheck] = newId;

        UpdateChunk();
        UpdateSurroundingVoxels(new Vector3Int(xCheck, yCheck, zCheck));
    }

    void UpdateSurroundingVoxels(Vector3Int pos)
    {
        for (int p = 0; p < 6; p++)
        {
            Vector3Int currentVoxel = pos + VoxelData.faceChecks[p];

            if (!IsVoxelInChunk(currentVoxel))
            {
                (world.GetChunkFromWorld(currentVoxel + position)).UpdateChunk();
            }
        }
    }

    void ClearMeshData()
    {
        vertexIndex = 0;
        verticies.Clear();
        triangles.Clear();
        transparentTriangles.Clear();
        uvs.Clear();
    }

    /// <summary>
    /// Returns the world position of the chunk
    /// </summary>
    public Vector3Int position
    {
        get { 
            return new Vector3Int(
                Mathf.FloorToInt(chunkObject.transform.position.x),
                Mathf.FloorToInt(chunkObject.transform.position.y),
                Mathf.FloorToInt(chunkObject.transform.position.z)
            ); 
        }
    }

    bool IsVoxelInChunk(Vector3Int pos)
    {
        if (pos.x < 0 || pos.x > VoxelData.chunkWidth - 1) return false;
        if (pos.z < 0 || pos.z > VoxelData.chunkWidth - 1) return false;
        if (pos.y < 0 || pos.y > VoxelData.chunkHeight - 1) return false;

        return true;
    }

    bool CheckVoxel(Vector3Int pos)
    {
        if (!IsVoxelInChunk(pos)) return world.checkIfVoxelTransparent(pos + position);

        return world.blockTypes[voxelMap[pos.x, pos.y, pos.z]].isTransparent;
    }

    public byte GetVoxelFromWorldPos(Vector3Int pos)
    {
        pos.x -= Mathf.FloorToInt(chunkObject.transform.position.x);
        pos.z -= Mathf.FloorToInt(chunkObject.transform.position.z);

        return voxelMap[pos.x, pos.y, pos.z];
    }

    void UpdateMeshData(Vector3Int pos)
    {
        byte blockId = voxelMap[pos.x, pos.y, pos.z];
        bool isTransparent = world.blockTypes[blockId].isTransparent;

        for (int p = 0; p < 6; p++)
        {
            if (!CheckVoxel(pos + VoxelData.faceChecks[p])) continue;

            verticies.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 0]]);
            verticies.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 1]]);
            verticies.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 2]]);
            verticies.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 3]]);

            AddTexture(GetTextureID(blockId, p));

            if (!isTransparent)
            {
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 3);
            }

            else
            {
                transparentTriangles.Add(vertexIndex);
                transparentTriangles.Add(vertexIndex + 1);
                transparentTriangles.Add(vertexIndex + 2);
                transparentTriangles.Add(vertexIndex + 2);
                transparentTriangles.Add(vertexIndex + 1);
                transparentTriangles.Add(vertexIndex + 3);
            }
            

            vertexIndex += 4;
        }
    }

    void CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = verticies.ToArray();

        mesh.subMeshCount = 2;
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.SetTriangles(transparentTriangles.ToArray(), 1);
        
        mesh.uv = uvs.ToArray();

        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
    }

    void AddTexture(int textureID)
    {
        float y = textureID / VoxelData.textureAtlasSizeInBlocks;
        float x = textureID - (y * VoxelData.textureAtlasSizeInBlocks);

        x *= VoxelData.NormalizedBlockTextureSize;
        y *= VoxelData.NormalizedBlockTextureSize;

        y = 1f - y - VoxelData.NormalizedBlockTextureSize;

        uvs.Add(new Vector2(x, y));
        uvs.Add(new Vector2(x, y + VoxelData.NormalizedBlockTextureSize));
        uvs.Add(new Vector2(x + VoxelData.NormalizedBlockTextureSize, y));
        uvs.Add(new Vector2(x + VoxelData.NormalizedBlockTextureSize, y + VoxelData.NormalizedBlockTextureSize));
    }

    public int GetTextureID(byte blockId, int faceIndex)
    {
        BlockType blockType = world.blockTypes[blockId];
        
        switch (faceIndex)
        {
            case 0:
                return blockType.back;

            case 1:
                return blockType.front;

            case 2:
                return blockType.top;

            case 3:
                return blockType.bottom;

            case 4:
                return blockType.left;

            case 5:
                return blockType.right;

            default:
                Debug.LogError("Invalid Face Index");
                return 0;
        }
    }
}
