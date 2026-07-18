using UnityEngine;

namespace Miventech.NativeVoxReader.Data
{
    // Represents an individual model within the VOX file (corresponds to SIZE and XYZI chunks)
    [System.Serializable]
    public class VoxModel
    {
        public Vector3Int size; // Model dimensions
        public Vector3Int position; // Model position in the world (Unity coords, post basis-swap)
        // Model orientation in the world, cumulative across the nTRN/nGRP hierarchy.
        // Already converted from the MagicaVoxel-native right-handed Z-up basis into
        // Unity's left-handed Y-up basis, so consumers can apply it directly to a
        // Transform.localRotation without further adjustment.
        public Quaternion rotation = Quaternion.identity;
        // Optional _name attribute from the nearest ancestor nTRN chunk. Null when
        // the artist didn't name the node. Consumers use this when they want to
        // preserve artist naming intent (e.g. multi-object builders naming their
        // GameObject children).
        public string name;
        public Voxel[] voxels;  // List of voxels it contains
        public bool UsePaletteCustom;
        public AdvanceColor[] CustomPalette;
        public VoxModel()
        {
            size = Vector3Int.zero;
            position = Vector3Int.zero;
            rotation = Quaternion.identity;
            voxels = new Voxel[0];
        }

        public VoxModel(bool usePaletteCustom)
        {
            size = Vector3Int.zero;
            position = Vector3Int.zero;
            rotation = Quaternion.identity;
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


