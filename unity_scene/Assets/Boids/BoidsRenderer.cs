// BoidsRenderer.cs
// Attach this script to any GameObject in the scene.
// Assign the BoidsSimulation compute shader and a Mesh + Material for rendering.

using UnityEngine;

public class BoidsRenderer : MonoBehaviour
{
    [Header("Compute")]
    public ComputeShader boidsComputeShader;

    [Header("Spawn")]
    public int   boidCount    = 2048;
    public float spawnRadius  = 10f;

    [Header("Rendering")]
    public Mesh     boidMesh;       // e.g. a simple arrow/cone mesh
    public Material boidMaterial;   // must support GPU instancing
    public float    boidScale = 0.2f;

    [Header("Behaviour")]
    [Range(0.1f, 5f)]  public float separationRadius  = 1.2f;
    [Range(0.1f, 10f)] public float alignmentRadius   = 2.5f;
    [Range(0.1f, 10f)] public float cohesionRadius    = 3.5f;

    [Range(0f, 10f)]   public float separationWeight  = 3.0f;
    [Range(0f, 10f)]   public float alignmentWeight   = 1.0f;
    [Range(0f, 10f)]   public float cohesionWeight    = 1.0f;

    [Range(0.5f, 20f)] public float maxSpeed          = 5f;
    [Range(0.1f, 5f)]  public float minSpeed          = 1f;

    [Header("Bounds")]
    public Vector3 boundsSize    = new Vector3(40, 20, 40);
    public float   boundsSteering = 5f;

    struct BoidData
    {
        public Vector3 position;
        public Vector3 velocity;
    }

    const int STRIDE = sizeof(float) * 6; // 3 (pos) + 3 (vel)

    ComputeBuffer boidBuffer;
    ComputeBuffer argsBuffer;

    int kernelIndex;
    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    void Start()
    {
        InitBuffers();
        InitArgsBuffer();
    }

    void Update()
    {
        DispatchCompute();
        DrawInstanced();
    }

    void OnDestroy()
    {
        boidBuffer?.Release();
        argsBuffer?.Release();
    }

    void InitBuffers()
    {
        BoidData[] initialData = new BoidData[boidCount];
        for (int i = 0; i < boidCount; i++)
        {
            initialData[i].position = Random.insideUnitSphere * spawnRadius;
            initialData[i].velocity = Random.onUnitSphere * ((maxSpeed + minSpeed) * 0.5f);
        }

        boidBuffer = new ComputeBuffer(boidCount, STRIDE);
        boidBuffer.SetData(initialData);

        kernelIndex = boidsComputeShader.FindKernel("UpdateBoids");
        boidsComputeShader.SetBuffer(kernelIndex, "boids", boidBuffer);

        // Pass the same buffer to the material so the shader can read positions
        boidMaterial.SetBuffer("_BoidsBuffer", boidBuffer);
    }

    void InitArgsBuffer()
    {
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint),
                                       ComputeBufferType.IndirectArguments);
        if (boidMesh != null)
        {
            args[0] = boidMesh.GetIndexCount(0);
            args[1] = (uint)boidCount;
            args[2] = boidMesh.GetIndexStart(0);
            args[3] = boidMesh.GetBaseVertex(0);
        }
        argsBuffer.SetData(args);
    }

    void DispatchCompute()
    {
        boidsComputeShader.SetInt   ("boidCount",         boidCount);
        boidsComputeShader.SetFloat ("deltaTime",         Time.deltaTime);

        boidsComputeShader.SetFloat ("separationRadius",  separationRadius);
        boidsComputeShader.SetFloat ("alignmentRadius",   alignmentRadius);
        boidsComputeShader.SetFloat ("cohesionRadius",    cohesionRadius);

        boidsComputeShader.SetFloat ("separationWeight",  separationWeight);
        boidsComputeShader.SetFloat ("alignmentWeight",   alignmentWeight);
        boidsComputeShader.SetFloat ("cohesionWeight",    cohesionWeight);

        boidsComputeShader.SetFloat ("maxSpeed",          maxSpeed);
        boidsComputeShader.SetFloat ("minSpeed",          minSpeed);

        boidsComputeShader.SetVector("boundsSize",        boundsSize);
        boidsComputeShader.SetFloat ("boundsSteering",    boundsSteering);
        
        boidsComputeShader.SetFloat("maxSteerForce", 3f);

        int groups = Mathf.CeilToInt(boidCount / 64f);
        boidsComputeShader.Dispatch(kernelIndex, groups, 1, 1);
    }

    void DrawInstanced()
    {
        if (boidMesh == null || boidMaterial == null) return;

        boidMaterial.SetFloat("_Scale", boidScale);

        Bounds drawBounds = new Bounds(Vector3.zero, boundsSize * 2f);
        
        
        
        Graphics.DrawMeshInstancedIndirect(
            boidMesh,
            0,
            boidMaterial,
            drawBounds,
            argsBuffer
        );
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        Gizmos.DrawWireCube(Vector3.zero, boundsSize);
    }
}
