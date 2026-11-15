using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace ShootingRange.EditorTools
{
    /// <summary>
    /// Builds simple meshes whose UVs are measured in metres, so a material's tiling is the
    /// same on every object regardless of its size. Meshes are cached and stored in one asset.
    /// </summary>
    internal sealed class RangeMeshFactory
    {
        private readonly string _assetPath;
        private readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();
        private Mesh _container;

        public RangeMeshFactory(string assetPath)
        {
            _assetPath = assetPath;
            AssetDatabase.DeleteAsset(assetPath);
            _container = new Mesh { name = "RangeMeshes" };
            AssetDatabase.CreateAsset(_container, assetPath);
        }

        private Mesh Store(string key, Mesh mesh)
        {
            mesh.name = key;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(false);
            AssetDatabase.AddObjectToAsset(mesh, _container);
            _cache[key] = mesh;
            return mesh;
        }

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        public Mesh Box(Vector3 size)
        {
            var key = $"Box_{F(size.x)}x{F(size.y)}x{F(size.z)}";
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var h = size * 0.5f;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            void Face(Vector3 n, Vector3 uAxis)
            {
                var vAxis = Vector3.Cross(uAxis, n);
                var hu = Mathf.Abs(Vector3.Dot(h, uAxis));
                var hv = Mathf.Abs(Vector3.Dot(h, vAxis));
                var hn = Mathf.Abs(Vector3.Dot(h, n));
                var c = n * hn;
                var start = verts.Count;
                var corners = new[]
                {
                    c - uAxis * hu - vAxis * hv,
                    c - uAxis * hu + vAxis * hv,
                    c + uAxis * hu + vAxis * hv,
                    c + uAxis * hu - vAxis * hv
                };
                foreach (var p in corners)
                {
                    verts.Add(p);
                    normals.Add(n);
                    uvs.Add(new Vector2(Vector3.Dot(p, uAxis) + hu, Vector3.Dot(p, vAxis) + hv));
                }

                tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            Face(Vector3.up, Vector3.right);
            Face(Vector3.down, Vector3.right);
            Face(Vector3.forward, Vector3.left);
            Face(Vector3.back, Vector3.right);
            Face(Vector3.right, Vector3.forward);
            Face(Vector3.left, Vector3.back);

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            return Store(key, mesh);
        }

        /// <summary>Cylinder along local Y, centred on the origin.</summary>
        public Mesh Cylinder(float radius, float height, int segments = 24, bool caps = true)
        {
            var key = $"Cyl_{F(radius)}x{F(height)}_{segments}{(caps ? "c" : "")}";
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var hh = height * 0.5f;
            var circumference = 2f * Mathf.PI * radius;

            for (var i = 0; i <= segments; i++)
            {
                var a = i / (float)segments * Mathf.PI * 2f;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var u = i / (float)segments * circumference;
                verts.Add(n * radius + Vector3.down * hh);
                normals.Add(n);
                uvs.Add(new Vector2(u, 0f));
                verts.Add(n * radius + Vector3.up * hh);
                normals.Add(n);
                uvs.Add(new Vector2(u, height));
            }

            for (var i = 0; i < segments; i++)
            {
                var b = i * 2;
                tris.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 });
            }

            if (caps)
            {
                foreach (var sign in new[] { 1f, -1f })
                {
                    var centre = verts.Count;
                    var n = Vector3.up * sign;
                    verts.Add(n * hh);
                    normals.Add(n);
                    uvs.Add(new Vector2(radius, radius));
                    for (var i = 0; i <= segments; i++)
                    {
                        var a = i / (float)segments * Mathf.PI * 2f;
                        var p = new Vector3(Mathf.Cos(a) * radius, hh * sign, Mathf.Sin(a) * radius);
                        verts.Add(p);
                        normals.Add(n);
                        uvs.Add(new Vector2(p.x + radius, p.z + radius));
                    }

                    for (var i = 0; i < segments; i++)
                    {
                        if (sign > 0) tris.AddRange(new[] { centre, centre + i + 2, centre + i + 1 });
                        else tris.AddRange(new[] { centre, centre + i + 1, centre + i + 2 });
                    }
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            return Store(key, mesh);
        }

        /// <summary>Flat quad in the XZ plane facing +Y (for floor markings and emissive panels).</summary>
        public Mesh FloorQuad(float width, float depth)
        {
            var key = $"Quad_{F(width)}x{F(depth)}";
            if (_cache.TryGetValue(key, out var cached)) return cached;

            float hx = width * 0.5f, hz = depth * 0.5f;
            var mesh = new Mesh();
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz)
            });
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0, 0), new Vector2(0, depth), new Vector2(width, depth), new Vector2(width, 0)
            });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            return Store(key, mesh);
        }

        public void Finish()
        {
            EditorUtility.SetDirty(_container);
            AssetDatabase.SaveAssets();
        }
    }
}
