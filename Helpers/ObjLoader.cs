using Avalonia3D.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Avalonia3D.Helpers
{
    public static class ObjLoader
    {
     
        private static void ProcessVertex(
            (int posIdx, int normIdx) vertex,
            Dictionary<(int, int), uint> indexMap,
            List<Vector3> positions,
            List<Vector3> normals,
            List<Vertex> vertices,
            List<uint> indices)
        {
            var key = (vertex.posIdx, vertex.normIdx);
            if (indexMap.TryGetValue(key, out uint index))
            {
                indices.Add(index);
            }
            else
            {
                var newVertex = new Vertex
                {
                    Position = positions[vertex.posIdx],
                    Normal = normals[vertex.normIdx]
                };
                vertices.Add(newVertex);
                uint newIndex = (uint)(vertices.Count - 1);
                indices.Add(newIndex);
                indexMap[key] = newIndex;
            }
        }

        private static float ParseFloat(string s)
        {
            return float.Parse(s, CultureInfo.InvariantCulture);
        }
    }
}
