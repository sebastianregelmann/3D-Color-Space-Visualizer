using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShaderManager : MonoBehaviour
{

    [Header("References")]
    public ComputeShader computeShader;
    public Texture2D inputTexture;
    public Material instancedMaterial;
    public Mesh mesh;



    [Header("Animation Settings")]
    public float scale = 0.1f;

    public float animationTime;
    private float timer = 0f;
    private float _t = 0f;
    public bool startAnimation = false;
    private bool animationRunning = false;

    /// <summary>
    /// Kernel IDs
    /// </summary>
    private int kernelInitRGBMap;
    private int kernelWriteRGBMap;
    private int kernelFindUniqueRGBColors;
    private int kernelInitWorkBuffer;
    private int kernelAnimateRGBtoHSV;
    private int kernelAnimateHSVtoRGB;

    /// <summary>
    /// Compute Buffer needed
    /// </summary>

    //Buffers for tracking the number of pixels with the same color
    private ComputeBuffer RGBMapBuffer;
    private ComputeBuffer RGBCountBuffer;
    private ComputeBuffer uniqueRGBBuffer;
    private ComputeBuffer uniqueRGBCountBuffer;
    private ComputeBuffer RGBReadBuffer;
    private ComputeBuffer HSVReadBuffer;
    private ComputeBuffer workBuffer;
    private ComputeBuffer argsBuffer;



    //Helper variable for that script
    private bool imageLoaded = false;
    private int textureWidth;
    private int textureHeight;
    private int totalPixelCount;
    private int uniqueColorCount;
    private int dataSize;


    [Header("Debug")]
    ANIMATION_DIRECTION animationDirection = ANIMATION_DIRECTION.RGBtoHSV;




    // Start is called before the first frame update
    void Start()
    {
        GetKernelIDs();

        //Calculate the size of one data element
        dataSize = sizeof(float) * 4 + sizeof(float) * 3 + sizeof(int);
    }



    // Update is called once per frame
    void Update()
    {
        // Load texture 
        if (Input.GetKeyDown(KeyCode.Space))
        {
            //Reset data load flag
            imageLoaded = false;

            //Release the args buffer
            argsBuffer?.Release();
            argsBuffer = null;

            try
            {
                TextureLoader.LoadTexture(); if (TextureLoader.textureLoaded)
                {
                    //When Texture is loaded apply Shader code on it 
                    inputTexture = TextureLoader.texture;

                    //Update image related Variables 
                    textureWidth = inputTexture.width;
                    textureHeight = inputTexture.height;
                    totalPixelCount = textureWidth * textureHeight;


                    AssignShaderVariables();

                    Debug.Log("Texture Loaded");
                    //Release the old Buffers 


                    //Init the RGB Map
                    InitRGBMap();
                    Debug.Log("RGB Map filled");

                    //Write to the RGB Map
                    WriteRGBMap();
                    Debug.Log("RGB Map Written to");


                    //Filter the RGB Map
                    FindUniqueRGBColors();
                    Debug.Log("RGB Map Filtered");

                    //Read back the number of unique RGB Values
                    GetUniqueColorCount();
                    Debug.Log("Unique RGB Colors: " + uniqueColorCount);

                    InitWorkBuffers();
                    Debug.Log("Init Movement Buffer");

                    imageLoaded = true;
                }
                else
                {
                    throw new System.Exception("No Error at texture load but boolean not set");
                }
            }
            catch (System.Exception e)
            {
                Debug.Log("Can't load Texture: " + e.ToString());
                imageLoaded = false;
            }
        }

        UpdateAnimationTime();

        // Then move positions using the updated _t
        if (animationRunning)
        {
            AnimatePosition();
        }

        // Render spheres
        RenderSpheres();


        if (Input.GetKeyDown(KeyCode.R))
        {
            DebugReadBack();
        }
    }


    /// <summary>
    /// Reads back the kernel IDs
    /// </summary>
    private void GetKernelIDs()
    {
        kernelInitRGBMap = computeShader.FindKernel("InitRGBMap");
        kernelWriteRGBMap = computeShader.FindKernel("WriteToRGBMap");
        kernelFindUniqueRGBColors = computeShader.FindKernel("FilterRGBMap");
        kernelInitWorkBuffer = computeShader.FindKernel("InitWorkBuffer");
        kernelAnimateRGBtoHSV = computeShader.FindKernel("AnimateRGBtoHSV");
        kernelAnimateHSVtoRGB = computeShader.FindKernel("AnimateHSVtoRGB");
    }



    /// <summary>
    /// Get's the kernel size from the shader
    /// </summary>
    private Vector3Int GetThreadGroupSize(int kernelID)
    {
        uint x, y, z;

        computeShader.GetKernelThreadGroupSizes(kernelID, out x, out y, out z);

        return new Vector3Int((int)x, (int)y, (int)z);
    }



    /// <summary>
    /// Initializes a new RGB map for tracking the Number of pixels with the same Color
    /// </summary>
    private void InitRGBMap()
    {
        //Release buffer if old one exists
        RGBCountBuffer?.Release();
        RGBMapBuffer?.Release();
        RGBCountBuffer = null;
        RGBMapBuffer = null;


        //Create the Buffer
        RGBMapBuffer = new ComputeBuffer(255 * 255 * 255, dataSize);
        RGBCountBuffer = new ComputeBuffer(255 * 255 * 255, sizeof(uint));


        //Assing Variables and Buffers for shader
        computeShader.SetBuffer(kernelInitRGBMap, "_RGBMap", RGBMapBuffer);
        computeShader.SetBuffer(kernelInitRGBMap, "_RGBCounts", RGBCountBuffer);


        //Calculate the number of threads dispatched
        Vector3Int threadGroupSize = GetThreadGroupSize(kernelInitRGBMap);
        Vector3Int threadCount = new Vector3Int(Mathf.CeilToInt(255f / threadGroupSize.x), Mathf.CeilToInt(255f / threadGroupSize.y), Mathf.CeilToInt(255f / threadGroupSize.z));


        //Dispatch the shader
        computeShader.Dispatch(kernelInitRGBMap, threadCount.x, threadCount.y, threadCount.z);
    }


    /// <summary>
    /// Fills the RGB count buffer with the number of pixels with the same color at the pixels index
    /// </summary>
    private void WriteRGBMap()
    {
        //Check if needed Buffers are initialized
        if (RGBCountBuffer == null || RGBMapBuffer == null || inputTexture == null)
        {
            throw new System.Exception("Buffers for WriteRGBMap not initialized");
        }


        //Assing Variables and Buffers for shader
        computeShader.SetBuffer(kernelWriteRGBMap, "_RGBCounts", RGBCountBuffer);
        computeShader.SetTexture(kernelWriteRGBMap, "_Texture", inputTexture);


        //Calculate the number of threads dispatched
        Vector3Int threadGroupSize = GetThreadGroupSize(kernelWriteRGBMap);
        Vector3Int threadCount = new Vector3Int(Mathf.CeilToInt((float)textureWidth / threadGroupSize.x), Mathf.CeilToInt((float)textureHeight / threadGroupSize.y), Mathf.CeilToInt(1f / threadGroupSize.z));


        //Dispatch the shader
        computeShader.Dispatch(kernelWriteRGBMap, threadCount.x, threadCount.y, threadCount.z);
    }



    /// <summary>
    /// Goes over each entry in RGBCountBuffer and only saves the entries that have a count bigger than 0
    /// </summary>
    private void FindUniqueRGBColors()
    {
        //Release buffer if old one exists
        uniqueRGBBuffer?.Release();
        uniqueRGBBuffer = null;


        //Create a new Buffer for the unique RGB Values
        uniqueRGBBuffer = new ComputeBuffer(totalPixelCount, dataSize, ComputeBufferType.Append);
        uniqueRGBBuffer.SetCounterValue(0);


        //Check if all needed Buffers are initialized
        if (RGBCountBuffer == null || RGBMapBuffer == null || uniqueRGBBuffer == null)
        {
            throw new System.Exception("Buffers for FindUniqueRGBColors not initialized");
        }


        //Assing Variables and Buffers for shader
        computeShader.SetBuffer(kernelFindUniqueRGBColors, "_RGBCounts", RGBCountBuffer);
        computeShader.SetBuffer(kernelFindUniqueRGBColors, "_RGBMap", RGBMapBuffer);
        computeShader.SetBuffer(kernelFindUniqueRGBColors, "_UniqueRGB", uniqueRGBBuffer);


        //Calculate the number of threads dispatched
        Vector3Int threadGroupSize = GetThreadGroupSize(kernelFindUniqueRGBColors);
        Vector3Int threadCount = new Vector3Int(Mathf.CeilToInt(255f / threadGroupSize.x), Mathf.CeilToInt(255f / threadGroupSize.y), Mathf.CeilToInt(255f / threadGroupSize.z));


        //Dispatch the shader
        computeShader.Dispatch(kernelFindUniqueRGBColors, threadCount.x, threadCount.y, threadCount.z);
    }



    /// <summary>
    /// Reads back the number of Colors that are unique
    /// </summary>
    private void GetUniqueColorCount()
    {
        //Release old Buffer if not existing
        uniqueRGBCountBuffer?.Release();
        uniqueRGBCountBuffer = null;


        //Initialize a new buffer
        uniqueRGBCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);


        //Check if all needed Buffers are existing
        if (uniqueRGBBuffer == null || uniqueRGBCountBuffer == null)
        {
            throw new System.Exception("Buffers for FindUniqueRGBColors not initialized");
        }


        //Copy the data from one buffer to the other
        ComputeBuffer.CopyCount(uniqueRGBBuffer, uniqueRGBCountBuffer, 0);


        //Read back the data
        int[] countArray = new int[1];
        uniqueRGBCountBuffer.GetData(countArray);
        uniqueColorCount = countArray[0];
    }



    /// <summary>
    /// Initialises the Read and Read Write buffers that animation woks with
    /// </summary>
    private void InitWorkBuffers()
    {
        //Release buffer if old one exists
        RGBReadBuffer?.Release();
        HSVReadBuffer?.Release();
        workBuffer?.Release();
        RGBReadBuffer = null;
        HSVReadBuffer = null;
        workBuffer = null;


        //Create the Buffer
        RGBReadBuffer = new ComputeBuffer(uniqueColorCount, dataSize);
        HSVReadBuffer = new ComputeBuffer(uniqueColorCount, dataSize);
        workBuffer = new ComputeBuffer(uniqueColorCount, dataSize);


        //Check if all Buffers needed are initialized
        if (uniqueRGBBuffer == null || uniqueRGBCountBuffer == null || RGBReadBuffer == null || HSVReadBuffer == null || workBuffer == null)
        {
            throw new System.Exception("Buffers for InitWorkBuffers not initialized");
        }


        //Assing Variables and Buffers for shader
        computeShader.SetBuffer(kernelInitWorkBuffer, "_RGBCounts", uniqueRGBCountBuffer);
        computeShader.SetBuffer(kernelInitWorkBuffer, "_UniqueRGBRead", uniqueRGBBuffer); //Asing this buffer to a Read only buffer to read from the append Buffer
        computeShader.SetBuffer(kernelInitWorkBuffer, "_FinalRGBRead", RGBReadBuffer);
        computeShader.SetBuffer(kernelInitWorkBuffer, "_FinalHSVRead", HSVReadBuffer);
        computeShader.SetBuffer(kernelInitWorkBuffer, "_WorkOnData", workBuffer);

        //Calculate the number of threads dispatched
        Vector3Int threadGroupSize = GetThreadGroupSize(kernelInitWorkBuffer);
        Vector3Int threadCount = new Vector3Int(Mathf.CeilToInt((float)uniqueColorCount / threadGroupSize.x), Mathf.CeilToInt(1f / threadGroupSize.y), Mathf.CeilToInt(1f / threadGroupSize.z));


        //Dispatch the shader
        computeShader.Dispatch(kernelInitWorkBuffer, threadCount.x, threadCount.y, threadCount.z);
    }


    /// <summary>
    /// Assigns Variables to the shader that do not change while using the same image
    /// </summary>
    private void AssignShaderVariables()
    {
        computeShader.SetInt("_Width", textureWidth);
        computeShader.SetInt("_Height", textureHeight);
    }



    /// <summary>
    /// Animates the position from RGB to HSV or reverse
    /// </summary>
    private void AnimatePosition()
    {
        //Check if all Buffers needed are initialized
        if (uniqueRGBCountBuffer == null || RGBReadBuffer == null || HSVReadBuffer == null || workBuffer == null)
        {
            throw new System.Exception("Buffers for AnimatePosition not initialized");
        }


        int kernelID = animationDirection == ANIMATION_DIRECTION.RGBtoHSV ? kernelAnimateRGBtoHSV : kernelAnimateHSVtoRGB;


        //Assing Variables and Buffers for shader
        computeShader.SetBuffer(kernelID, "_RGBCounts", uniqueRGBCountBuffer);
        computeShader.SetBuffer(kernelID, "_FinalRGBRead", RGBReadBuffer);
        computeShader.SetBuffer(kernelID, "_FinalHSVRead", HSVReadBuffer);
        computeShader.SetBuffer(kernelID, "_WorkOnData", workBuffer);


        //Asing Animation time 
        computeShader.SetFloat("_T", _t);


        //Calculate the number of threads dispatched
        Vector3Int threadGroupSize = GetThreadGroupSize(kernelID);
        Vector3Int threadCount = new Vector3Int(Mathf.CeilToInt((float)uniqueColorCount / threadGroupSize.x), Mathf.CeilToInt(1f / threadGroupSize.y), Mathf.CeilToInt(1f / threadGroupSize.z));


        //Dispatch the shader
        computeShader.Dispatch(kernelID, threadCount.x, threadCount.y, threadCount.z);
    }


    /// <summary>
    /// Updates the animation timer
    /// </summary>
    private void UpdateAnimationTime()
    {
        if (imageLoaded == false)
        {
            return;
        }

        //Check if last animation step was final one
        if (_t >= 1f)
        {
            animationRunning = false;
            timer = 0;
            _t = 0;
            //Debug Switch the animation direction
            switch (animationDirection)
            {
                case ANIMATION_DIRECTION.RGBtoHSV:
                    animationDirection = ANIMATION_DIRECTION.HSVtoRGB;
                    break;
                case ANIMATION_DIRECTION.HSVtoRGB:
                    animationDirection = ANIMATION_DIRECTION.RGBtoHSV;
                    break;
            }
        }


        if (startAnimation)
        {
            animationRunning = true;
            startAnimation = false;
            timer = 0;
            _t = 0;
        }

        if (animationRunning)
        {
            //Calculate _t
            _t = Mathf.Clamp01(timer / animationTime);

            timer += Time.deltaTime;
        }

    }



    /// <summary>
    /// Sends a instance Draw command to the GPU based on the data in workBuffer
    /// </summary>
    void RenderSpheres()
    {
        //Exit early if image is not loaded
        if (imageLoaded == false || uniqueColorCount <= 0)
        {
            return;
        }

        //Send data to gpu
        instancedMaterial.SetBuffer("_InstanceData", workBuffer);
        instancedMaterial.SetFloat("_Scale", scale);

        // Arguments buffer for DrawMeshInstancedIndirect
        if (argsBuffer == null)
        {
            uint[] args = new uint[5] { mesh.GetIndexCount(0), (uint)uniqueColorCount, 0, 0, 0 };
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);
        }

        Graphics.DrawMeshInstancedIndirect(
            mesh,
            0,
            instancedMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000f),
            argsBuffer
        );
    }




    private void DebugReadBack()
    {
        Data[] workData = new Data[uniqueColorCount];
        workBuffer.GetData(workData);
        Debug.Log("");
    }
    struct Data
    {
        public Vector4 color;
        public Vector3 position;
        public int count;
    }



    /// <summary>
    /// Enum for the animation Direction
    /// </summary>
    private enum ANIMATION_DIRECTION
    {
        RGBtoHSV,
        HSVtoRGB,
    }

}
