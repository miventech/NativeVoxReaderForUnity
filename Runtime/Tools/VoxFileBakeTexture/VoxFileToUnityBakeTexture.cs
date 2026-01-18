using System.Collections.Generic;
using UnityEngine;
using Miventech.NativeUnityVoxReader.Data;
using Miventech.NativeUnityVoxReader.Tools.VoxFileBakeTexture.Data;

namespace Miventech.NativeUnityVoxReader.Tools.VoxFileBakeTexture
{
    public static class VoxFileToUnityBakeTexture
    {
         


        public static VoxModelResult[] Convert(VoxFile FileData, Color32[] palette, VoxFileToUnityBakeTextureSetting settings = default)
        {
            var result = new VoxModelResult[FileData.models.Count];
            int index = 0;
            foreach (var voxModel in FileData.models)
            {
                result[index] = ConvertModel(voxModel, palette, settings);
                index++;
            }
            return result;
        }
        
        public static VoxModelResult ConvertModel(VoxModel model, Color32[] palette, VoxFileToUnityBakeTextureSetting settings = default){


            VoxModelResult result = new VoxModelResult(null,null,null);
             // 1. Generate local geometry
            List<QuadInfo> quads = new List<QuadInfo>();
            GenerateGreedyQuads(model, palette, quads, settings);

            if (quads.Count == 0) return null;

            // 2. Crear Atlas de Textura
            // Crear texturas temporales para cada quad
            Texture2D[] tempTextures = new Texture2D[quads.Count];
            for (int i = 0; i < quads.Count; i++)
            {
                QuadInfo q = quads[i];
                // Crear textura del tamaño del quad
                Texture2D t = new Texture2D(q.width, q.height, TextureFormat.RGBA32, false);
                t.filterMode = FilterMode.Point;
                
                t.SetPixels32(q.colors);
                t.Apply();
                tempTextures[i] = t;
            }

            // Atlas 512x512 como base, pero permitimos crecer hasta maxAtlasSize si hace falta.
            Texture2D atlas = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            atlas.filterMode = FilterMode.Point;
            
            // Empaquetar texturas. PackTextures devuelve los UV Rects en el atlas.
            // padding=0 para pixel art exacto, o 1-2 si queremos evitar bleeding. pondremos 0.
            Rect[] uvRects = atlas.PackTextures(tempTextures, 0, settings.maxAtlasSize, false);

            // Asignar Material con la textura "cocinada"
            Material mat = new Material(Shader.Find("Standard"));
            result.texture = atlas; 
            mat.mainTexture = atlas;
            mat.mainTexture.filterMode = FilterMode.Point; // Importante para voxel look
            // Ajustar propiedades del material standard para que no sea muy brillante/specular por defecto si se desea
            mat.SetFloat("_Glossiness", 0.0f); // Mate
            
            result.material = mat;

            // 3. Generar Mesh Final mapeando UVs al atlas
            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            for (int i = 0; i < quads.Count; i++)
            {
                AddQuadToMesh(quads[i], uvRects[i], vertices, triangles, uvs);
            }
            for (int i = 0; i < vertices.Count; i++)
            {
                // Opcional: Escalar a 0.1 para que el modelo no sea tan grande en Unity
                vertices[i] *= settings.Scale;
                // Re-center mesh local position
                // por lo que el ajuste del centro debe ser (size.x, size.z, size.y)
                vertices[i] -= new Vector3(model.size.x * settings.Scale * 0.5f, model.size.z * settings.Scale * 0.5f, model.size.y * settings.Scale * 0.5f);
            }
            
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            result.mesh = mesh;

            // Limpieza de texturas temporales
            // En editor usamos DestroyImmediate
            foreach (var t in tempTextures)
            {
                if (t != null)
                {
                    if (Application.isEditor) GameObject.DestroyImmediate(t);
                    else GameObject.Destroy(t);
                }
            }

            return result;
        }
        
