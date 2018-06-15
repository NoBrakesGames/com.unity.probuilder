using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.ProBuilder;
using System;

namespace UnityEngine.ProBuilder.MeshOperations
{
	/// <summary>
	/// Functions for appending elements to meshes.
	/// </summary>
	public static class AppendElements
	{
		/// <summary>
		/// Append a new face to the ProBuilderMesh.
		/// </summary>
		/// <param name="mesh">The mesh target.</param>
		/// <param name="positions">The new vertex positions to add.</param>
		/// <param name="colors">The new colors to add (must match positions length).</param>
		/// <param name="uvs">The new uvs to add (must match positions length).</param>
		/// <param name="face">A face with the new triangle indexes. The indexes should be 0 indexed.</param>
		/// <returns>The new face as referenced on the mesh.</returns>
		public static Face AppendFace(this ProBuilderMesh mesh, Vector3[] positions, Color[] colors, Vector2[] uvs, Face face)
		{
            if (positions == null)
                throw new ArgumentNullException("positions");
			int[] shared = new int[positions.Length];
			for(int i = 0; i < positions.Length; i++)
				shared[i] = -1;
			return mesh.AppendFace(positions, colors, uvs, face, shared);
		}

		internal static Face AppendFace(this ProBuilderMesh mesh, Vector3[] positions, Color[] colors, Vector2[] uvs, Face face, int[] common)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            if (positions == null)
                throw new ArgumentNullException("positions");

            if (colors == null)
                throw new ArgumentNullException("colors");

            if (uvs == null)
                throw new ArgumentNullException("uvs");

            if (face == null)
                throw new ArgumentNullException("face");

            if (common == null)
                throw new ArgumentNullException("common");

			int vertexCount = mesh.vertexCount;

			Vector3[] newPositions = new Vector3[vertexCount + positions.Length];
			Color[] newColors = new Color[vertexCount + colors.Length];
			Vector2[] newTextures = new Vector2[mesh.texturesInternal.Length + uvs.Length];

			List<Face> faces = new List<Face>(mesh.facesInternal);
			IntArray[] sharedIndexes = mesh.sharedIndexesInternal;

			Array.Copy(mesh.positionsInternal, 0, newPositions, 0, vertexCount);
			Array.Copy(positions, 0, newPositions, vertexCount, positions.Length);
			Array.Copy(mesh.colorsInternal, 0, newColors, 0, vertexCount);
			Array.Copy(colors, 0, newColors, vertexCount, colors.Length);
			Array.Copy(mesh.texturesInternal, 0, newTextures, 0, mesh.texturesInternal.Length);
			Array.Copy(uvs, 0, newTextures, mesh.texturesInternal.Length, uvs.Length);

			face.ShiftIndexesToZero();
			face.ShiftIndexes(vertexCount);

			faces.Add(face);

			for(int i = 0; i < common.Length; i++)
				IntArrayUtility.AddValueAtIndex(ref sharedIndexes, common[i], i+vertexCount);

			mesh.positions = newPositions;
			mesh.colors = newColors;
			mesh.textures = newTextures;
			mesh.sharedIndexes = sharedIndexes;
			mesh.faces = faces;

			return face;
		}

		/// <summary>
		/// Append a group of new faces to the mesh. Significantly faster than calling AppendFace multiple times.
		/// </summary>
		/// <param name="mesh">The source mesh to append new faces to.</param>
		/// <param name="positions">An array of position arrays, where indexes correspond to the appendedFaces parameter.</param>
		/// <param name="colors">An array of colors arrays, where indexes correspond to the appendedFaces parameter.</param>
		/// <param name="uvs">An array of uvs arrays, where indexes correspond to the appendedFaces parameter.</param>
		/// <param name="faces">An array of faces arrays, which contain the triangle winding information for each new face. Face index values are 0 indexed.</param>
		/// <param name="shared">An optional mapping of each new vertex's common index. Common index refers to a triangle's index in the @"UnityEngine.ProBuilder.ProBuilderMesh.sharedIndexes" array. If this value is provided, it must contain entries for each vertex position. Ex, if there are 4 vertexes in this face, there must be shared index entries for { 0, 1, 2, 3 }.</param>
		/// <returns>An array of the new faces that where successfully appended to the mesh.</returns>
		public static Face[] AppendFaces(
			this ProBuilderMesh mesh,
			Vector3[][] positions,
			Color[][] colors,
			Vector2[][] uvs,
			Face[] faces,
			int[][] shared)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            if (positions == null)
                throw new ArgumentNullException("positions");

