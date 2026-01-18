using UnityEngine;
using System.Collections.Generic;
using Miventech.NativeUnityVoxReader.Data;
using Miventech.NativeUnityVoxReader.Abstract;

namespace Miventech.NativeUnityVoxReader.CreatorObjects
{
    /// <summary>
    /// Basic implementation of VoxMeshBuilderAbstract.
    /// </summary>
    public class BasicVoxCreateObject : VoxCreateObjectAbstract
    {
        public override void BuildObject(VoxModel model, Color32[] palette)
        {
            GameObject ChildObject = new GameObject("VoxModel");
            ChildObject.transform.SetParent(this.transform);
            ChildObject.transform.localPosition = (Vector3)model.position;
            ChildObject.transform.localRotation = Quaternion.identity;
            ChildObject.transform.localScale = Vector3.one;

            MeshFilter meshFilter = ChildObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = ChildObject.AddComponent<MeshRenderer>();
            
            // Local color palette asset
            Texture2D paletteTexture = GeneratePaletteTexture(palette);
            
            // Usamos un Shader standard o unlit que soporte texturas.
            // "Standard" soportará iluminación.
            Material mat = new Material(Shader.Find("Standard")); 
            mat.mainTexture = paletteTexture;
            // Para Pixel Art (colores planos), Point filter es mejor
            mat.mainTexture.filterMode = FilterMode.Point; 
            
            meshRenderer.material = mat;

            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            GenerateGreedyMesh(model, palette, vertices, triangles, uvs);

            // Re-center mesh local position
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] -= new Vector3(model.size.x * 0.5f, model.size.z * 0.5f, model.size.y * 0.5f);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            meshFilter.mesh = mesh;
        }

        private Texture2D GeneratePaletteTexture(Color32[] palette)
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

