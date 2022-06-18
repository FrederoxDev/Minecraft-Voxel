using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "BlockType", order = 1)]
public class BlockType : ScriptableObject
{
    public string blockName;
    public string identifier;
    public bool isSolid;
    public bool isTransparent;

    [Header("Textures")]
    public int back;
    public int front;
    public int top;
    public int bottom;
    public int left;
    public int right;
}
