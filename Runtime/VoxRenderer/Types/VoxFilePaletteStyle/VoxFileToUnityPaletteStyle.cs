using System.Collections.Generic;
using UnityEngine;
using Miventech.NativeVoxReader.Data;
using Miventech.NativeVoxReader.VoxRenderer;

namespace Miventech.NativeVoxReader.Tools.VoxFilePaletteStyle
{
    public static class VoxFileToUnityPaletteStyle
    {
        public static VoxModelResult[] Convert(VoxFile FileData, Color32[] palette, VoxFileToUnityPaletteStyleSetting settings = default)
        {
            if (settings == null) settings = new VoxFileToUnityPaletteStyleSetting();

            var result = new VoxModelResult[FileData.models.Count];
            int index = 0;
            foreach (var voxModel in FileData.models)
            {
                result[index] = ConvertModel(voxModel, palette, settings);
                index++;
            }
            return result;
        }

        public static VoxModelResult ConvertModel(VoxModel model, Color32[] palette, VoxFileToUnityPaletteStyleSetting settings = default)
        {
            if (settings == null) settings = new VoxFileToUnityPaletteStyleSetting();

            //XD ready custom palette
            if (model.UsePaletteCustom)
            {
                palette = model.CustomPalette.ToColor32Array();
            }

            // 1. Generate Palette Texture
            Texture2D paletteTexture = GeneratePaletteTexture(palette);

            // 2. Create Material
            Material mat = new Material(GetDefaultShader());
            mat.mainTexture = paletteTexture;
            mat.mainTexture.filterMode = FilterMode.Point;

            // Adjust properties to be Matte
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.0f);

            // 3. Generate Geometry
            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            GenerateGreedyMesh(model, palette, vertices, triangles, uvs);

