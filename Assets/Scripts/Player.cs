using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public bool isGrounded;

    private Transform cam;
    private World world;

    private float walkSpeed = 5f;
    private const float gravity = -9.8f;
    private float jumpForce = 5f;

    private const float playerWidth = 0.2f;
    private const float playerHeight = 1.95f;

    private float horizontal;
    private float vertical;
    private float mouseHorizontal;
    private float mouseVertical;
    private Vector3 velocity;
    private float verticalMomentum;
    private bool jumpRequest;

    public float checkIncrement = 0.1f;
    public float reach = 5f;

    public Text selectedBlock;
    public byte blockIndex = 1;

    private void Start()
    {
        cam = Camera.main.transform;
        world = GameObject.FindObjectOfType<World>();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        blockIndex = 1;
        selectedBlock.text = $"Selected: {world.blockTypes[blockIndex].blockName}";
    }

    private void Update()
    {
        GetPlayerInput();
        transform.Rotate(Vector3.up * mouseHorizontal);
        cam.transform.Rotate(Vector3.right * -mouseVertical);
    }

    private void FixedUpdate()
    {
        CalculateVelocity();
        if (jumpRequest) Jump();

        transform.Translate(velocity, Space.World);
    }

    private void Jump()
    {
        verticalMomentum = jumpForce;
        isGrounded = false;
        jumpRequest = false;
    }

    private void CalculateVelocity()
    {
        if (verticalMomentum > gravity) verticalMomentum += Time.fixedDeltaTime * gravity;
        velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * walkSpeed;

        // Falling / Jumping
        velocity += Vector3.up * verticalMomentum * Time.fixedDeltaTime;

        // Constrain movement to stop (walking / jumping) through (walls / floors)
        if ((velocity.z > 0 && front) || (velocity.z < 0 && back)) velocity.z = 0;
        if ((velocity.x > 0 && right) || (velocity.x < 0 && left)) velocity.x = 0;

        if (velocity.y < 0) velocity.y = checkDownSpeed(velocity.y);
        if (velocity.y > 0) velocity.y = checkUpSpeed(velocity.y);
    }

    private void GetPlayerInput()
    {
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");
        mouseHorizontal = Input.GetAxis("Mouse X");
        mouseVertical = Input.GetAxis("Mouse Y");

        if (Input.GetButtonDown("Jump") && isGrounded) jumpRequest = true;

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0)
        {
            if (scroll > 0) blockIndex++;
            if (scroll < 0) blockIndex--;

            if (blockIndex > (byte)world.blockTypes.Length - 1) blockIndex = 1;
            if (blockIndex < 1) blockIndex = (byte)(world.blockTypes.Length - 1);

            selectedBlock.text = $"Selected: {world.blockTypes[blockIndex].blockName}";
        }

        Vector3Int destroyBlock = GetDestroyBlock();
        Vector3Int placeBlock = GetPlaceBlock();

        // Destroy
        if (Input.GetMouseButtonDown(0) && destroyBlock != new Vector3Int(-1, -1, -1)) {
            world.GetChunkFromWorld(destroyBlock).EditVoxel(destroyBlock, 0);
        }

        // Place
        if (Input.GetMouseButtonDown(1) && placeBlock != new Vector3Int(-1, -1, -1))
        {
            Vector3Int playerPos = new Vector3Int(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y), Mathf.FloorToInt(transform.position.z));

            if (placeBlock.Equals(playerPos) || placeBlock.Equals(playerPos + new Vector3Int(0, 1, 0))) return;

            world.GetChunkFromWorld(placeBlock).EditVoxel(placeBlock, blockIndex);
        }
    }

    private float checkDownSpeed(float downSpeed)
    {
        float x = transform.position.x;
        float y = transform.position.y;
        float z = transform.position.z;

        if (
            world.checkForVoxel(x - playerWidth, y + downSpeed, z + playerWidth) && (!left && !front) ||
            world.checkForVoxel(x - playerWidth, y + downSpeed, z - playerWidth) && (!left && !back) ||
            world.checkForVoxel(x + playerWidth, y + downSpeed, z - playerWidth) && (!right && !back) ||
            world.checkForVoxel(x + playerWidth, y + downSpeed, z + playerWidth) && (!right && !front)
        ) {
            isGrounded = true;
            return 0;
        }

        isGrounded = false;
        return downSpeed;
    }

    private float checkUpSpeed(float upSpeed)
    {
        float x = transform.position.x;
        float y = transform.position.y;
        float z = transform.position.z;

        if (
            world.checkForVoxel(x - playerWidth, y + upSpeed + playerHeight, z - playerWidth) && (!left && !back) ||
            world.checkForVoxel(x - playerWidth, y + upSpeed + playerHeight, z + playerWidth) && (!left && !front) ||
            world.checkForVoxel(x + playerWidth, y + upSpeed + playerHeight, z - playerWidth) && (!right && !back) ||
            world.checkForVoxel(x + playerWidth, y + upSpeed + playerHeight, z + playerWidth) && (!right && !front)
        )
        {
            verticalMomentum = 0;
            return 0;
        }

        return upSpeed;
    }

    public bool front
    {
        get
        {
            float x = transform.position.x;
            float y = transform.position.y;
            float z = transform.position.z + playerWidth;

            if (world.checkForVoxel(x, y, z) || world.checkForVoxel(x, y + 1, z)) return true;
            return false;
        }
    }

    public bool back
    {
        get
        {
            float x = transform.position.x;
            float y = transform.position.y;
            float z = transform.position.z - playerWidth;

            if (world.checkForVoxel(x, y, z) || world.checkForVoxel(x, y + 1, z)) return true;
            return false;
        }
    }

    public bool left
    {
        get
        {
            float x = transform.position.x - playerWidth;
            float y = transform.position.y;
            float z = transform.position.z;

            if (world.checkForVoxel(x, y, z) || world.checkForVoxel(x, y + 1, z)) return true;
            return false;
        }
    }
    public bool right
    {
        get
        {
            float x = transform.position.x + playerWidth;
            float y = transform.position.y;
            float z = transform.position.z;

            if (world.checkForVoxel(x, y, z) || world.checkForVoxel(x, y + 1, z)) return true;
            return false;
        }
    }

    public Vector3Int GetDestroyBlock()
    {
        float step = checkIncrement;
        Vector3 lastPos = new Vector3();

        while (step < reach)
        {
            Vector3 pos = cam.position + (cam.forward * step);

            if (world.checkForVoxel(pos.x, pos.y, pos.z))
            {
                return new Vector3Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
            }

            lastPos = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
            step += checkIncrement;
        }

        return new Vector3Int(-1, -1, -1);
    }

    public Vector3Int GetPlaceBlock()
    {
        float step = checkIncrement;
        Vector3Int lastPos = new Vector3Int();

        while (step < reach)
        {
            Vector3 pos = cam.position + (cam.forward * step);

            if (world.checkForVoxel(pos.x, pos.y, pos.z))
            {
                return lastPos;
            }

            lastPos = new Vector3Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
            step += checkIncrement;
        }

        return new Vector3Int(-1, -1, -1);
    }
}
