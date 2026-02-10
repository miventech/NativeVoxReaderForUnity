using UnityEngine;
using System.Collections.Generic;

namespace Miventech.NativeVoxReader.Data
{
    // Main container for all information read from the file
    [System.Serializable]
    public class VoxFile
    {
        public int version;
        public List<VoxModel> models = new List<VoxModel>();
        public AdvanceColor[] palette = new AdvanceColor[256]; // MagicaVoxel uses a 256-color palette

        public VoxFile()
        {
            // Initialize default or empty palette
            for (int i = 0; i < 256; i++)
            {
                palette[i] = Color.white; // Placeholder
            }
        }
    }
}