            // Re-center mesh local position and apply scale
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] *= settings.Scale;
                vertices[i] -= new Vector3(model.size.x * settings.Scale * 0.5f, model.size.z * settings.Scale * 0.5f, model.size.y * settings.Scale * 0.5f);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return new VoxModelResult(mesh, paletteTexture, mat);
        }

        private static Shader GetDefaultShader()
        {
            // Detect the current Render Pipeline
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            {
                string pipelineType = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().ToString();

                if (pipelineType.Contains("Universal"))
                {
                    return Shader.Find("Universal Render Pipeline/Lit");
                }
                if (pipelineType.Contains("HDRenderPipeline") || pipelineType.Contains("HighDefinition"))
                {
                    return Shader.Find("HDRP/Lit");
                }
            }
            // Built-in Render Pipeline
            return Shader.Find("Standard");
        }

        private static Texture2D GeneratePaletteTexture(Color32[] palette)
        {
            Texture2D tex = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int i = 0; i < 256; i++)
            {
                if (i < palette.Length)
                    tex.SetPixel(i, 0, palette[i]);
                else
                    tex.SetPixel(i, 0, Color.black);
            }
            tex.Apply();
            return tex;
        }

        private static void GenerateGreedyMesh(VoxModel model, Color32[] palette, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
        {
            Vector3Int size = model.size;
            int[,,] volume = new int[size.x, size.y, size.z];

            foreach (var v in model.voxels)
            {
                if (v.x < size.x && v.y < size.y && v.z < size.z)
                    volume[v.x, v.y, v.z] = v.colorIndex;
            }

            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3;
                int v = (d + 2) % 3;

                int[] x = new int[3];
                int[] q = new int[3];
                q[d] = 1;

                for (int faceDir = -1; faceDir <= 1; faceDir += 2)
                {
                    int[] mask = new int[size[u] * size[v]];

                    for (x[d] = 0; x[d] < size[d]; x[d]++)
                    {
                        int n = 0;
                        for (x[v] = 0; x[v] < size[v]; x[v]++)
                        {
                            for (x[u] = 0; x[u] < size[u]; x[u]++)
                            {
                                int cCurrent = volume[x[0], x[1], x[2]];
                                int cNeighbor = 0;
                                int nx = x[0] + (d == 0 ? faceDir : 0);
                                int ny = x[1] + (d == 1 ? faceDir : 0);
                                int nz = x[2] + (d == 2 ? faceDir : 0);

                                if (nx >= 0 && nx < size.x &&
                                    ny >= 0 && ny < size.y &&
                                    nz >= 0 && nz < size.z)
                                {
                                    cNeighbor = volume[nx, ny, nz];
                                }

                                bool visible = (cCurrent != 0 && cNeighbor == 0);
                                mask[n++] = visible ? cCurrent : 0;
                            }
                        }

                        n = 0;
                        for (int j = 0; j < size[v]; j++)
                        {
                            for (int i = 0; i < size[u]; i++)
                            {
                                int c = mask[n];
                                if (c != 0)
                                {
                                    int width = 1;
                                    while (i + width < size[u] && mask[n + width] == c)
                                    {
                                        width++;
                                    }

                                    int height = 1;
                                    bool done = false;
                                    while (j + height < size[v])
                                    {
                                        for (int k = 0; k < width; k++)
                                        {
                                            if (mask[n + k + height * size[u]] != c)
                                            {
                                                done = true;
                                                break;
                                            }
                                        }
                                        if (done) break;
                                        height++;
                                    }

                                    int[] pos = new int[3];
                                    pos[u] = i;
                                    pos[v] = j;
                                    pos[d] = x[d];

                                    int depthOffset = (faceDir == 1) ? 1 : 0;
                                    pos[d] += depthOffset;

                                    int colorIndex = c - 1;
                                    if (colorIndex < 0) colorIndex = 0;
                                    if (colorIndex > 255) colorIndex = 255;

                                    float uCoord = (colorIndex + 0.5f) / 256.0f;
                                    Vector2 uv = new Vector2(uCoord, 0.5f);

                                    AddGreedyQuad(pos, u, v, d, width, height, faceDir, uv, vertices, triangles, uvs);

                                    for (int ly = 0; ly < height; ly++)
                                    {
                                        for (int lx = 0; lx < width; lx++)
                                        {
                                            mask[n + lx + ly * size[u]] = 0;
                                        }
                                    }

                                    i += width - 1;
                                    n += width - 1;
                                }
                                n++;
                            }
                        }
                    }
                }
            }
        }

        private static void AddGreedyQuad(int[] pos, int axisU, int axisV, int axisD, int width, int height, int faceDir, Vector2 uv,
                                   List<Vector3> verts, List<int> tris, List<Vector2> uvs)
        {
            int[] p0 = new int[] { pos[0], pos[1], pos[2] };
            int[] p1 = new int[] { pos[0], pos[1], pos[2] };
            p1[axisU] += width;
            int[] p2 = new int[] { pos[0], pos[1], pos[2] };
            p2[axisV] += height;
            int[] p3 = new int[] { pos[0], pos[1], pos[2] };
            p3[axisU] += width;
            p3[axisV] += height;

            Vector3 v0 = new Vector3(p0[0], p0[2], p0[1]);
            Vector3 v1 = new Vector3(p1[0], p1[2], p1[1]);
            Vector3 v2 = new Vector3(p2[0], p2[2], p2[1]);
            Vector3 v3 = new Vector3(p3[0], p3[2], p3[1]);

            int baseIndex = verts.Count;
            verts.Add(v0);
            verts.Add(v1);
            verts.Add(v2);
            verts.Add(v3);

            uvs.Add(uv);
            uvs.Add(uv);
            uvs.Add(uv);
            uvs.Add(uv);

            if (faceDir == 1)
            {
                tris.Add(baseIndex);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 1);

                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 3);
            }
            else
            {
                tris.Add(baseIndex);
                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);

                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 3);
                tris.Add(baseIndex + 2);
            }
        }
    }
}
