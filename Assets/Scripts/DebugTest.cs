using System;
using System.Linq;
using UnityEngine;

public class DebugTest : MonoBehaviour
{
    [Header("References")]
    public ComputeShader computeShader;
    public Texture2D inputTexture;
    public Material instancedMaterial;
    public Mesh sphereMesh;

    private bool imageLoaded = false;

    [Header("Settings")]
    public float scale = 0.1f;


    //Compute Buffers
    private ComputeBuffer allDataBuffer;
    private ComputeBuffer RGBMapBuffer;
    private ComputeBuffer RGBCountBuffer;
    private ComputeBuffer uniqueRGBBuffer;
    private ComputeBuffer countBuffer;

    private ComputeBuffer targetPositionsRGBBuffer;
    private ComputeBuffer currentDataRGBBuffer;
    private ComputeBuffer argsBuffer;


    private int uniqueColorCount;

    private int kernelWritePositions;
    private int kernelInitRGBMap;
    private int kernelWriteRGBMap;
    private int kernelFilterRGBMap;

    private int width, height, totalPixels;
    private int resultCount;

    private Data[] resultData;

    struct Data
    {
        public Vector4 color;
        public Vector3 position;
        public int count;
    }

    void Start()
    {
        if (computeShader == null) return;

        kernelWritePositions = computeShader.FindKernel("WritePositionsRGB");
        kernelInitRGBMap = computeShader.FindKernel("InitRGBMap");
        kernelWriteRGBMap = computeShader.FindKernel("WriteToRGBMap");
        kernelFilterRGBMap = computeShader.FindKernel("FilterRGBMap");
    }
    void Update()
    { // Load texture 
        if (Input.GetKeyDown(KeyCode.Space))
        {
            try
            {
                TextureLoader.LoadTexture(); if (TextureLoader.textureLoaded)
                { //When Texture is loaded apply Shader code on it 
                    inputTexture = TextureLoader.texture;
                    //Update image related Variables 
                    width = inputTexture.width;
                    height = inputTexture.height;
                    totalPixels = width * height; Debug.Log("Texture Loaded");
                    // //Release the old Buffers 
                    ReleaseAllBuffers();
                    Debug.Log("Buffers Released");

                    // /Write the positions 
                    WritePositionsRGB();
                    Debug.Log("Positions Written");

                    //Init the RGB Map
                    InitRGBMap();
                    Debug.Log("RGB Map filled");

                    //Write to the RGB Map
                    WriteRGBMap();
                    Debug.Log("RGB Map Written to");

                    //Filter the RGB Map
                    FilterRGBMap();
                    Debug.Log("RGB Map Filtered");

                    //Read back the number of unique RGB Values
                    GetUniqueColorCount();
                    Debug.Log("Unique RGB Colors: " + uniqueColorCount);
                    imageLoaded = true;
                }
                else { throw new Exception("No Error at texture load but boolean not set"); }
            }
            catch (Exception e)
            {
                Debug.Log("Can't load Texture: " + e.ToString());
                imageLoaded = false;
            }
        }


        // Read data and prepare instance rendering 
        if (Input.GetKeyDown(KeyCode.R))
        {
            ReadDataFromGPU();
        }


        RenderSpheres();
    }


    //RenderSpheres
    void RenderSpheres()
    {

        //Exit early if image is not loaded
        if (imageLoaded == false)
        {
            return;
        }

        //Send data to gpu
        instancedMaterial.SetBuffer("_InstanceData", uniqueRGBBuffer);
        instancedMaterial.SetFloat("_Scale", scale);

        // Arguments buffer for DrawMeshInstancedIndirect
        if (argsBuffer == null)
        {
            uint[] args = new uint[5] { sphereMesh.GetIndexCount(0), (uint)uniqueColorCount, 0, 0, 0 };
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);
        }

