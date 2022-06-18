using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Structure
{
    public static void MakeTree (Vector3Int pos, Queue<VoxelMod> queue, int minTrunkHeight, int maxTrunkHeight, World world)
    {
        int height = (int)(maxTrunkHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 250f, 3f));
        if (height < minTrunkHeight) height = minTrunkHeight;

        int leavesStartHeight = (int)(5 * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 280f, 3f));
        if (leavesStartHeight < 3) leavesStartHeight = 3;

        int baseLeavesRadius = (int)(6 * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 340f, 3f));
        if (baseLeavesRadius < 4) baseLeavesRadius = 4;

        // Create central log
        for (int i = 1; i < height - 3; i++)
        {
            queue.Enqueue(new VoxelMod(new Vector3Int(pos.x, pos.y + i, pos.z), world.GetBlockId("minecraft:oak_log")));
        }

        // Leaves
        for (int i = 1 + leavesStartHeight; i < height + 3; i++)
        {
            int leavesRadius = Mathf.FloorToInt((1 - (float) i / (float) height) * baseLeavesRadius);

            for (int x = -leavesRadius; x <= leavesRadius; x++)
            {
                for (int z = -leavesRadius; z <= leavesRadius; z++)
                {
                    float distance = Mathf.Sqrt((x * x) + (z * z));

                    if (distance <= leavesRadius && !(x == 0 && z == 0 && i < height - 3))
                        queue.Enqueue(new VoxelMod(new Vector3Int(pos.x + x, pos.y + i, pos.z + z), world.GetBlockId("minecraft:oak_leaves")));
                }
            }
        }
    }
}
