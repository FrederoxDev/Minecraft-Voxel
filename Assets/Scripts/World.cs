using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public int seed;
    
    public Material material;
    public BlockType[] blockTypes;

    public Transform player;
    public Vector3 spawnPosition;
    Vector2Int playerLastChunkCoord;
    Vector2Int playerChunkCoord;

    Chunk[,] chunks = new Chunk[VoxelData.worldSizeInChunks, VoxelData.worldSizeInChunks];
    List<Vector2Int> activeChunks = new List<Vector2Int>();

    public AnimationCurve continentalnessCurve;
    public AnimationCurve peaksAndValleysCurve;
    public AnimationCurve erosionCurve;

    private void Awake()
    {
        blockTypes = Resources.LoadAll<BlockType>("Blocks");
    }

    private void Start()
    {
        Random.InitState(seed);

        spawnPosition = new Vector3(VoxelData.worldSizeInVoxels / 2f, VoxelData.chunkHeight + 1.7f, VoxelData.worldSizeInVoxels / 2);
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

    void CheckViewDistance()
    {
        Vector2Int playerPosCoord = WorldToChunk(player.position);

        List<Vector2Int> previouslyActiveChunks = new List<Vector2Int>(activeChunks);

        for (int x = playerPosCoord.x - VoxelData.viewDistanceInChunks; x < playerPosCoord.x + VoxelData.viewDistanceInChunks; x++)
        {
            for (int y = playerPosCoord.y - VoxelData.viewDistanceInChunks; y < playerPosCoord.y + VoxelData.viewDistanceInChunks; y++)
            {
                if (!IsChunkInWorld(new Vector2Int(x, y))) continue;

                if (chunks[x, y] == null) CreateNewChunk(x, y);
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

        var watch = System.Diagnostics.Stopwatch.StartNew();

        for (int x = startChunk; x < endChunk; x++)
        {
            for (int z = startChunk; z < endChunk; z++)
            {
                CreateNewChunk(x, z);
            }
        }

        watch.Stop();
        Debug.Log($"Generated world in {watch.ElapsedMilliseconds}ms");

        player.position = spawnPosition;
    }

    /// <summary>
    /// Creates a new chunk at a given chunk coordinate
    /// </summary>
    private void CreateNewChunk(int x, int z)
    {
        chunks[x, z] = new Chunk(this, new Vector2Int(x, z));
        activeChunks.Add(new Vector2Int(x, z));
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

        /* IMMUTABLE PASS */
        if (!IsVoxelInWorld(pos)) return GetBlockId("minecraft:air");
        if (pos.y == 0) return GetBlockId("minecraft:bedrock");
        byte blockId = GetBlockId("minecraft:air");

        /* TERRAIN SHAPING */
        float continentalness = continentalnessCurve.Evaluate(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, 0.25f));
        float peaksAndValleys = peaksAndValleysCurve.Evaluate(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 10000f, 1 - erosionScale));
        float erosion = erosionCurve.Evaluate(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 50000f, 0.5f));

        int terrainHeight = groundHeight + Mathf.FloorToInt((VoxelData.chunkHeight - groundHeight) * ((continentalness + peaksAndValleys + erosion) / 3));

        if (pos.y < terrainHeight) blockId = GetBlockId("minecraft:stone");
        if (pos.y == terrainHeight) blockId = GetBlockId("minecraft:grass");

        /* WATER PASS */
        if (pos.y <= waterLevel && blockId == GetBlockId("minecraft:air")) blockId = GetBlockId("minecraft:water");

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
