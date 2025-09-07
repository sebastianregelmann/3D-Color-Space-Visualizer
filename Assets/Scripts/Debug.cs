using System.Linq;
using UnityEngine;

public class Debug : MonoBehaviour
{
    [Header("References")]
    public ComputeShader computeShader;
    public Texture2D inputTexture;
    public Material instancedMaterial;
    public Mesh sphereMesh;

    [Header("Settings")]
    public float scale = 0.1f;

    private ComputeBuffer allDataBuffer;
    private ComputeBuffer combinedDataBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer countBuffer;

    private int kernelWritePositions;
    private int kernelCombineDuplicates;

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
        kernelCombineDuplicates = computeShader.FindKernel("CombineDublicatesRGB");
    }

    void Update()
    {
        // Load texture
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TextureLoader.LoadTexture();
            if (TextureLoader.textureLoaded)
                inputTexture = TextureLoader.texture;


            //Release all previous buffers
            allDataBuffer?.Release();
            combinedDataBuffer?.Release();
            argsBuffer?.Release();
            countBuffer?.Release();
            resultData = null;
        }

        // Write raw pixel data
        if (Input.GetKeyDown(KeyCode.P))
        {
            WritePositionsRGB();
        }

        // Combine duplicates
        if (Input.GetKeyDown(KeyCode.C))
        {
            CombinePositionsRGB();
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
        if (resultData == null || resultData.Length <= 0)
        {
            return;
        }

        //Send data to gpu
        instancedMaterial.SetBuffer("_InstanceData", combinedDataBuffer);
        instancedMaterial.SetFloat("_Scale", scale);

        // Arguments buffer for DrawMeshInstancedIndirect
        if (argsBuffer == null)
        {
            uint[] args = new uint[5] { sphereMesh.GetIndexCount(0), (uint)resultData.Length, 0, 0, 0 };
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
        if (inputTexture == null) return;

        width = inputTexture.width;
        height = inputTexture.height;
        totalPixels = width * height;

        allDataBuffer?.Release();
        allDataBuffer = new ComputeBuffer(totalPixels, sizeof(float) * 4 + sizeof(float) * 3 + sizeof(int));

        computeShader.SetTexture(kernelWritePositions, "_Texture", inputTexture);
        computeShader.SetInt("_Width", width);
        computeShader.SetInt("_Height", height);
        computeShader.SetBuffer(kernelWritePositions, "_AllData", allDataBuffer);

        computeShader.Dispatch(kernelWritePositions, Mathf.CeilToInt(width / 128), Mathf.CeilToInt(height / 128f), 1);
    }

    private void CombinePositionsRGB()
    {
        if (inputTexture == null) return;

        combinedDataBuffer?.Release();
        combinedDataBuffer = new ComputeBuffer(totalPixels, sizeof(float) * 4 + sizeof(float) * 3 + sizeof(int), ComputeBufferType.Append);
        combinedDataBuffer.SetCounterValue(0);

        computeShader.SetInt("_Width", width);
        computeShader.SetInt("_Height", height);
        computeShader.SetBuffer(kernelCombineDuplicates, "_AllData", allDataBuffer);
        computeShader.SetBuffer(kernelCombineDuplicates, "_CombinedData", combinedDataBuffer);

        computeShader.Dispatch(kernelCombineDuplicates, Mathf.CeilToInt(totalPixels / 512f), 1, 1);
    }

    private void ReadDataFromGPU()
    {
        countBuffer?.Release();
        countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(combinedDataBuffer, countBuffer, 0);

        int[] countArray = { 0 };
        countBuffer.GetData(countArray);
        resultCount = countArray[0];

        resultData = new Data[resultCount];
        combinedDataBuffer.GetData(resultData, 0, 0, resultCount);
    }

    void OnDestroy()
    {
        allDataBuffer?.Release();
        combinedDataBuffer?.Release();
        argsBuffer?.Release();
        countBuffer?.Release();
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
