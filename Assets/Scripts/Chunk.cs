using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk
{
    public Vector2Int coord;

    GameObject chunkObject;
    MeshRenderer meshRenderer;
    MeshFilter meshFilter;

    int vertexIndex = 0;
    List<Vector3> verticies = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Vector2> uvs = new List<Vector2>();

    byte[,,] voxelMap = new byte[VoxelData.chunkWidth, VoxelData.chunkHeight, VoxelData.chunkWidth];
    World world;

    public Chunk (World world, Vector2Int coord)
    {
        this.world = world;
        this.coord = coord;
        chunkObject = new GameObject();

        meshFilter = chunkObject.AddComponent<MeshFilter>();
        meshRenderer = chunkObject.AddComponent<MeshRenderer>();

        meshRenderer.material = world.material;
        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(coord.x * VoxelData.chunkWidth, 0f, coord.y * VoxelData.chunkWidth);
        chunkObject.name = $"Chunk: ({coord.x}, {coord.y})";

        PopulateVoxelMap();
        CreateMeshData();
        CreateMesh();
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
    }

    void CreateMeshData()
    {
        for (int y = 0; y < VoxelData.chunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.chunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.chunkWidth; z++)
                {
                    if (world.blockTypes[voxelMap[x, y, z]].isSolid) AddVoxelDataToChunk(new Vector3Int(x, y, z));
                }
            }
        }
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

    /// <summary>
    /// Checks if the chunk is active, or sets the state of the chunk
    /// </summary>
    public bool isActive
    {
        get { return chunkObject.activeSelf; }
        set { chunkObject.SetActive(value); }
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
        if (!IsVoxelInChunk(pos)) return world.blockTypes[world.GetVoxel(pos + position)].isSolid;

        return world.blockTypes[voxelMap[pos.x, pos.y, pos.z]].isSolid;
    }

    void AddVoxelDataToChunk(Vector3Int pos)
    {
        for (int p = 0; p < 6; p++)
        {
            if (CheckVoxel(pos + VoxelData.faceChecks[p])) continue;

            byte blockId = voxelMap[pos.x, pos.y, pos.z];

            verticies.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 0]]);
            verticies.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 1]]);
            verticies.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 2]]);
            verticies.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 3]]);

            AddTexture(GetTextureID(blockId, p));

            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 3);

            vertexIndex += 4;
        }
    }

    void CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = verticies.ToArray();
        mesh.triangles = triangles.ToArray();
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
