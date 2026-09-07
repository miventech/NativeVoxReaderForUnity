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
        // Per-animated-shape keyframe streams. Non-null but empty when the file
        // has no animation — consumers that don't touch this list get the same
        // behavior as the pre-animation reader. Each entry corresponds to a
        // single nSHP whose parent chain contains keyframe animation OR whose
        // own numModels > 1.
        public List<VoxShapeAnimation> animations = new List<VoxShapeAnimation>();
        public AdvanceColor[] palette = new AdvanceColor[256]; // MagicaVoxel uses a 256-color palette
        // True once an RGBA chunk was read from the file. Needed to distinguish the
        // white placeholder palette from a real one when applying the default fallback.
        public bool paletteLoaded = false;

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