        private static void GenerateGreedyQuads(VoxModel model, Color32[] palette, List<QuadInfo> quads, VoxFileToUnityBakeTextureSetting settings = default)
        {
            Vector3Int size = model.size;
            int[,,] volume = new int[size.x, size.y, size.z];
            
            foreach (var v in model.voxels)
            {
                if(v.x < size.x && v.y < size.y && v.z < size.z)
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
                                // IMPORTANTE: Guardamos el color en la máscara, pero para la fusión
                                // solo nos importará si es != 0 para ignorar cambios de color
                                mask[n++] = visible ? cCurrent : 0;
                            }
                        }

                        n = 0;
                        for (int j = 0; j < size[v]; j++)
                        {
                            for (int i = 0; i < size[u]; i++)
                            {
                                int c = mask[n];
                                if (c != 0) // Si es visible
                                {
                                    int width = 1;
                                    // Expandir ancho MIENTRAS sea visible (mask != 0), ignorando si cambia de color
                                    while (i + width < size[u] && mask[n + width] != 0 && width < settings.maxQuadSize) 
                                    {
                                        width++;
                                    }

                                    int height = 1;
                                    bool done = false;
                                    while (j + height < size[v] && height < settings.maxQuadSize)
                                    {
                                        for (int k = 0; k < width; k++)
                                        {
                                            // Verificar si la fila siguiente es visible en todo el ancho
                                            if (mask[n + k + height * size[u]] == 0)
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

                                    // Extraer los colores individuales de este bloque
                                    Color32[] quadColors = new Color32[width * height];
                                    
                                    // Recorrer el área del quad para obtener los colores del volume original
                                    for (int ly = 0; ly < height; ly++)
                                    {
                                        for (int lx = 0; lx < width; lx++)
                                        {
                                            int[] voxelPos = new int[3];
                                            voxelPos[u] = pos[u] + lx;
                                            voxelPos[v] = pos[v] + ly;
                                            voxelPos[d] = pos[d];
                                            
                                            // Obtener indice de color del volume
                                            int colorIdx = volume[voxelPos[0], voxelPos[1], voxelPos[2]];
                                            
                                            // Convertir a Color32
                                            Color32 colorPixel = Color.magenta;
                                            if (colorIdx - 1 < palette.Length && colorIdx - 1 >= 0) 
                                                colorPixel = palette[colorIdx - 1];
                                                
                                            // Guardar en array linear. Texture2D llena de izquierda a derecha, abajo a arriba.
                                            // La malla se genera con u (width) y v (height). 
                                            // Asumimos mapeo directo: x = lx, y = ly.
                                            quadColors[lx + ly * width] = colorPixel;
                                            
                                            // Limpiar máscara
                                            // La lógica original limpiaba después. Aquí limpiamos mientras leemos.
                                            // Pero ojo, n apunta al inicio de la fila j.
                                            // mask index = n + lx + ly * size[u]
                                            // n = (j * size[u]) + i
                                            mask[(j + ly) * size[u] + (i + lx)] = 0;
                                        }
                                    }

                                    int[] visualPos = new int[] { pos[0], pos[1], pos[2] };
                                    int depthOffset = (faceDir == 1) ? 1 : 0;
                                    visualPos[d] += depthOffset;

                                    // Guardar Info del Quad
                                    AddQuadInfo(visualPos, u, v, d, width, height, faceDir, quadColors, quads);

                                    // IMPORTANTE: Ya limpiamos la mascara arriba al extraer colores.
                                    
                                    // Avanzar indices para sincronizar n con i
                                    int skip = width - 1;
                                    i += skip;
                                    n += skip;
                                }
                                n++;
                            }
                        }
                    }
                }
            }
        }

        private static void AddQuadInfo(int[] pos, int axisU, int axisV, int axisD, int width, int height, int faceDir, Color32[] colors, List<QuadInfo> quads, VoxFileToUnityBakeTextureSetting settings = default)
        {
            QuadInfo q = new QuadInfo();
            q.colors = colors;
            q.width = width;
            q.height = height;
            q.faceDir = faceDir;

            // Calcular vértices en Unity World Space
            // v0: 0,0
            int[] p0 = new int[]{ pos[0], pos[1], pos[2] };
            // v1: w,0
            int[] p1 = new int[]{ pos[0], pos[1], pos[2] };
            p1[axisU] += width;
            // v2: 0,h
            int[] p2 = new int[]{ pos[0], pos[1], pos[2] };
            p2[axisV] += height;
            // v3: w,h
            int[] p3 = new int[]{ pos[0], pos[1], pos[2] };
            p3[axisU] += width;
            p3[axisV] += height;

            // Vox(x,y,z) -> Unity(x,z,y)
            q.v0 = new Vector3(p0[0], p0[2], p0[1]);
            q.v1 = new Vector3(p1[0], p1[2], p1[1]);
            q.v2 = new Vector3(p2[0], p2[2], p2[1]);
            q.v3 = new Vector3(p3[0], p3[2], p3[1]);

            quads.Add(q);
        }

        private static void AddQuadToMesh(QuadInfo q, Rect uvRect, List<Vector3> verts, List<int> tris, List<Vector2> uvs)
        {
            int baseIndex = verts.Count;
            verts.Add(q.v0);
            verts.Add(q.v1);
            verts.Add(q.v2);
            verts.Add(q.v3);

            // UVs: Mapear esquinas del Quad (0,0 -> 1,1) al Rect del Atlas
            // v0 (0,0) -> uvRect.min
            // v1 (w,0) -> uvRect.xMax, uvRect.yMin
            // v2 (0,h) -> uvRect.xMin, uvRect.yMax
            // v3 (w,h) -> uvRect.max
            
            // Nota: En AddQuadInfo v0=(0,0), v1=(w,0), v2=(0,h), v3=(w,h) relativo al origen del quad.
            
            uvs.Add(new Vector2(uvRect.xMin, uvRect.yMin)); // v0
            uvs.Add(new Vector2(uvRect.xMax, uvRect.yMin)); // v1
            uvs.Add(new Vector2(uvRect.xMin, uvRect.yMax)); // v2 (Cuidado con el orden v2/v3 en triángulos)
            uvs.Add(new Vector2(uvRect.xMax, uvRect.yMax)); // v3

            // Winding order (Triangulos)
            if (q.faceDir == 1)
            {
                // Normal positiva
                tris.Add(baseIndex);     // 0
                tris.Add(baseIndex + 2); // 2
                tris.Add(baseIndex + 1); // 1
                
                tris.Add(baseIndex + 1); // 1
                tris.Add(baseIndex + 2); // 2
                tris.Add(baseIndex + 3); // 3
            }
            else
            {
                // Normal negativa
                tris.Add(baseIndex);     // 0
                tris.Add(baseIndex + 1); // 1
                tris.Add(baseIndex + 2); // 2

                tris.Add(baseIndex + 1); // 1
                tris.Add(baseIndex + 3); // 3
                tris.Add(baseIndex + 2); // 2
            }
        }
        
        // Remove unused method
        /* private void AddCubeOptimized... */
        
        private static void AddFace(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color32 color, List<Vector3> verts, List<int> tris, List<Color32> cols)
        {
            int baseIndex = verts.Count;

            verts.Add(v0);
            verts.Add(v1);
            verts.Add(v2);
            verts.Add(v3);

            cols.Add(color);
            cols.Add(color);
            cols.Add(color);
            cols.Add(color);

            // Primer triángulo
            tris.Add(baseIndex);
            tris.Add(baseIndex + 1);
            tris.Add(baseIndex + 2);

            // Segundo triángulo
            tris.Add(baseIndex);
            tris.Add(baseIndex + 2);
            tris.Add(baseIndex + 3);
        }
        
    }
}