            if (colors == null)
                throw new ArgumentNullException("colors");

            if (uvs == null)
                throw new ArgumentNullException("uvs");

            if (faces == null)
                throw new ArgumentNullException("faces");

            var newPositions = new List<Vector3>(mesh.positionsInternal);
			var newColors = new List<Color>(mesh.colorsInternal);
			var newTextures = new List<Vector2>(mesh.texturesInternal);
			var newFaces = new List<Face>(mesh.facesInternal);
			IntArray[] sharedIndexes = mesh.sharedIndexesInternal;

			int vc = mesh.vertexCount;

			for(int i = 0; i < faces.Length; i++)
			{
				newPositions.AddRange(positions[i]);
				newColors.AddRange(colors[i]);
				newTextures.AddRange(uvs[i]);

				faces[i].ShiftIndexesToZero();
				faces[i].ShiftIndexes(vc);
				newFaces.Add(faces[i]);

				if(shared != null && positions[i].Length != shared[i].Length)
				{
					Debug.LogError("Append Face failed because shared array does not match new vertex array.");
					return null;
				}

				if(shared != null)
				{
					for(int j = 0; j < shared[i].Length; j++)
					{
						IntArrayUtility.AddValueAtIndex(ref sharedIndexes, shared[i][j], j+vc);
					}
				}
				else
				{
					for(int j = 0; j < positions[i].Length; j++)
					{
						IntArrayUtility.AddValueAtIndex(ref sharedIndexes, -1, j+vc);
					}
				}

				vc = newPositions.Count;
			}

			mesh.positions = newPositions;
			mesh.colors = newColors;
			mesh.textures = newTextures;
			mesh.faces = newFaces;
			mesh.sharedIndexesInternal = sharedIndexes;

