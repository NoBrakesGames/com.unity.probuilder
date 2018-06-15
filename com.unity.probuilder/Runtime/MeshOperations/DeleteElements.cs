using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.ProBuilder;

namespace UnityEngine.ProBuilder.MeshOperations
{
	/// <summary>
	/// Functions for removing vertexes and triangles from a mesh.
	/// </summary>
	public static class DeleteElements
	{
		/// <summary>
		/// Removes vertexes that no face references.
		/// </summary>
		/// <param name="mesh">The source mesh.</param>
		/// <returns>A list of deleted vertex indexes.</returns>
		public static int[] RemoveUnusedVertexes(this ProBuilderMesh mesh)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

			List<int> del = new List<int>();
			HashSet<int> tris = new HashSet<int>(mesh.facesInternal.SelectMany(x => x.indexes));

			for(int i = 0; i < mesh.positionsInternal.Length; i++)
				if(!tris.Contains(i))
					del.Add(i);

			mesh.DeleteVertexes(del);

			return del.ToArray();
		}

		/// <summary>
		/// Deletes the vertexes from the passed index array, and handles rebuilding the sharedIndexes array.
		/// </summary>
		/// <remarks>This function does not retriangulate the mesh. Ie, you are responsible for ensuring that indexes
		/// deleted by this function are not referenced by any triangles.</remarks>
		/// <param name="mesh">The source mesh.</param>
		/// <param name="distinctIndexes">A list of vertexes to delete. Note that this must not contain duplicates.</param>
		public static void DeleteVertexes(this ProBuilderMesh mesh, IEnumerable<int> distinctIndexes)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            if (distinctIndexes == null || !distinctIndexes.Any())
				return;

			Vertex[] vertexes = Vertex.GetVertexes(mesh);
			int originalVertexCount = vertexes.Length;
			int[] offset = new int[originalVertexCount];

			List<int> sorted = new List<int>(distinctIndexes);

			sorted.Sort();

			vertexes = vertexes.SortedRemoveAt(sorted);

			// Add 1 because NearestIndexPriorToValue is 0 indexed.
			for(int i = 0; i < originalVertexCount; i++)
				offset[i] = ArrayUtility.NearestIndexPriorToValue(sorted, i) + 1;

			foreach(Face face in mesh.facesInternal)
			{
				int[] indexes = face.indexesInternal;

				for(int i = 0; i < indexes.Length; i++)
					indexes[i] -= offset[indexes[i]];

				face.InvalidateCache();
			}

			// remove from sharedIndexes & shift to account for deletions
			IEnumerable<KeyValuePair<int, int>> common = mesh.sharedIndexesInternal.ToDictionary().Where(x => sorted.BinarySearch(x.Key) < 0).Select(y => new KeyValuePair<int, int>(y.Key - offset[y.Key], y.Value));
			IEnumerable<KeyValuePair<int, int>> commonUV = mesh.sharedIndexesUVInternal.ToDictionary().Where(x => sorted.BinarySearch(x.Key) < 0).Select(y => new KeyValuePair<int, int>(y.Key - offset[y.Key], y.Value));

