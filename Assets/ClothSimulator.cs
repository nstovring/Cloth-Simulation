﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ClothSimulator : MonoBehaviour {

    public ComputeShader clothComputeShader;
    public int count = 64;//32768//16384
    int mainClothKernelHandler;
    //int springKernelHandler;

    ComputeBuffer particleBuffer;
    ComputeBuffer springBuffer;
    ComputeBuffer initialPosBuffer;

    public Material clothMaterial;

    int springCount;
    //[Range(1,50)]
    [Delayed]
    public float globalStiffness;
    [Delayed]
    [Range(0,100)]
    public float damping;
    [Range(0.1f, 1f)]
    public float mass;
    public float gravityMul = 1f;
    public Transform clothHandler;
    public Transform sphereCollider;

    [System.Serializable]
    public class SpringVariables
    {
        public float damping = 0.5f;
        public float stiffness = 7;
    }

  
    [SerializeField]
    public SpringVariables structuralSpringVars;
    public SpringVariables shearSpringVars;
    public SpringVariables structuralBendingSpringVars;
    public SpringVariables shearBendingSpringVars;


    // Use this for initialization
    void Start () {
        mainClothKernelHandler = clothComputeShader.FindKernel("CSMain");
        int particleStructSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(SpringHandler.particle));
        int springStructSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(SpringHandler.spring));

        SpringHandler.particle[] particles = new SpringHandler.particle[count];
        List<SpringHandler.spring> springs = new List<SpringHandler.spring>();

        Vector3[] initialPositions = new Vector3[count];
        int rows = (int) Mathf.Sqrt(count);
        int collumns = (int)Mathf.Sqrt(count);

        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].structuralSprings.x = -1;
            particles[i].structuralSprings.y = -1;
            particles[i].structuralSprings.z = -1;
            particles[i].structuralSprings.w = -1;
            particles[i].shearSprings.x = -1;
            particles[i].shearSprings.y = -1;
            particles[i].shearSprings.z = -1;
            particles[i].shearSprings.w = -1;
            particles[i].structutralBendingSprings.x = -1;
            particles[i].structutralBendingSprings.y = -1;
            particles[i].structutralBendingSprings.z = -1;
            particles[i].structutralBendingSprings.w = -1;
            particles[i].shearBendingSprings.x = -1;
            particles[i].shearBendingSprings.y = -1;
            particles[i].shearBendingSprings.z = -1;
            particles[i].shearBendingSprings.w = -1;
        }
        //Double for loop intializes all values within particle array
        for (int x = 0; x < collumns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                particles[y + x * rows].mass = mass;
                particles[y + x * rows].iD = y + x * rows;
                particles[y + x * rows].position = transform.TransformPoint(new Vector3(x, 0, y));
                particles[y + x * rows].isFixed = 0;
                initialPositions[y + x * rows] = transform.position + transform.InverseTransformPoint(new Vector3(x, 0, y ));
                //Add springs get the current index and finds every other particle which should connect to this one with a spring
                AddSprings(x, y, rows, particles, ref springs);
            }
        }

        for (int i = 0; i < (collumns * rows); i+= rows)
        {
            particles[i].isFixed = 1;
        }

        particleBuffer = new ComputeBuffer(count, particleStructSize, ComputeBufferType.Default);
        springBuffer = new ComputeBuffer(springs.Count, springStructSize, ComputeBufferType.Default);
        initialPosBuffer = new ComputeBuffer(count, sizeof(float) * 3, ComputeBufferType.Default);

        springCount = springs.Count;
        initialPosBuffer.SetData(initialPositions);
        particleBuffer.SetData(particles);
        springBuffer.SetData(springs.ToArray());


        clothComputeShader.SetBuffer(mainClothKernelHandler, "initialPositions", initialPosBuffer);
        clothComputeShader.SetBuffer(mainClothKernelHandler, "particles", particleBuffer);
        clothComputeShader.SetBuffer(mainClothKernelHandler, "springs", springBuffer);
        clothMaterial.SetBuffer("particleBuffer", particleBuffer);
        clothMaterial.SetInt("count", count);

        CommandBuffer cm = new CommandBuffer();

        cm.DrawProcedural(Matrix4x4.identity, clothMaterial, -1, MeshTopology.Points, count);
        Camera.main.AddCommandBuffer(CameraEvent.AfterSkybox, cm);
#if UNITY_EDITOR
        Camera[] cams = UnityEditor.SceneView.GetAllSceneCameras();
        for (int i = 0; i < cams.Length; i++)
        {
            cams[i].AddCommandBuffer(CameraEvent.AfterForwardOpaque, cm);
        }