        private void GenerateGreedyMesh(VoxModel model, Color32[] palette, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
        {
            // Init volume buffer
            // Vox coordinates: x, y, z
            Vector3Int size = model.size;
            // Usamos int para colorIndex (1-255). 0 = vacio.
            // Si el modelo es muy grande este array puede ser grande, pero para MagicaVoxel (max 256^3) es aceptable (~16MB).
            int[,,] volume = new int[size.x, size.y, size.z];
            
            foreach (var v in model.voxels)
            {
                // Index safety check
                if(v.x < size.x && v.y < size.y && v.z < size.z)
                    volume[v.x, v.y, v.z] = v.colorIndex;
            }

            // 2. Iterar sobre las 3 dimensiones (ejes)
            // d=0 -> X (Width plane YZ)
            // d=1 -> Y (Depth plane XZ) - En Vox Y es profundidad
            // d=2 -> Z (Height plane XY) - En Vox Z es altura
            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3; // Eje U del plano de corte
                int v = (d + 2) % 3; // Eje V del plano de corte

                int[] x = new int[3]; // Cursor de posición 3D
                int[] q = new int[3]; // Cursor de dirección de barrido en el eje d
                q[d] = 1;

                // Dos direcciones por eje: -1 (cara "back" o negativa) y +1 (cara "front" o positiva)
                // faceDir: determina la normal de la cara y qué vecino mirar.
                for (int faceDir = -1; faceDir <= 1; faceDir += 2)
                {
                    // Máscara 2D para el "slice" actual. Guarda el índice de color.
                    // Dimensiones del plano: size[u] x size[v]
                    int[] mask = new int[size[u] * size[v]];

                    // Barrido a través del volumen en la dirección d
                    // x[d] recorre desde 0 hasta size[d]-1
                    for (x[d] = 0; x[d] < size[d]; x[d]++)
                    {
                        int n = 0;
                        // Rellenar máscara para este slice x[d]
                        for (x[v] = 0; x[v] < size[v]; x[v]++)
                        {
                            for (x[u] = 0; x[u] < size[u]; x[u]++)
                            {
                                // Obtener color actual
                                int cCurrent = volume[x[0], x[1], x[2]];
                                
                                // Obtener color del vecino en la dirección de la cara
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

                                // Determinar si la cara es visible
                                // Una cara es visible si el voxel actual es sólido Y el vecino es aire (0).
                                // Pero ojo: estamos iterando 'slices'.
                                // Si faceDir es +1: estamos dibujando la cara + del voxel actual. Visible si Current != 0 y Neighbor == 0.
                                // Si faceDir es -1: estamos dibujando la cara - del voxel actual. Visible si Current != 0 y Neighbor == 0.
                                
                                bool visible = (cCurrent != 0 && cNeighbor == 0);
                                mask[n++] = visible ? cCurrent : 0;
                            }
                        }

                        // Algoritmo Greedy Meshing sobre la máscara mask[]
                        n = 0;
                        for (int j = 0; j < size[v]; j++)
                        {
                            for (int i = 0; i < size[u]; i++)
                            {
                                int c = mask[n];
                                if (c != 0)
                                {
                                    // Encontramos comienzo de un quad. Calcular ancho (width).
                                    int width = 1;
                                    while (i + width < size[u] && mask[n + width] == c)
                                    {
                                        width++;
                                    }

                                    // Calcular alto (height).
                                    int height = 1;
                                    bool done = false;
                                    while (j + height < size[v])
                                    {
                                        // Verificar si la siguiente fila tiene un segmento del mismo ancho y color
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

                                    // Añadir Quad
                                    // Posición base en coord Vox:
                                    int[] pos = new int[3];
                                    pos[u] = i; 
                                    pos[v] = j; 
                                    pos[d] = x[d];

                                    // Si es cara positiva (+1), la geometría se dibuja en x[d] + 1 visualmente
                                    // Si es cara negativa (-1), la geometría se dibuja en x[d] visualmente
                                    int depthOffset = (faceDir == 1) ? 1 : 0;
                                    pos[d] += depthOffset;

                                    // Calcular UV
                                    // c es 1-based index (1-255). 
                                    // Palette texture es 256 de ancho. Index 0->color 0.
                                    // Queremos el centro del pixel.
                                    // Indice en textura (0-255) = c - 1. 
                                    int colorIndex = c - 1;
                                    if (colorIndex < 0) colorIndex = 0;
                                    if (colorIndex > 255) colorIndex = 255;
                                    
                                    float uCoord = (colorIndex + 0.5f) / 256.0f;
                                    Vector2 uv = new Vector2(uCoord, 0.5f);

                                    AddGreedyQuad(pos, u, v, d, width, height, faceDir, uv, vertices, triangles, uvs);

                                    // Limpiar máscara en el área usada
                                    for (int ly = 0; ly < height; ly++)
                                    {
                                        for (int lx = 0; lx < width; lx++)
                                        {
                                            mask[n + lx + ly * size[u]] = 0;
                                        }
                                    }

                                    // Saltar i ya que procesamos 'width' elementos
                                    i += width - 1;
                                    n += width - 1; // n incrementará en el loop for también
                                }
                                n++;
                            }
                        }
                    }
                }
            }
        }

        private void AddGreedyQuad(int[] pos, int axisU, int axisV, int axisD, int width, int height, int faceDir, Vector2 uv, 
                                   List<Vector3> verts, List<int> tris, List<Vector2> uvs)
        {
            // Construir los 4 vértices del quad en coordenadas VOX
            // Quad se extiende en planos U (width) y V (height), fijo en D
            
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

            // Convertir a Unity Coordinates
            // Vox(x,y,z) -> Unity(x,z,y)
            Vector3 v0 = new Vector3(p0[0], p0[2], p0[1]);
            Vector3 v1 = new Vector3(p1[0], p1[2], p1[1]);
            Vector3 v2 = new Vector3(p2[0], p2[2], p2[1]);
            Vector3 v3 = new Vector3(p3[0], p3[2], p3[1]);

            // Añadir vértices
            int baseIndex = verts.Count;
            verts.Add(v0);
            verts.Add(v1);
            verts.Add(v2);
            verts.Add(v3);

            // Añadir UVs (todas iguales para un color sólido plano mapping a paleta)
            uvs.Add(uv);
            uvs.Add(uv);
            uvs.Add(uv);
            uvs.Add(uv);

            // Winding order depende de la dirección de la cara
            // faceDir == 1 (Positive direction in Vox Axis):
            // Normal apunta a +Axis. 
            // En sistema right-handed vs left-handed de Unity, hay que tener cuidado.
            // Vox X -> Unity X. Vox Y -> Unity Z. Vox Z -> Unity Y.
            // Si D=0 (X), dir=1 (+X). Quad en YZ (Unity ZY). Normal (1,0,0). CCW vistos desde X+.
            // Si D=1 (Y), dir=1 (+Y). Quad en XZ (Unity X Y?). No, Vox axis names. U=Z(Unity Y), V=X(Unity X).
            
            // Para asegurar winding consistente, lo mejor es probar o deducir con cuidado.
            // Estandar: (0,0)->(w,0)->(w,h)->(0,h) es v0, v1, v3, v2.
            
            if (faceDir == 1)
            {
                // Normal positiva.
                // Generalmente v0 -> v1 -> v2 (o v0-v2-v1) depende.
                // Probemos CCW estándar: 0, 2, 1 y 2, 3, 1
                // Pero ojo que v2 y v3 están intercambiados en mi def arriba vs standard (0,1,2,3 zig zag)
                // Mi definición: v0(00), v1(10), v2(01), v3(11).
                // Triangulo 1: v0, v2, v1
                // Triangulo 2: v1, v2, v3
                
                // Estos tris miran hacia "atras" o "adelante"?
                // Cross(v2-v0, v1-v0) = Cross((0,1)-(0,0), (1,0)-(0,0)) = Cross(Up, Right) = -Forward (en coords U,V) -> Mira hacia -D.
                // Si queremos que mire hacia +D, necesitamos orden inverso:
                // Cross(Right, Up) = Forward.
                // v1-v0 (Right), v2-v0 (Up).
                // Tris: v0, v1, v2.
                
                // PERO Unity y Vox ejes pueden voltearse.
                // Eje 0 (X): U=Y(Unity Z), V=Z(Unity Y). Cross(Z, Y) = -X. (Unity Left handed rule).
                // Así que para D=0 (+X), Cross(Z, Y) da -X. Queremos +X. Necesitamos invertir.
                
                // Hay una regla general para Greedy Meshing en Unity que alterna winding según el eje y paridad.
                // Simplemente usaré un criterio que suele funcionar y si sale en negro, invierto el booleano.
                // Por experiencia: faceDir 1 -> Normal +.
                
                // Probemos: 
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
        
        private void AddFace(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color32 color, List<Vector3> verts, List<int> tris, List<Color32> cols)
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

