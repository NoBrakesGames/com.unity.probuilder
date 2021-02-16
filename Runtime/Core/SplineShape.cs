using System;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Splines;
using Unity.Mathematics;
using UnityEditor;
using Spline = UnityEngine.Splines.Spline;

[AddComponentMenu("")]
[DisallowMultipleComponent, ExcludeFromPreset, ExcludeFromObjectFactory]
[RequireComponent(typeof(ProBuilderMesh))]
[RequireComponent(typeof(SplineContainer))]

public class SplineShape : MonoBehaviour
{
    [Serializable]
    public struct FloatKeyFrame
    {
        public float Index;
        public float Value;
    }

    // todo "radius" should be in a data buffer
    [Min(0.01f)]
    public float m_Radius = 0.25f;

    [Range(3,36)]
    public int m_SidesCount = 4;

    [Min(0.05f)]
    public float m_SegmentsLength = 0.5f;

    public bool m_ClosedSpline = false;

    public bool m_UseEndCaps = true;

    public FloatKeyFrame[] m_RadiusBufferData;

    SplineContainer m_SplineContainer;
    Spline m_Spline;

    public Spline spline => m_Spline;

    ProBuilderMesh m_Mesh;

    public ProBuilderMesh mesh
    {
        get
        {
            if (m_Mesh == null)
                m_Mesh = GetComponent<ProBuilderMesh>();

            return m_Mesh;
        }

        set
        {
            m_Mesh = value;
        }
    }

    void OnValidate()
    {
        //if(m_RadiusBufferData != null)
        //    Array.Sort(m_RadiusBufferData, delegate(FloatKeyFrame x, FloatKeyFrame y) { return x.Index.CompareTo(y.Index);});

        if(m_Spline != null)
            m_Spline.Closed = m_ClosedSpline;
        UpdateSplineMesh();
    }

    public void Init()
    {
        m_SplineContainer = GetComponent<SplineContainer>();
        m_Spline = m_SplineContainer.Spline;
        m_Spline.EditType = SplineType.Bezier;
        m_SplineContainer.Spline.changed += SplineChanged;

        Refresh();
    }

    void SplineChanged()
    {
        var newKnotPos = m_SplineContainer.Spline[m_SplineContainer.Spline.KnotCount - 1].Position;
        var length = SplineUtility.CalculateSplineLength(m_Spline);
        if(math.length(newKnotPos) > 0.0f && length > 0.0f)
            UpdateSplineMesh();
    }

    void UpdateSplineMesh()
    {
        if(m_Radius > 0
           && m_SidesCount > 0
           && m_SegmentsLength > 0)
        {
            Refresh();
        }
    }

    /// <summary>
    /// Rebuild the ProBuilderMesh with the extruded spline.
    /// </summary>
    public void Refresh()
    {
        if(m_SplineContainer == null)
            return;

        if (m_SplineContainer.Spline.KnotCount < 2)
        {
            mesh.Clear();
            mesh.ToMesh();
            mesh.Refresh();
        }
        else
        {
            mesh.Clear();
            UpdateMesh();
        }
    }