#endif
    }

    struct int2
    {
        public int2(params int[] args)
        {
            x = args[0];
            y = args[1];
        }
        public int x;
        public int y;
    }

    void AddSprings(int x, int y, int rows, SpringHandler.particle[] particles,ref List<SpringHandler.spring> springs)
    {
        //Get Structural springs
        if (y < rows - 1)
        {
            particles[y + x * rows].structuralSprings.x = springs.Count;
            int connectedParticleIndex;
            springs.Add(SpringHandler.GetSpring(x, y, rows,out connectedParticleIndex, 0,1, structuralSpringVars, SpringHandler.SpringType.StructuralSpring));
            particles[connectedParticleIndex].structuralSprings.w = springs.Count-1;
        }
        
        if (x < rows -1)
        {
            particles[y + x * rows].structuralSprings.y = springs.Count;
            int connectedParticleIndex;
            springs.Add(SpringHandler.GetSpring(x, y, rows,out connectedParticleIndex, 1, 0, structuralSpringVars, SpringHandler.SpringType.StructuralSpring));
            particles[connectedParticleIndex].structuralSprings.z = springs.Count-1;
        }
        //Get Shear Springs
        if (y < rows - 1 && x < rows - 1)
        {
            particles[y + x * rows].shearSprings.x = springs.Count;
            int connectedParticleIndex;
            springs.Add(SpringHandler.GetSpring(x, y, rows, out connectedParticleIndex, 1, 1, shearSpringVars, SpringHandler.SpringType.ShearSpring));
            particles[connectedParticleIndex].shearSprings.z = springs.Count - 1;
        }
        
        if (y < rows - 1 && x > 0)
        {
            particles[y + x * rows].shearSprings.y = springs.Count;
            int connectedParticleIndex;
            springs.Add(SpringHandler.GetSpring(x, y, rows, out connectedParticleIndex, -1, 1, shearSpringVars, SpringHandler.SpringType.ShearSpring));
            particles[connectedParticleIndex].shearSprings.w = springs.Count - 1;
        }

        //Get shearBendingSprings
        int BendRange = 2;
        if (y < rows - BendRange && x < rows - BendRange)
        {
            particles[y + x * rows].shearBendingSprings.x = springs.Count;
            int connectedParticleIndex;
            springs.Add(SpringHandler.GetSpring(x, y, rows, out connectedParticleIndex, BendRange, BendRange, shearBendingSpringVars, SpringHandler.SpringType.ShearBendingSpring));
            particles[connectedParticleIndex].shearBendingSprings.z = springs.Count - 1;
        }
        
        if (y < rows - BendRange && x > BendRange - 1)
        {
            particles[y + x * rows].shearBendingSprings.y = springs.Count;
            int connectedParticleIndex;
            springs.Add(SpringHandler.GetSpring(x, y, rows, out connectedParticleIndex, -BendRange, BendRange, shearBendingSpringVars, SpringHandler.SpringType.ShearBendingSpring));
            particles[connectedParticleIndex].shearBendingSprings.w = springs.Count - 1;
        }
        
        //Get structutralBendingSprings
        if (y < rows - BendRange)
        {
            particles[y + x * rows].structutralBendingSprings.x = springs.Count;
            int connectedParticleIndex;
            springs.Add(SpringHandler.GetSpring(x, y, rows, out connectedParticleIndex, 0, BendRange, structuralBendingSpringVars, SpringHandler.SpringType.StructuralBendingSpring));
            particles[connectedParticleIndex].structutralBendingSprings.z = springs.Count - 1;
        }
        
        if (x < rows - BendRange)
        {
            particles[y + x * rows].structutralBendingSprings.y = springs.Count;
            int connectedParticleIndex;
            springs.Add(SpringHandler.GetSpring(x, y, rows, out connectedParticleIndex, BendRange, 0, structuralBendingSpringVars, SpringHandler.SpringType.StructuralBendingSpring));
            particles[connectedParticleIndex].structutralBendingSprings.w = springs.Count - 1;
        }

    }


    void DrawConnections(SpringHandler.particle[] ps, SpringHandler.spring[] ss, int i)
    {
        if (drawStructuralSprings)
        {
            SpringHandler.DrawStructuralSprings(ps, ss, i);
        }
        if (drawShearSprings)
        {

            SpringHandler.DrawShearSprings(ps, ss, i);

        }
        if (drawStructuralBendingSprings)
        {
            SpringHandler.DrawStructuralBendingSprings(ps, ss, i);

        }
        if (drawShearBendingSprings)
        {
            SpringHandler.DrawShearBendingSprings(ps, ss, i);
        }
    }

    int solverSteps = 16;
    void Update () {
       

        if (clothHandler != null)
        {
            clothComputeShader.SetVector("fixedPos", clothHandler.transform.position);
        }
        clothComputeShader.SetFloat("globalStiffness", globalStiffness);
        clothComputeShader.SetFloat("globalDamping", damping);
        clothComputeShader.SetFloat("mass", mass);
        clothComputeShader.SetFloat("gravityMul", gravityMul);
        clothComputeShader.SetVector("spherePos", sphereCollider.transform.position);
        clothComputeShader.SetFloat("sphereRadius", sphereCollider.transform.lossyScale.y/2);

        for (int i = 0; i < solverSteps; i++)
        {
            clothComputeShader.SetFloat("deltaTime", Time.deltaTime/ solverSteps);
            clothComputeShader.Dispatch(mainClothKernelHandler, count / 8, 1, 1);
        }

        SpringHandler.particle[] ps = new SpringHandler.particle[count];
        SpringHandler.spring[] ss = new SpringHandler.spring[springCount];
        
        particleBuffer.GetData(ps);
        springBuffer.GetData(ss);

        if (drawCloth)
        {
            DrawAllConnections(ps, ss);
        }
        if (drawVelocities)
        {
            SpringHandler.DrawVelocity(ps);
        }

        if (printDebug)
        {
            Print("Velocities", ps);
            printDebug = false;
        }

        for (int i = 0; i < ss.Length; i++)
        {
            SpringHandler.spring s = ss[i];
            switch((SpringHandler.SpringType)s.springType)
            {
                case (SpringHandler.SpringType.StructuralSpring):
                    s = SpringHandler.ApplyChanges(s, structuralSpringVars);
                    break;
                case (SpringHandler.SpringType.ShearSpring):
                    s = SpringHandler.ApplyChanges(s, shearSpringVars);
                    break;
                case (SpringHandler.SpringType.StructuralBendingSpring):
                    s = SpringHandler.ApplyChanges(s, structuralBendingSpringVars);
                    break;
                case (SpringHandler.SpringType.ShearBendingSpring):
                    s = SpringHandler.ApplyChanges(s, shearBendingSpringVars);
                    break;
            }
            ss[i] = s;
        }

        particleBuffer.SetData(ps);
        springBuffer.SetData(ss);

    }
    public bool printDebug = false;
    public bool drawCloth = true;
    public bool drawVelocities = true;
    public bool drawStructuralSprings = false;
    public bool drawShearSprings = false;
    public bool drawStructuralBendingSprings = false;
    public bool drawShearBendingSprings = false;
    public bool showForSingleParticle = false;
    [Range(0, 511)]
    public int selectedParticle = 0;

    void ApplyForce(ref SpringHandler.particle[] ps, SpringHandler.spring[] ss)
    {
        for (int i = 0; i < count; i++)
        {
            SpringHandler.particle p = ps[i];
            if (!(p.isFixed == 1))
            {
                Vector3 gravity = new Vector3(0, -5f, 0);
                float mass = p.mass;
                Vector3 dampingForce = new Vector3(0, 0, 0);
                Vector3 springForce = new Vector3(0, 0, 0);
                Vector3 tempSpringForce = new Vector3(0, 0, 0);
                Vector3 tempdampingForce = new Vector3(0, 0, 0);
        
                if (ps[i].structuralSprings.x != -1) {
                    SpringHandler.GetSpringForce(ref ss,ref ps,out tempdampingForce, out tempSpringForce, ps[i], ps[i].structuralSprings.x);
                    dampingForce += tempdampingForce;
                    springForce += tempSpringForce;
                }
        
                if (ps[i].structuralSprings.y != -1) {
                    SpringHandler.GetSpringForce(ref ss, ref ps, out tempdampingForce, out tempSpringForce, ps[i], ps[i].structuralSprings.y);
                    dampingForce += tempdampingForce;
                    springForce += tempSpringForce;
                }
        
                if (ps[i].structuralSprings.z != -1) {
                    SpringHandler.GetSpringForce(ref ss, ref ps, out tempdampingForce, out tempSpringForce, ps[i], ps[i].structuralSprings.z);
                    dampingForce += tempdampingForce;
                    springForce += tempSpringForce;
                }
        
                if (ps[i].structuralSprings.w != -1) {
                    SpringHandler.GetSpringForce(ref ss, ref ps, out tempdampingForce, out tempSpringForce, ps[i], ps[i].structuralSprings.w);
                    dampingForce += tempdampingForce;
                    springForce += tempSpringForce;
                }
        
                Vector3 force = dampingForce + springForce + mass * gravity;
                Vector3 acceleration = force / mass;
        
        
                p.velocity = p.velocity + acceleration * Time.deltaTime;
                p.position = p.position + p.velocity * Time.deltaTime;
        
                ps[i].position = p.position;
                ps[i].velocity = p.velocity;
        
            }
        
        }
        
        
    }

    void DrawAllConnections(SpringHandler.particle[] ps, SpringHandler.spring[] ss)
    {
        if (showForSingleParticle)
        {
            DrawConnections(ps, ss, selectedParticle);
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                DrawConnections(ps, ss, i);
            }
        }
    }


    void Print(string name, SpringHandler.particle[] array)
    {
        string values = "";
        string problems = "";

        for (int i = 0; i < array.Length; i++)
        {
            //if ((i != 0) && (array[i - 1] > array[i]))
            //    problems += "Discontinuity found at " + i + "!! \n";

            values += "ID, " + array[i].iD +":" +  array[i].structuralSprings.x + ", " + array[i].structuralSprings.y + ", " + array[i].structuralSprings.z + ", " + array[i].structuralSprings.w + "\n";
        }

        Debug.Log(name + " :  " + values + "\n" + problems);
        Debug.Log("Count " + array.Length);

    }

   

    private void OnDestroy()
    {
        particleBuffer.Release();
        springBuffer.Release();
        initialPosBuffer.Release();
    }
}