			return faces;
		}

	    /// <summary>
        /// Create a new face connecting existing vertexes.
        /// </summary>
        /// <param name="mesh">The source mesh.</param>
        /// <param name="indexes">The indexes of the vertexes to join with the new polygon.</param>
        /// <param name="unordered">Are the indexes in an ordered path (false), or not (true)? If indexes are not ordered this function will treat the polygon as a convex shape. Ordered paths will be triangulated allowing concave shapes.</param>
        /// <returns>The new face created if the action was successfull, null if action failed.</returns>
        public static Face CreatePolygon(this ProBuilderMesh mesh, IList<int> indexes, bool unordered)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

			IntArray[] sharedIndexes = mesh.sharedIndexesInternal;
			Dictionary<int, int> lookup = sharedIndexes.ToDictionary();
			HashSet<int> common = IntArrayUtility.GetCommonIndexes(lookup, indexes);
			List<Vertex> vertexes = new List<Vertex>(Vertex.GetVertexes(mesh));
			List<Vertex> appendVertexes = new List<Vertex>();

			foreach(int i in common)
			{
				int index = sharedIndexes[i][0];
				appendVertexes.Add(new Vertex(vertexes[index]));
			}

			FaceRebuildData data = FaceWithVertexes(appendVertexes, unordered);

			if(data != null)
			{
				data.sharedIndexes = common.ToList();
				List<Face> faces = new List<Face>(mesh.facesInternal);
				FaceRebuildData.Apply(new FaceRebuildData[] { data }, vertexes, faces, lookup, null);
				mesh.SetVertexes(vertexes);
				mesh.faces = faces;
				mesh.SetSharedIndexes(lookup);

                return data.face;
			}

			const string insufficientPoints = "Too Few Unique Points Selected";
			const string badWinding = "Points not ordered correctly";

            Log.Info(unordered ? insufficientPoints : badWinding);

            return null;
		}

		/// <summary>
		/// Create a poly shape from a set of points on a plane. The points must be ordered.
		/// </summary>
		/// <param name="poly"></param>
		/// <returns>An action result indicating the status of the operation.</returns>
		internal static ActionResult CreateShapeFromPolygon(this PolyShape poly)
		{
			var mesh = poly.mesh;
			var material = poly.material;

			if (material == null)
			{
				var renderer = poly.GetComponent<MeshRenderer>();
				material = renderer.sharedMaterial;
			}

			var res = mesh.CreateShapeFromPolygon(poly.m_Points, poly.extrude, poly.flipNormals);

			if (material != null)
			{
				foreach (var face in mesh.faces)
					face.material = material;

				// no need to do a ToMesh and Refresh here because we know every face is set to the same material
				poly.GetComponent<MeshRenderer>().sharedMaterial = material;
			}

			return res;
		}

		/// <summary>
		/// Rebuild a mesh from an ordered set of points.
		/// </summary>
		/// <param name="mesh">The target mesh. The mesh values will be cleared and repopulated with the shape extruded from points.</param>
		/// <param name="points">A path of points to triangulate and extrude.</param>
		/// <param name="extrude">The distance to extrude.</param>
		/// <param name="flipNormals">If true the faces will be inverted at creation.</param>
		/// <returns>An ActionResult with the status of the operation.</returns>
		public static ActionResult CreateShapeFromPolygon(this ProBuilderMesh mesh, IList<Vector3> points, float extrude, bool flipNormals)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            if (points == null || points.Count < 3)
			{
				mesh.Clear();
				mesh.ToMesh();
				mesh.Refresh();
				return new ActionResult(ActionResult.Status.NoChange, "Too Few Points");
			}

			Vector3[] vertexes = points.ToArray();
			List<int> triangles;

			Log.PushLogLevel(LogLevel.Error);

			if(Triangulation.TriangulateVertexes(vertexes, out triangles, false))
			{
				int[] indexes = triangles.ToArray();

				if(Math.PolygonArea(vertexes, indexes) < Mathf.Epsilon )
				{
					mesh.Clear();
					Log.PopLogLevel();
					return new ActionResult(ActionResult.Status.Failure, "Polygon Area < Epsilon");
				}

				mesh.Clear();
				mesh.RebuildWithPositionsAndFaces(vertexes, new Face[] { new Face(indexes) });

				Vector3 nrm = Math.Normal(mesh, mesh.facesInternal[0]);

				if (Vector3.Dot(Vector3.up, nrm) > 0f)
					mesh.facesInternal[0].Reverse();

				mesh.DuplicateAndFlip(mesh.facesInternal);

				mesh.Extrude(new Face[] { mesh.facesInternal[1] }, ExtrudeMethod.IndividualFaces, extrude);

				if ((extrude < 0f && !flipNormals) || (extrude > 0f && flipNormals))
				{
					foreach(var face in mesh.facesInternal)
						face.Reverse();
				}

				mesh.ToMesh();
				mesh.Refresh();
			}
			else
			{
				Log.PopLogLevel();
				return new ActionResult(ActionResult.Status.Failure, "Failed Triangulating Points");
			}

			Log.PopLogLevel();

			return new ActionResult(ActionResult.Status.Success, "Create Polygon Shape");
		}

		/// <summary>
		/// Create a new face given a set of unordered vertexes (or ordered, if unordered param is set to false).
		/// </summary>
		/// <param name="vertexes"></param>
		/// <param name="unordered"></param>
		/// <returns></returns>
		internal static FaceRebuildData FaceWithVertexes(List<Vertex> vertexes, bool unordered = true)
		{
			List<int> triangles;

			if(Triangulation.TriangulateVertexes(vertexes, out triangles, unordered))
			{
				FaceRebuildData data = new FaceRebuildData();
				data.vertexes = vertexes;
				data.face = new Face(triangles.ToArray());
				return data;
			}

			return null;
		}

		/// <summary>
		/// Given a path of vertexes, inserts a new vertex in the center inserts triangles along the path.
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		internal static List<FaceRebuildData> TentCapWithVertexes(List<Vertex> path)
		{
			int count = path.Count;
			Vertex center = Vertex.Average(path);
			List<FaceRebuildData> faces = new List<FaceRebuildData>();

			for(int i = 0; i < count; i++)
			{
				List<Vertex> vertexes = new List<Vertex>()
				{
					path[i],
					center,
					path[(i+1)%count]
				};

				FaceRebuildData data = new FaceRebuildData();
				data.vertexes = vertexes;
				data.face = new Face(new int[] {0 , 1, 2});

				faces.Add(data);
			}

			return faces;
		}

		/// <summary>
		/// Duplicate and reverse the winding direction for each face.
		/// </summary>
		/// <param name="mesh">The target mesh.</param>
		/// <param name="faces">The faces to duplicate, reverse triangle winding order, and append to mesh.</param>
		public static void DuplicateAndFlip(this ProBuilderMesh mesh, Face[] faces)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            if (faces == null)
                throw new ArgumentNullException("faces");

			List<FaceRebuildData> rebuild = new List<FaceRebuildData>();
			List<Vertex> vertexes = new List<Vertex>(Vertex.GetVertexes(mesh));
			Dictionary<int, int> lookup = mesh.sharedIndexesInternal.ToDictionary();

			foreach(Face face in faces)
			{
				FaceRebuildData data = new FaceRebuildData();

				data.vertexes = new List<Vertex>();
				data.face = new Face(face);
				data.sharedIndexes = new List<int>();

				Dictionary<int, int> map = new Dictionary<int, int>();
				int len = data.face.indexesInternal.Length;

				for(int i = 0; i < len; i++)
				{
					if(map.ContainsKey(face.indexesInternal[i]))
						continue;

					map.Add(face.indexesInternal[i], map.Count);
					data.vertexes.Add(vertexes[face.indexesInternal[i]]);
					data.sharedIndexes.Add(lookup[face.indexesInternal[i]]);
				}

				int[] tris = new int[len];

				for(var i = 0; i < len; i++)
					tris[len - (i+1)] = map[data.face[i]];

				data.face.SetIndexes(tris);

				rebuild.Add(data);
			}

			FaceRebuildData.Apply(rebuild, mesh, vertexes, null, lookup, null);
		}

		/// <summary>
		/// Insert a face between two edges.
		/// </summary>
		/// <param name="mesh">The source mesh.</param>
		/// <param name="a">First edge.</param>
		/// <param name="b">Second edge</param>
		/// <param name="enforcePerimiterEdgesOnly">If true, this function will not create a face bridging manifold edges.</param>
		/// <returns>The new face, or null of the action failed.</returns>
		public static Face Bridge(this ProBuilderMesh mesh, Edge a, Edge b, bool enforcePerimiterEdgesOnly = false)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

			IntArray[] sharedIndexes = mesh.GetSharedIndexes();
			Dictionary<int, int> lookup = sharedIndexes.ToDictionary();

			// Check to see if a face already exists
			if(enforcePerimiterEdgesOnly)
			{
				if( ElementSelection.GetNeighborFaces(mesh, a).Count > 1 || ElementSelection.GetNeighborFaces(mesh, b).Count > 1 )
				{
					return null;
				}
			}

			foreach(Face face in mesh.facesInternal)
			{
				if(face.edgesInternal.IndexOf(a, lookup) >= 0 && face.edgesInternal.IndexOf(b, lookup) >= 0)
				{
					Log.Warning("Face already exists between these two edges!");
					return null;
				}
			}

			Vector3[] verts = mesh.positionsInternal;
			Vector3[] v;
			Color[] c;
			int[] s;
			AutoUnwrapSettings uvs = AutoUnwrapSettings.tile;
			Material mat = BuiltinMaterials.defaultMaterial;

			// Get material and UV stuff from the first edge face
			SimpleTuple<Face, Edge> faceAndEdge = null;

			if(!EdgeExtension.ValidateEdge(mesh, a, out faceAndEdge))
				EdgeExtension.ValidateEdge(mesh, b, out faceAndEdge);

			if(faceAndEdge != null)
			{
				uvs = new AutoUnwrapSettings(faceAndEdge.item1.uv);
				mat = faceAndEdge.item1.material;
			}

			// Bridge will form a triangle
			if( a.Contains(b.a, sharedIndexes) || a.Contains(b.b, sharedIndexes) )
			{
				v = new Vector3[3];
				c = new Color[3];
				s = new int[3];

				bool axbx = System.Array.IndexOf(sharedIndexes[sharedIndexes.IndexOf(a.a)], b.a) > -1;
				bool axby = System.Array.IndexOf(sharedIndexes[sharedIndexes.IndexOf(a.a)], b.b) > -1;

				bool aybx = System.Array.IndexOf(sharedIndexes[sharedIndexes.IndexOf(a.b)], b.a) > -1;
				bool ayby = System.Array.IndexOf(sharedIndexes[sharedIndexes.IndexOf(a.b)], b.b) > -1;

				if(axbx)
				{
					v[0] = verts[a.a];
					c[0] = mesh.colorsInternal[a.a];
					s[0] = sharedIndexes.IndexOf(a.a);
					v[1] = verts[a.b];
					c[1] = mesh.colorsInternal[a.b];
					s[1] = sharedIndexes.IndexOf(a.b);
					v[2] = verts[b.b];
					c[2] = mesh.colorsInternal[b.b];
					s[2] = sharedIndexes.IndexOf(b.b);
				}
				else
				if(axby)
				{
					v[0] = verts[a.a];
					c[0] = mesh.colorsInternal[a.a];
					s[0] = sharedIndexes.IndexOf(a.a);
					v[1] = verts[a.b];
					c[1] = mesh.colorsInternal[a.b];
					s[1] = sharedIndexes.IndexOf(a.b);
					v[2] = verts[b.a];
					c[2] = mesh.colorsInternal[b.a];
					s[2] = sharedIndexes.IndexOf(b.a);
				}
				else
				if(aybx)
				{
					v[0] = verts[a.b];
					c[0] = mesh.colorsInternal[a.b];
					s[0] = sharedIndexes.IndexOf(a.b);
					v[1] = verts[a.a];
					c[1] = mesh.colorsInternal[a.a];
					s[1] = sharedIndexes.IndexOf(a.a);
					v[2] = verts[b.b];
					c[2] = mesh.colorsInternal[b.b];
					s[2] = sharedIndexes.IndexOf(b.b);
				}
				else
				if(ayby)
				{
					v[0] = verts[a.b];
					c[0] = mesh.colorsInternal[a.b];
					s[0] = sharedIndexes.IndexOf(a.b);
					v[1] = verts[a.a];
					c[1] = mesh.colorsInternal[a.a];
					s[1] = sharedIndexes.IndexOf(a.a);
					v[2] = verts[b.a];
					c[2] = mesh.colorsInternal[b.a];
					s[2] = sharedIndexes.IndexOf(b.a);
				}

				return mesh.AppendFace(
					v,
					c,
					new Vector2[v.Length],
					new Face( axbx || axby ? new int[3] {2, 1, 0} : new int[3] {0, 1, 2}, mat, uvs, 0, -1, -1, false ),
					s);;
			}

			// Else, bridge will form a quad

			v = new Vector3[4];
			c = new Color[4];
			s = new int[4]; // shared indexes index to add to

			v[0] = verts[a.a];
			c[0] = mesh.colorsInternal[a.a];
			s[0] = sharedIndexes.IndexOf(a.a);
			v[1] = verts[a.b];
			c[1] = mesh.colorsInternal[a.b];
			s[1] = sharedIndexes.IndexOf(a.b);

			Vector3 nrm = Vector3.Cross( verts[b.a]-verts[a.a], verts[a.b]-verts[a.a] ).normalized;
			Vector2[] planed = Projection.PlanarProject( new Vector3[4] {verts[a.a], verts[a.b], verts[b.a], verts[b.b] }, nrm );

			Vector2 ipoint = Vector2.zero;
			bool intersects = Math.GetLineSegmentIntersect(planed[0], planed[2], planed[1], planed[3], ref ipoint);

			if(!intersects)
			{
				v[2] = verts[b.a];
				c[2] = mesh.colorsInternal[b.a];
				s[2] = sharedIndexes.IndexOf(b.a);
				v[3] = verts[b.b];
				c[3] = mesh.colorsInternal[b.b];
				s[3] = sharedIndexes.IndexOf(b.b);
			}
			else
			{
				v[2] = verts[b.b];
				c[2] = mesh.colorsInternal[b.b];
				s[2] = sharedIndexes.IndexOf(b.b);
				v[3] = verts[b.a];
				c[3] = mesh.colorsInternal[b.a];
				s[3] = sharedIndexes.IndexOf(b.a);
			}

			return mesh.AppendFace(
				v,
				c,
				new Vector2[v.Length],
				new Face( new int[6] {2, 1, 0, 2, 3, 1 }, mat, uvs, 0, -1, -1, false ),
				s);
		}

		/// <summary>
		/// Add a set of points to a face and retriangulate. Points are added to the nearest edge.
		/// </summary>
		/// <param name="mesh">The source mesh.</param>
		/// <param name="face">The face to append points to.</param>
		/// <param name="points">Points to added to the face.</param>
		/// <returns>The face created by appending the points.</returns>
		public static Face AppendVertexesToFace(this ProBuilderMesh mesh, Face face, Vector3[] points)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            if (face == null)
                throw new ArgumentNullException("face");

            if (points == null)
                throw new ArgumentNullException("points");

            List<Vertex> vertexes = Vertex.GetVertexes(mesh).ToList();
            List<Face> faces = new List<Face>(mesh.facesInternal);
            Dictionary<int, int> lookup = mesh.sharedIndexesInternal.ToDictionary();
            Dictionary<int, int> lookupUV = mesh.sharedIndexesUVInternal == null ? null : mesh.sharedIndexesUVInternal.ToDictionary();

            List<Edge> wound = WingedEdge.SortEdgesByAdjacency(face);

            List<Vertex> n_vertexes = new List<Vertex>();
            List<int> n_shared = new List<int>();
            List<int> n_sharedUV = lookupUV != null ? new List<int>() : null;

            for (int i = 0; i < wound.Count; i++)
			{
				n_vertexes.Add(vertexes[wound[i].a]);
				n_shared.Add(lookup[wound[i].a]);

				if(lookupUV != null)
				{
					int uv;

					if(lookupUV.TryGetValue(wound[i].a, out uv))
						n_sharedUV.Add(uv);
					else
						n_sharedUV.Add(-1);
				}
			}

			// now insert the new points on the nearest edge
			for(int i = 0; i < points.Length; i++)
			{
				int index = -1;
				float best = Mathf.Infinity;
				Vector3 p = points[i];
				int vc = n_vertexes.Count;

				for(int n = 0; n < vc; n++)
				{
					Vector3 v = n_vertexes[n].position;
					Vector3 w = n_vertexes[(n + 1) % vc].position;

					float dist = Math.DistancePointLineSegment(p, v, w);

					if(dist < best)
					{
						best = dist;
						index = n;
					}
				}

				Vertex left = n_vertexes[index], right = n_vertexes[(index+1) % vc];

				float x = (p - left.position).sqrMagnitude;
				float y = (p - right.position).sqrMagnitude;

				Vertex insert = Vertex.Mix(left, right, x / (x + y));

				n_vertexes.Insert((index + 1) % vc, insert);
				n_shared.Insert((index + 1) % vc, -1);
				if(n_sharedUV != null) n_sharedUV.Insert((index + 1) % vc, -1);
			}

			List<int> triangles;

			try
			{
				Triangulation.TriangulateVertexes(n_vertexes, out triangles, false);
			}
			catch
			{
				Debug.Log("Failed triangulating face after appending vertexes.");
				return null;
			}

			FaceRebuildData data = new FaceRebuildData();

			data.face = new Face(triangles.ToArray(), face.material, new AutoUnwrapSettings(face.uv), face.smoothingGroup, face.textureGroup, -1, face.manualUV);
			data.vertexes 			= n_vertexes;
			data.sharedIndexes 		= n_shared;
			data.sharedIndexesUV 	= n_sharedUV;

			FaceRebuildData.Apply(	new List<FaceRebuildData>() { data },
										vertexes,
										faces,
										lookup,
										lookupUV);

			var newFace = data.face;

			mesh.SetVertexes(vertexes);
			mesh.faces = faces;
			mesh.SetSharedIndexes(lookup);
			mesh.SetSharedIndexesUV(lookupUV);

			// check old normal and make sure this new face is pointing the same direction
			Vector3 oldNrm = Math.Normal(mesh, face);
			Vector3 newNrm = Math.Normal(mesh, newFace);

			if( Vector3.Dot(oldNrm, newNrm) < 0 )
				newFace.Reverse();

			mesh.DeleteFace(face);

			return newFace;
		}

		/// <summary>
		/// Insert a number of new points to an edge. Points are evenly spaced out along the edge.
		/// </summary>
		/// <param name="mesh">The source mesh.</param>
		/// <param name="edge">The edge to split with points.</param>
		/// <param name="count">The number of new points to insert. Must be greater than 0.</param>
		/// <returns>The new edges created by inserting points.</returns>
		public static List<Edge> AppendVertexesToEdge(this ProBuilderMesh mesh, Edge edge, int count)
		{
			return AppendVertexesToEdge(mesh, new Edge[] { edge }, count);
		}

		/// <summary>
		/// Insert a number of new points to each edge. Points are evenly spaced out along the edge.
		/// </summary>
		/// <param name="mesh">The source mesh.</param>
		/// <param name="edges">The edges to split with points.</param>
		/// <param name="count">The number of new points to insert. Must be greater than 0.</param>
		/// <returns>The new edges created by inserting points.</returns>
		public static List<Edge> AppendVertexesToEdge(this ProBuilderMesh mesh, IList<Edge> edges, int count)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            if (edges == null)
                throw new ArgumentNullException("edges");

            if (count < 1 || count > 512)
            {
                Log.Error("New edge vertex count is less than 1 or greater than 512.");
                return null;
            }

            List<Vertex> vertexes = new List<Vertex>(Vertex.GetVertexes(mesh));
            Dictionary<int, int> lookup = mesh.sharedIndexesInternal.ToDictionary();
            Dictionary<int, int> lookupUV = mesh.sharedIndexesUVInternal.ToDictionary();
            List<int> indexesToDelete = new List<int>();
            Edge[] commonEdges = EdgeExtension.GetUniversalEdges(edges.ToArray(), lookup);
            List<Edge> distinctEdges = commonEdges.Distinct().ToList();

            Dictionary<Face, FaceRebuildData> modifiedFaces = new Dictionary<Face, FaceRebuildData>();

			int originalSharedIndexesCount = lookup.Count();
			int sharedIndexesCount = originalSharedIndexesCount;

			foreach(Edge edge in distinctEdges)
			{
				Edge localEdge = EdgeExtension.GetLocalEdgeFast(edge, mesh.sharedIndexesInternal);

				// Generate the new vertexes that will be inserted on this edge
				List<Vertex> vertexesToAppend = new List<Vertex>(count);

				for(int i = 0; i < count; i++)
					vertexesToAppend.Add(Vertex.Mix(vertexes[localEdge.a], vertexes[localEdge.b], (i+1)/((float)count + 1)));

				List<SimpleTuple<Face, Edge>> adjacentFaces = ElementSelection.GetNeighborFaces(mesh, localEdge);

				// foreach face attached to common edge, append vertexes
				foreach(SimpleTuple<Face, Edge> tup in adjacentFaces)
				{
					Face face = tup.item1;

					FaceRebuildData data;

					if( !modifiedFaces.TryGetValue(face, out data) )
					{
						data = new FaceRebuildData();
						data.face = new Face(new int[0], face.material, new AutoUnwrapSettings(face.uv), face.smoothingGroup, face.textureGroup, -1, face.manualUV);
						data.vertexes = new List<Vertex>(ArrayUtility.ValuesWithIndexes(vertexes, face.distinctIndexesInternal));
						data.sharedIndexes = new List<int>();
						data.sharedIndexesUV = new List<int>();

						foreach(int i in face.distinctIndexesInternal)
						{
							int shared;

							if(lookup.TryGetValue(i, out shared))
								data.sharedIndexes.Add(shared);

							if(lookupUV.TryGetValue(i, out shared))
								data.sharedIndexesUV.Add(shared);
						}

						indexesToDelete.AddRange(face.distinctIndexesInternal);

						modifiedFaces.Add(face, data);
					}

					data.vertexes.AddRange(vertexesToAppend);

					for(int i = 0; i < count; i++)
					{
						data.sharedIndexes.Add(sharedIndexesCount + i);
						data.sharedIndexesUV.Add(-1);
					}
				}

				sharedIndexesCount += count;
			}

			// now apply the changes
			List<Face> dic_face = modifiedFaces.Keys.ToList();
			List<FaceRebuildData> dic_data = modifiedFaces.Values.ToList();
			List<EdgeLookup> appendedEdges = new List<EdgeLookup>();

			for(int i = 0; i < dic_face.Count; i++)
			{
				Face face = dic_face[i];
				FaceRebuildData data = dic_data[i];

				Vector3 nrm = Math.Normal(mesh, face);
				Vector2[] projection = Projection.PlanarProject(data.vertexes.Select(x => x.position).ToArray(), nrm);

				int vertexCount = vertexes.Count;

				// triangulate and set new face indexes to end of current vertex list
				List<int> indexes;

				if(Triangulation.SortAndTriangulate(projection, out indexes))
					data.face.indexesInternal = indexes.ToArray();
				else
					continue;

				data.face.ShiftIndexes(vertexCount);
				face.CopyFrom(data.face);

				for(int n = 0; n < data.vertexes.Count; n++)
					lookup.Add(vertexCount + n, data.sharedIndexes[n]);

				if(data.sharedIndexesUV.Count == data.vertexes.Count)
				{
					for(int n = 0; n < data.vertexes.Count; n++)
						lookupUV.Add(vertexCount + n, data.sharedIndexesUV[n]);
				}

				vertexes.AddRange(data.vertexes);

				foreach(Edge e in face.edgesInternal)
				{
					EdgeLookup el = new EdgeLookup(new Edge(lookup[e.a], lookup[e.b]), e);

					if(el.common.a >= originalSharedIndexesCount || el.common.b >= originalSharedIndexesCount)
						appendedEdges.Add(el);
				}
			}

			indexesToDelete = indexesToDelete.Distinct().ToList();
			int delCount = indexesToDelete.Count;

			var newEdges = appendedEdges.Distinct().Select(x => x.local - delCount).ToList();

			mesh.SetVertexes(vertexes);
			mesh.sharedIndexes = lookup.ToIntArray();
			mesh.SetSharedIndexesUV(lookupUV.ToIntArray());
			mesh.DeleteVertexes(indexesToDelete);

            return newEdges;
		}

	}
}
