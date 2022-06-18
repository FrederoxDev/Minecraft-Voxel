using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public Material material;
    public Material transparentMaterial;

    public BlockType[] blockTypes;

    public Transform player;
    public Vector3 spawnPosition;
    Vector2Int playerLastChunkCoord;
    public Vector2Int playerChunkCoord;

    Chunk[,] chunks = new Chunk[VoxelData.worldSizeInChunks, VoxelData.worldSizeInChunks];
    List<Vector2Int> activeChunks = new List<Vector2Int>();
    List<Vector2Int> chunksToCreate = new List<Vector2Int>();
    List<Chunk> chunksToUpdate = new List<Chunk>();

    public AnimationCurve continentalnessCurve;
    public AnimationCurve peaksAndValleysCurve;
    public AnimationCurve erosionCurve;
    public AnimationCurve caveDensity;

    private bool isCreatingChunks;
    Queue<VoxelMod> modifications = new Queue<VoxelMod>();

    private void Awake()
    {
        blockTypes = Resources.LoadAll<BlockType>("Blocks");
        Application.targetFrameRate = 15000;
    }

    private void Start()
    { 
        spawnPosition = new Vector3(VoxelData.worldSizeInVoxels / 2f, VoxelData.chunkHeight - 20, VoxelData.worldSizeInVoxels / 2);
        GenerateWorld();

        playerLastChunkCoord = WorldToChunk(player.position);
    }

    private void Update()
    {
        playerChunkCoord = WorldToChunk(player.position);

        if (playerChunkCoord != playerLastChunkCoord)
        {
            CheckViewDistance();
            playerLastChunkCoord = playerChunkCoord;
        }

        if (chunksToCreate.Count > 0 && !isCreatingChunks) StartCoroutine("CreateChunks");
    }

    public bool checkForVoxel(Vector3Int pos)
    {
        Vector2Int coord = new Vector2Int(pos.x / VoxelData.chunkWidth, pos.z / VoxelData.chunkWidth);

        if (!IsChunkInWorld(coord) || pos.y < 0 || pos.y > VoxelData.chunkHeight) return false;

        if (chunks[coord.x, coord.y] != null && chunks[coord.x, coord.y].isVoxelMapPopulated)
            return blockTypes[chunks[coord.x, coord.y].GetVoxelFromWorldPos(pos)].isSolid;

        return blockTypes[GetVoxel(pos)].isSolid;
    }

    public bool checkForVoxel(float x, float y, float z)
    {
        Vector2Int coord = new Vector2Int(Mathf.FloorToInt(x / VoxelData.chunkWidth), Mathf.FloorToInt(z / VoxelData.chunkWidth));
        Vector3Int pos = new Vector3Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y), Mathf.FloorToInt(z));

        if (!IsVoxelInWorld(pos)) return false;

        if (chunks[coord.x, coord.y] != null && chunks[coord.x, coord.y].isVoxelMapPopulated)
            return blockTypes[chunks[coord.x, coord.y].GetVoxelFromWorldPos(pos)].isSolid;

        return blockTypes[GetVoxel(pos)].isSolid;
    }

    public bool checkIfVoxelTransparent(Vector3Int pos)
    {
        Vector2Int coord = new Vector2Int(pos.x / VoxelData.chunkWidth, pos.z / VoxelData.chunkWidth);

        if (!IsChunkInWorld(coord) || pos.y < 0 || pos.y > VoxelData.chunkHeight) return false;

        if (chunks[coord.x, coord.y] != null && chunks[coord.x, coord.y].isVoxelMapPopulated)
            return blockTypes[chunks[coord.x, coord.y].GetVoxelFromWorldPos(pos)].isTransparent;

        return blockTypes[GetVoxel(pos)].isTransparent;
    }

    /// <summary>
    /// Returns the chunk coord for a point in world space
    /// </summary>
    public Vector2Int WorldToChunk(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / VoxelData.chunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.chunkWidth);

        return new Vector2Int(x, z);
    }

    /// <summary>
    /// Returns a reference for a chunk in world space
    /// </summary>
    public Chunk GetChunkFromWorld(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / VoxelData.chunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.chunkWidth);

        return chunks[x, z];
    }

    void CheckViewDistance()
    {
        Vector2Int playerPosCoord = WorldToChunk(player.position);

        List<Vector2Int> previouslyActiveChunks = new List<Vector2Int>(activeChunks);

        for (int x = playerPosCoord.x - VoxelData.viewDistanceInChunks; x < playerPosCoord.x + VoxelData.viewDistanceInChunks; x++)
        {
            for (int y = playerPosCoord.y - VoxelData.viewDistanceInChunks; y < playerPosCoord.y + VoxelData.viewDistanceInChunks; y++)
            {
                if (!IsChunkInWorld(new Vector2Int(x, y))) continue;

                if (chunks[x, y] == null)
                {
                    chunks[x, y] = new Chunk(this, new Vector2Int(x, y), false);
                    chunksToCreate.Add(new Vector2Int(x, y));
                }
                else if (!chunks[x, y].isActive)
                {
                    chunks[x, y].isActive = true;
                    activeChunks.Add(new Vector2Int(x, y));
                }

                // Remove chunk from previouslyActive chunks when it is still in render distance, so it is not culled
                for (int i = 0; i < previouslyActiveChunks.Count; i++)
                {
                    if (previouslyActiveChunks[i].Equals(new Vector2Int(x, y))) previouslyActiveChunks.RemoveAt(i);
                }
            }
        }

        // Remove any chunks that are no longer in the viewing distance
        foreach(Vector2Int c in previouslyActiveChunks)
        {
            chunks[c.x, c.y].isActive = false;
            activeChunks.Remove(new Vector2Int(c.x, c.y));
        }
    }

    /// <summary>
    /// Runs the first time the world is entered, creates initial chunks
    /// </summary>
    private void GenerateWorld()
    {
        int startChunk = (VoxelData.worldSizeInChunks / 2) - VoxelData.viewDistanceInChunks;
        int endChunk = (VoxelData.worldSizeInChunks / 2) + VoxelData.viewDistanceInChunks;

        for (int x = startChunk; x < endChunk; x++)
        {
            for (int z = startChunk; z < endChunk; z++)
            {
                chunks[x, z] = new Chunk(this, new Vector2Int(x, z), true);
                activeChunks.Add(new Vector2Int(x, z));
            }
        }

        while (modifications.Count > 0)
        {
            VoxelMod v = modifications.Dequeue();
            Vector2Int c = new Vector2Int(Mathf.FloorToInt(v.position.x / VoxelData.chunkWidth), Mathf.FloorToInt(v.position.z / VoxelData.chunkWidth));

            if (chunks[c.x, c.y] == null)
            {
                chunks[c.x, c.y] = new Chunk(this, c, true);
                activeChunks.Add(c);
            }

            chunks[c.x, c.y].modifications.Enqueue(v);

            if (!chunksToUpdate.Contains(chunks[c.x, c.y])) chunksToUpdate.Add(chunks[c.x, c.y]);
        }

        for (int i = 0; i < chunksToUpdate.Count; i++)
        {
            chunksToUpdate[0].UpdateChunk();
            chunksToUpdate.RemoveAt(0);
        }

        player.position = spawnPosition;
    }

    IEnumerator CreateChunks()
    {
        isCreatingChunks = true;

        while(chunksToCreate.Count > 0)
        {
            chunks[chunksToCreate[0].x, chunksToCreate[0].y].Init();
            chunksToCreate.RemoveAt(0);
            yield return null;
        }

        isCreatingChunks = false;
    }


    /// <summary>
    /// Checks if a coord is inside of the world boundry
    /// </summary>
    private bool IsChunkInWorld (Vector2Int coord)
    {
        if (coord.x > 0 && coord.x < VoxelData.worldSizeInChunks - 1 && coord.y > 0 && coord.y < VoxelData.worldSizeInChunks) return true;
        else return false;
    }

    /// <summary>
    /// Checks if a voxel is inside of the world boundry
    /// </summary>
    private bool IsVoxelInWorld(Vector3Int pos)
    {
        if (pos.x < 0 || pos.x >= VoxelData.worldSizeInVoxels) return false;
        if (pos.y < 0 || pos.y >= VoxelData.chunkHeight) return false;
        if (pos.z < 0 || pos.z >= VoxelData.worldSizeInVoxels) return false;

        return true;
    }

    /// <summary>
    /// Returns what type of block a position should be at
    /// </summary> 
    /// <returns>Block Byte ID</returns>
    public byte GetVoxel(Vector3Int pos)
    {
        const int groundHeight = 60;
        const int waterLevel = 76;
        const float erosionScale = 0.9f; // Higher Values = Flatter terrain

        float treeZoneScale = 1.3f;
        float treeZoneThreshold = 0.6f;

        float treePlacementScale = 15f;
        float treePlacementThreshold = 0.75f;

        int maxTreeHeight = 28;
        int minTreeHeight = 14;

        /* IMMUTABLE PASS */
        if (!IsVoxelInWorld(pos)) return GetBlockId("minecraft:air");
        if (pos.y == 0) return GetBlockId("minecraft:bedrock");
        byte blockId = GetBlockId("minecraft:air");

        /* TERRAIN SHAPING */
        float continentalness = continentalnessCurve.Evaluate(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, 0.25f));
        float peaksAndValleys = peaksAndValleysCurve.Evaluate(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 10000f, 1 - erosionScale));
        float erosion = erosionCurve.Evaluate(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 50000f, 0.5f));

        int terrainHeight = groundHeight + Mathf.FloorToInt((VoxelData.chunkHeight - groundHeight) * ((continentalness + peaksAndValleys + erosion) / 3));

        int dirtHeight = Mathf.FloorToInt(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 5939f, 2f) * 5);

        if (pos.y < terrainHeight) blockId = GetBlockId("minecraft:stone");
        if (pos.y == terrainHeight) blockId = GetBlockId("minecraft:grass");
        else if (pos.y > terrainHeight - dirtHeight && blockId == GetBlockId("minecraft:stone")) blockId = GetBlockId("minecraft:dirt");

        /* WATER PASS */
        if (pos.y <= waterLevel && blockId == GetBlockId("minecraft:air")) blockId = GetBlockId("minecraft:water");

        /* CAVES */
        float caveThreshold = 0.55f;
        float heightMultiplier = caveDensity.Evaluate((float) pos.y / (float) VoxelData.chunkHeight);
        float density = Noise.Get3DPerlin((Vector3)pos, 25000f, 0.07f) * heightMultiplier;

        if (density > caveThreshold && blockId != GetBlockId("minecraft:water")) blockId = GetBlockId("minecraft:air");

        /* TREES */
        if (pos.y == terrainHeight && pos.y > waterLevel)
        {
            if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 40302f, treeZoneScale) > treeZoneThreshold && blockId == GetBlockId("minecraft:grass"))
            {
                if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 98342f, treePlacementScale) > treePlacementThreshold)
                {
                    blockId = GetBlockId("minecraft:dirt");
                    Structure.MakeTree(pos, modifications, minTreeHeight, maxTreeHeight, this);
                }
            }
        }

        return blockId;
    }

    public byte GetBlockId(string identifier)
    {
        for (byte i = 0; i < blockTypes.Length; i++)
        {
            if (blockTypes[i].identifier == identifier)
            {
                return i;
            }
        }

        Debug.LogError("Could not find block with id: " + identifier);
        return 0;
    }
}
