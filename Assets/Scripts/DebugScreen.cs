using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DebugScreen : MonoBehaviour
{
    private Text debugText;
    private World world;

    private float frameRate;
    private float timer;

    private void Awake()
    {
        debugText = GetComponent<Text>();
        debugText.enabled = false;

        world = GameObject.FindObjectOfType<World>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3)) debugText.enabled = !debugText.enabled;
        if (!debugText.enabled) return;

        // Work out the frames per second
        if (timer > 1f)
        {
            frameRate = (int)(1f / Time.unscaledDeltaTime);
            timer = 0;
        } else 
        {
            timer += Time.deltaTime;
        }

        Vector3 pos = world.player.transform.position;
        Vector2Int chunkCoord = world.playerChunkCoord;

        string text = "Voxel-Engine v1 (FrederoxDev)";
        text += $"\n{frameRate} fps";
        text += $"\n\nXYZ: {pos.x} / {pos.y} / {pos.z}";
        text += $"\nChunk: {chunkCoord.x} {chunkCoord.y}";

        debugText.text = text;
    }
}