        Graphics.DrawMeshInstancedIndirect(
            sphereMesh,
            0,
            instancedMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000f),
            argsBuffer
        );
    }

    private void WritePositionsRGB()
    {
        //Create new Buffer to write to
        allDataBuffer = new ComputeBuffer(totalPixels, sizeof(float) * 4 + sizeof(float) * 3 + sizeof(int));

        //Assign variables and Buffers for shader
        computeShader.SetTexture(kernelWritePositions, "_Texture", inputTexture);
        computeShader.SetInt("_Width", width);
        computeShader.SetInt("_Height", height);
        computeShader.SetBuffer(kernelWritePositions, "_AllData", allDataBuffer);

        //Dispatch the shader
        computeShader.Dispatch(kernelWritePositions, Mathf.CeilToInt(width / 32f), Mathf.CeilToInt(height / 32f), 1);
    }

    private void InitRGBMap()
    {
        //Create the Buffer
        RGBMapBuffer = new ComputeBuffer(255 * 255 * 255, sizeof(float) * 4 + sizeof(float) * 3 + sizeof(int));
        RGBCountBuffer = new ComputeBuffer(255 * 255 * 255, sizeof(uint));

        //Assing Variables and Buffers for shader
        computeShader.SetBuffer(kernelInitRGBMap, "_RGBMap", RGBMapBuffer);
        computeShader.SetBuffer(kernelInitRGBMap, "_RGBCounts", RGBCountBuffer);

        //Dispatch the shader
        int threadSize = Mathf.CeilToInt(255f / 8f);
        computeShader.Dispatch(kernelInitRGBMap, threadSize, threadSize, threadSize);
    }

    private void WriteRGBMap()
    {
        //Assing Variables and Buffers for shader
        computeShader.SetBuffer(kernelWriteRGBMap, "_RGBCounts", RGBCountBuffer);
        computeShader.SetBuffer(kernelWriteRGBMap, "_AllData", allDataBuffer);
        computeShader.SetInt("_Width", width);
        computeShader.SetInt("_Height", height);

        //Dispatch the shader
        computeShader.Dispatch(kernelWriteRGBMap, Mathf.CeilToInt(width * height / 512f), 1, 1);
    }


    private void FilterRGBMap()
    {
        //Create the buffer for the Unique Colors
        uniqueRGBBuffer = new ComputeBuffer(width * height, sizeof(float) * 4 + sizeof(float) * 3 + sizeof(int), ComputeBufferType.Append);
        uniqueRGBBuffer.SetCounterValue(0);

        //Assing Variables and Buffers for shader
        computeShader.SetBuffer(kernelFilterRGBMap, "_RGBCounts", RGBCountBuffer);
        computeShader.SetBuffer(kernelFilterRGBMap, "_RGBMap", RGBMapBuffer);
        computeShader.SetBuffer(kernelFilterRGBMap, "_UniqueRGB", uniqueRGBBuffer);


        //Dispatch the shader
        int threadSize = Mathf.CeilToInt(255f / 8f);
        computeShader.Dispatch(kernelFilterRGBMap, threadSize, threadSize, threadSize);
    }


    private void GetUniqueColorCount()
    {
        countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(uniqueRGBBuffer, countBuffer, 0);

        int[] countArray = new int[1];
        countBuffer.GetData(countArray);
        uniqueColorCount = countArray[0];
    }


    private void InitWorkingBuffer()
    {
    
    }

    private void ReadDataFromGPU()
    {
        Data[] RGBMapData = new Data[255 * 255 * 255];
        uint[] colorCount = new uint[255 * 255 * 255];
        Data[] uniqueColors = new Data[uniqueColorCount];
        Data[] positions = new Data[width * height];


        RGBMapBuffer.GetData(RGBMapData);
        RGBCountBuffer.GetData(colorCount);
        uniqueRGBBuffer.GetData(uniqueColors);
        allDataBuffer.GetData(positions);
    }


    void ReleaseAllBuffers()
    {
        allDataBuffer?.Release();
        RGBCountBuffer?.Release();
        RGBMapBuffer?.Release();
        uniqueRGBBuffer?.Release();
        argsBuffer?.Release();
        countBuffer?.Release();
    }

    void OnDestroy()
    {
        ReleaseAllBuffers();
    }

    void OnDrawGizmos()
    {
        if (resultData != null)
        {
            foreach (Data data in resultData)
            {
                Gizmos.color = data.color;
                Gizmos.DrawSphere(data.position, data.count * scale);
            }
        }
    }
}
