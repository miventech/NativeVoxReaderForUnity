using UnityEngine;
using System.Collections.Generic;

namespace Miventech.NativeUnityVoxReader.Data
{
    // Contenedor principal de toda la información leída del archivo
    [System.Serializable]
    public class VoxFile
    {
        public int version;
        public List<VoxModel> models = new List<VoxModel>();
        public Color32[] palette = new Color32[256]; // MagicaVoxel usa una paleta de 256 colores

        public VoxFile()
        {
            // Inicializar paleta por defecto o vacía
            for (int i = 0; i < 256; i++)
            {
                palette[i] = Color.white; // Placeholder
            }
        }
    }
}


