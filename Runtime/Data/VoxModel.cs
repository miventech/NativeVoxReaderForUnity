using UnityEngine;

namespace Miventech.NativeVoxReader.Data
{
    // Represents an individual model within the VOX file (corresponds to SIZE and XYZI chunks)
    [System.Serializable]
    public class VoxModel
    {
        public Vector3Int size; // Model dimensions
        public Vector3Int position; // Model position in the world
        public Voxel[] voxels;  // List of voxels it contains
        public bool UsePaletteCustom;
        public AdvanceColor[] CustomPalette;
        public VoxModel()
        {
            size = Vector3Int.zero;
            position = Vector3Int.zero;
            voxels = new Voxel[0];
        }

        public VoxModel(bool usePaletteCustom)
        {
            size = Vector3Int.zero;
            position = Vector3Int.zero;
            voxels = new Voxel[0];

            if (usePaletteCustom)
            {
                UsePaletteCustom = true;
                CustomPalette = new AdvanceColor[256];
                for (int i = 0; i < 256; i++)
                {
                    CustomPalette[i] = new Color(255,255,255,255); // Placeholder, should be set to actual colors
                }
            }
        }
    }
}


