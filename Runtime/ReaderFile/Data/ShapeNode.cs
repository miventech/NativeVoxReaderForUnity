using System.Collections.Generic;
using Miventech.NativeVoxReader.Data;

namespace Miventech.NativeVoxReader.Tools.ReaderFile.Data
{
    internal class ShapeNode : VoxNode
    {
        // First-frame modelId — preserved so the frame-0 code path (existing
        // consumers) still works without touching the keyframes list.
        public int modelId;
        // Full model-swap sequence — one entry per numModels in the nSHP chunk.
        // Populated for EVERY nSHP (single-model shapes get one keyframe with
        // frameIndex=0 pointing at the sole modelId). Animation is present
        // when Count > 1.
        public List<ShapeKeyframe> keyframes = new List<ShapeKeyframe>();
    }
}


