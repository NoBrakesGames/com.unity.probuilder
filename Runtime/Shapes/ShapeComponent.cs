﻿using System;

namespace UnityEngine.ProBuilder
{
    [RequireComponent(typeof(ProBuilderMesh))]
    public class ShapeComponent : MonoBehaviour
    {
        [SerializeReference]
        public Shape shape = new Cube();

        public bool isInit;

        ProBuilderMesh m_Mesh;

        [SerializeField]
        Vector3 m_Size;

        [HideInInspector]
        [SerializeField]
        Quaternion m_RotationQuaternion = Quaternion.identity;

        public Vector3 size {
            get { return m_Size; }
            set { m_Size = value; }
        } 

        public ProBuilderMesh mesh {
            get { return m_Mesh == null ? m_Mesh = GetComponent<ProBuilderMesh>() : m_Mesh; }
        }

        // Bounds where center is in world space, size is mesh.bounds.size
        internal Bounds meshFilterBounds {
            get {
                var mb = mesh.mesh.bounds;
                return new Bounds(transform.TransformPoint(mb.center), mb.size);
            }
        }

        public void Rebuild(Bounds bounds, Quaternion rotation)
        {
            size = Math.Abs(bounds.size);
            transform.position = bounds.center;
            transform.rotation = rotation;
            Rebuild();
        }

        public void Rebuild()
        {
            shape.RebuildMesh(mesh, size);
            SetRotation(m_RotationQuaternion);
            FitToSize();
        }

        public void SetShape(Shape shape)
        {
            this.shape = shape;
            Rebuild();
        }

        void FitToSize()
        {
            if (mesh.vertexCount < 1)
                return;

            var scale = size.DivideBy(mesh.mesh.bounds.size);
            if (scale == Vector3.one)
                return;

            var positions = mesh.positionsInternal;

            if (System.Math.Abs(mesh.mesh.bounds.size.x) < 0.001f)
                scale.x = 0;
            if (System.Math.Abs(mesh.mesh.bounds.size.y) < 0.001f)
                scale.y = 0;
            if (System.Math.Abs(mesh.mesh.bounds.size.z) < 0.001f)
                scale.z = 0;

            for (int i = 0, c = mesh.vertexCount; i < c; i++)
            {
                positions[i] -= mesh.mesh.bounds.center;
                positions[i].Scale(scale);
            }

            mesh.ToMesh();
            mesh.Rebuild();
        }

        /// <summary>
        /// Set the rotation of the Shape to a given set of eular angles, then rotates it
        /// </summary>
        /// <param name="eulerAngles">The angles to rotate by</param>
        public void SetRotation(Vector3 eulerAngles)
        {
            m_RotationQuaternion = Quaternion.Euler(eulerAngles);
            SetRotation(m_RotationQuaternion);
        }

        /// <summary>
        /// Rotates the Shape by a given set of eular angles
        /// </summary>
        /// <param name="eulerAngles">The angles to rotate by</param>
        public void Rotate(Vector3 eulerAngles)
        {
            Quaternion rotation = Quaternion.Euler(eulerAngles);
            m_RotationQuaternion = rotation * m_RotationQuaternion;
            SetRotation(m_RotationQuaternion);
            FitToSize();
        }

        /// <summary>
        /// Rotates the Shape by a given set of eular angles
        /// </summary>
        /// <param name="eulerAngles">The angles to rotate by</param>
        public void Rotate(Quaternion rotation)
        {
            if (rotation == Quaternion.identity)
            {
                return;
            }
            m_RotationQuaternion = rotation * m_RotationQuaternion;
            SetRotation(m_RotationQuaternion);
            FitToSize();
        }

        void SetRotation(Quaternion rotation)
        {
            if (rotation == Quaternion.identity)
            {
                return;
            }
            shape.RebuildMesh(mesh, size);

            var origVerts = mesh.positionsInternal;

            for (int i = 0; i < origVerts.Length; ++i)
            {
                origVerts[i] = rotation * origVerts[i];
            }
            mesh.mesh.vertices = origVerts;
            mesh.ReplaceVertices(origVerts);
        }
    }
}