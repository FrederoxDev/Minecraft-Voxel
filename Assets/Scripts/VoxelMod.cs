using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelMod
{
    public Vector3Int position;
    public byte id;

    public VoxelMod()
    {
        position = new Vector3Int();
        id = 0;
    }

    public VoxelMod(Vector3Int position, byte id)
    {
        this.position = position;   
        this.id = id;
    }
}