			mesh.SetVertexes(vertexes);
			mesh.SetSharedIndexes(common);
			mesh.SetSharedIndexesUV(commonUV);
		}

		/// <summary>
		/// Removes a face from a mesh.
		/// </summary>
		/// <param name="mesh">The source mesh.</param>
		/// <param name="face">The face to remove.</param>
		/// <returns>An array of vertex indexes that were deleted as a result of face deletion.</returns>
		public static int[] DeleteFace(this ProBuilderMesh mesh, Face face)
		{
			return DeleteFaces(mesh, new Face[] { face });
		}

		/// <summary>
		/// Delete a collection of faces from a mesh.
		/// </summary>
		/// <param name="mesh">The source mesh.</param>
		/// <param name="faces">The faces to remove.</param>
		/// <returns>An array of vertex indexes that were deleted as a result of deletion.</returns>
		public static int[] DeleteFaces(this ProBuilderMesh mesh, IEnumerable<Face> faces)
		{
			return DeleteFaces(mesh, faces.Select(x => System.Array.IndexOf(mesh.facesInternal, x)).ToList());
		}

		/// <summary>
		/// Delete a collection of faces from a mesh.
		/// </summary>
		/// <param name="mesh">The source mesh.</param>
		/// <param name="faceIndexes">The indexes of faces to remove (corresponding to the @"UnityEngine.ProBuilder.ProBuilderMesh.faces" collection.</param>
		/// <returns>An array of vertex indexes that were deleted as a result of deletion.</returns>
		public static int[] DeleteFaces(this ProBuilderMesh mesh, IList<int> faceIndexes)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            if (faceIndexes == null)
                throw new ArgumentNullException("faceIndexes");

			Face[] faces = new Face[faceIndexes.Count];

			for(int i = 0; i < faces.Length; i++)
				faces[i] = mesh.facesInternal[faceIndexes[i]];

			List<int> indexesToRemove = faces.SelectMany(x => x.distinctIndexesInternal).Distinct().ToList();
			indexesToRemove.Sort();

			int vertexCount = mesh.positionsInternal.Length;

			Vector3[] verts = mesh.positionsInternal.SortedRemoveAt(indexesToRemove);
			Color[] cols = mesh.colorsInternal.SortedRemoveAt(indexesToRemove);
			Vector2[] uvs = mesh.texturesInternal.SortedRemoveAt(indexesToRemove);
			Face[] nFaces = mesh.facesInternal.RemoveAt(faceIndexes);

			Dictionary<int, int> shiftmap = new Dictionary<int, int>();

			for(var i = 0;  i < vertexCount; i++)
				shiftmap.Add(i, ArrayUtility.NearestIndexPriorToValue<int>(indexesToRemove, i) + 1);

			// shift all other face indexes down to account for moved vertex positions
			for(var i = 0; i < nFaces.Length; i++)
			{
				int[] tris = nFaces[i].indexesInternal;

				for(var n = 0; n < tris.Length; n++)
					tris[n] -= shiftmap[tris[n]];

				nFaces[i].indexesInternal = tris;
			}

			// shift all other face indexes in the shared index array down to account for moved vertex positions
			IntArray[] si = mesh.sharedIndexesInternal;
			IntArray[] si_uv = mesh.sharedIndexesUVInternal;

			IntArrayUtility.RemoveValuesAndShift(ref si, indexesToRemove);
			if(si_uv != null) IntArrayUtility.RemoveValuesAndShift(ref si_uv, indexesToRemove);

			mesh.sharedIndexesInternal = si;
			mesh.sharedIndexesUVInternal = si_uv;
			mesh.positionsInternal = verts;
			mesh.colorsInternal = cols;
			mesh.texturesInternal = uvs;
			mesh.facesInternal = nFaces;

			int[] array = indexesToRemove.ToArray();

			return array;
		}

		/// <summary>
		/// Iterates through all faces in a mesh and removes triangles with an area less than float.Epsilon, or with indexes that point to the same vertex.
		/// </summary>
		/// <param name="mesh">The source mesh.</param>
		/// <returns>The number of vertexes deleted as a result of the degenerate triangle cleanup.</returns>
		public static int[] RemoveDegenerateTriangles(this ProBuilderMesh mesh)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

			Dictionary<int, int> m_Lookup = mesh.sharedIndexesInternal.ToDictionary();
			Dictionary<int, int> m_LookupUV = mesh.sharedIndexesUVInternal != null ? mesh.sharedIndexesUVInternal.ToDictionary() : new Dictionary<int, int>();
			Vector3[] m_Positions = mesh.positionsInternal;
			Dictionary<int, int> m_RebuiltLookup = new Dictionary<int, int>();
			Dictionary<int, int> m_RebuiltLookupUV = new Dictionary<int, int>();
			List<Face> m_RebuiltFaces = new List<Face>();

			foreach(Face face in mesh.facesInternal)
			{
				List<int> tris = new List<int>();

				int[] ind = face.indexesInternal;

				for(int i = 0; i < ind.Length; i+=3)
				{
					float area = Math.TriangleArea(m_Positions[ind[i+0]], m_Positions[ind[i+1]], m_Positions[ind[i+2]]);

					if(area > Mathf.Epsilon)
					{
						int a = m_Lookup[ind[i  ]],
							b = m_Lookup[ind[i+1]],
							c = m_Lookup[ind[i+2]];

						if( !(a == b || a == c || b == c) )
						{
							tris.Add(ind[i+0]);
							tris.Add(ind[i+1]);
							tris.Add(ind[i+2]);

							if(!m_RebuiltLookup.ContainsKey(ind[i  ]))
								m_RebuiltLookup.Add(ind[i  ], a);
							if(!m_RebuiltLookup.ContainsKey(ind[i+1]))
								m_RebuiltLookup.Add(ind[i+1], b);
							if(!m_RebuiltLookup.ContainsKey(ind[i+2]))
								m_RebuiltLookup.Add(ind[i+2], c);

							if(m_LookupUV.ContainsKey(ind[i]) && !m_RebuiltLookupUV.ContainsKey(ind[i]))
								m_RebuiltLookupUV.Add(ind[i], m_LookupUV[ind[i]]);
							if(m_LookupUV.ContainsKey(ind[i+1]) && !m_RebuiltLookupUV.ContainsKey(ind[i+1]))
								m_RebuiltLookupUV.Add(ind[i+1], m_LookupUV[ind[i+1]]);
							if(m_LookupUV.ContainsKey(ind[i+2]) && !m_RebuiltLookupUV.ContainsKey(ind[i+2]))
								m_RebuiltLookupUV.Add(ind[i+2], m_LookupUV[ind[i+2]]);
						}
					}
				}

				if(tris.Count > 0)
				{
					face.indexesInternal = tris.ToArray();
					m_RebuiltFaces.Add(face);
				}
			}

			mesh.faces = m_RebuiltFaces;
			mesh.SetSharedIndexes(m_RebuiltLookup);
			mesh.SetSharedIndexesUV(m_RebuiltLookupUV);
			return mesh.RemoveUnusedVertexes();
		}

	}
}