    void UpdateMesh()
    {
        float length = SplineUtility.CalculateSplineLength(m_Spline);
        if(length == 0)
        {
            mesh.ToMesh();
            return;
        }

        Vector2[] circle = new Vector2[m_SidesCount];
        float radialStepAngle = 360f / m_SidesCount;
        // get a circle

        for (int i = 0; i < m_SidesCount; i++)
        {
            float angle0 = radialStepAngle * i * Mathf.Deg2Rad;

            float x = Mathf.Cos(angle0);
            float y = Mathf.Sin(angle0);

            circle[i] = new Vector2(x, y);
        }

        int segmentsCount = (int) (length / m_SegmentsLength) + 1;

        var vertexCount = m_SidesCount * ( segmentsCount + 1 );
        var faceCount = m_Spline.Closed ?
                                m_SidesCount * 2 * ( segmentsCount + 1 )
                                : m_SidesCount * 2 * segmentsCount;

        if(!m_Spline.Closed && m_UseEndCaps)
        {
            vertexCount += 2;
            faceCount += 2 * m_SidesCount;
        }

        Vector3[] vertices = new Vector3[vertexCount];
        Face[] faces = new Face[faceCount];

        int vertexIndex = 0;
        for(int i = 0; i < segmentsCount + 1; i++)
        {
            var index = (float)i / (float)segmentsCount;
            if(index > 1)
                index = 1f;

            var center = SplineUtility.EvaluateSplinePosition(m_Spline,index);
            float3 tangent = SplineUtility.EvaluateSplineDirection(m_Spline,index);

            var rightDir = math.normalize(math.cross(new float3(0, 1, 0), tangent));
            var upDir = math.normalize(math.cross(tangent, rightDir));

            float radius;
            if(!Evaluate(m_RadiusBufferData, index, out radius))
                radius = m_Radius;

            if(radius < 0.01f)
                radius = 0.01f;

            for(int j = 0; j < m_SidesCount; j++)
                vertices[vertexIndex++] = (Vector3) center
                                          + radius * circle[j].x * (Vector3) rightDir
                                          + radius * circle[j].y * (Vector3) upDir;

            if(!m_Spline.Closed && m_UseEndCaps)
            {
                if(i == 0)
                    vertices[vertexCount - 2] = center;
                if(i == segmentsCount)
                    vertices[vertexCount - 1] = center;
            }
        }

        var maxSegmentCount = m_Spline.Closed ? segmentsCount + 1 : segmentsCount;
        for(int i = 0; i < maxSegmentCount; i++)
        {
            for(int j = 0; j < m_SidesCount; j++)
            {
                faces[2 * (j + i * m_SidesCount)] =
                    new Face(
                        new int[3]
                        {
                            (j + i * m_SidesCount)%vertices.Length,
                            ((j + 1)%m_SidesCount + i * m_SidesCount)%vertices.Length,
                            (j + (i + 1) * m_SidesCount)%vertices.Length
                        });
                faces[2 * (j + i * m_SidesCount) + 1] =
                    new Face(new int[3]
                    {
                        ((j + 1)%m_SidesCount + i * m_SidesCount)%vertices.Length,
                        ((j + 1)%m_SidesCount + (i + 1) * m_SidesCount)%vertices.Length,
                        (j + (i + 1) * m_SidesCount)%vertices.Length
                    });
            }
        }

        if(!m_Spline.Closed && m_UseEndCaps)
        {
            var offset = m_SidesCount * 2 * segmentsCount;
            //Build end caps
            //Start cap
            for(int i = 0; i < m_SidesCount; i++)
            {
                faces[offset + i] = new Face(new int[3] { vertexCount-2, (i+1)%(m_SidesCount), i });
            }

            //End cap
            offset += m_SidesCount;
            for(int i = 0; i < m_SidesCount; i++)
            {
                faces[offset + i] = new Face(new int[3] { vertexCount - 1,
                    vertexCount - 2 - m_SidesCount + i,
                    vertexCount - 2 - m_SidesCount + (i + 1)%m_SidesCount});
            }
        }

        mesh.RebuildWithPositionsAndFaces(vertices, faces);
        mesh.ToMesh();
        mesh.Refresh();
    }

    bool Evaluate(FloatKeyFrame[] frames, float index, out float result)
    {
        result = 0.01f;
        if(frames == null || frames.Length == 0)
            return false;

        FloatKeyFrame[] framesCopy = new FloatKeyFrame[frames.Length];
        Array.Copy(frames,framesCopy,frames.Length);
        Array.Sort(framesCopy, delegate(FloatKeyFrame x, FloatKeyFrame y) { return x.Index.CompareTo(y.Index);});

        if(index < framesCopy[0].Index)
            result = framesCopy[0].Value;
        else if(index > framesCopy[frames.Length - 1].Index)
        {
            if(!m_Spline.Closed)
                result = framesCopy[frames.Length - 1].Value;
            else
            {
                var lerpFactor = ( index - framesCopy[frames.Length - 1].Index ) / ( 1f - framesCopy[frames.Length - 1].Index );
                result = Mathf.Lerp(framesCopy[frames.Length - 1].Value, framesCopy[0].Value, lerpFactor);
            }
        }
        else
        {
            for(int i = 1; i < framesCopy.Length; i++)
            {

                if(framesCopy[i].Index < index)
                    continue;

                var lerpFactor = ( index - framesCopy[i-1].Index ) / ( framesCopy[i].Index - framesCopy[i-1].Index );
                result = Mathf.Lerp(framesCopy[i-1].Value, framesCopy[i].Value, lerpFactor);
                break;
            }
        }

        return true;
    }
}
