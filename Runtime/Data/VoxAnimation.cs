using System.Collections.Generic;
using UnityEngine;

namespace Miventech.NativeVoxReader.Data
{
    /// <summary>
    /// One frame of a MagicaVoxel nTRN keyframe stream. Translation and rotation
    /// are in Unity coordinates (post basis swap) but represent the nTRN's LOCAL
    /// transform relative to its parent — the reader composes the parent chain
    /// on top when it emits the per-shape animation stream.
    /// </summary>
    [System.Serializable]
    public class TransformKeyframe
    {
        public int frameIndex;
        public Vector3Int translation;
        public Quaternion rotation;

        public TransformKeyframe()
        {
            rotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// One frame of a MagicaVoxel nSHP model-swap stream. <c>modelId</c> is an
    /// index into <see cref="VoxFile.models"/>, so multiple keyframes referencing
    /// the same modelId share the same underlying voxel dataset — natural
    /// deduplication at the .vox level.
    /// </summary>
    [System.Serializable]
    public class ShapeKeyframe
    {
        public int frameIndex;
        public int modelId;
    }

    /// <summary>
    /// Per-shape animation record surfaced by the reader when any part of the
    /// shape's parent chain (or the shape itself) carries more than a single
    /// keyframe. Purely additive to the existing per-model position/rotation
    /// data — consumers that don't need animation ignore <see cref="VoxFile.animations"/>
    /// entirely and get identical behavior to the pre-animation reader.
    ///
    /// <para><b>Transform stream</b> (<see cref="transformKeyframes"/>): world
    /// transforms per keyframe, already composed through the shape's parent
    /// nTRN chain and expressed in Unity coordinates. The union of every animated
    /// nTRN's frame indices in the chain forms the keyframe set; each node's
    /// per-frame local transform is interpolated (lerp for translation, slerp
    /// for rotation) between its own adjacent keyframes, then composed
    /// bottom-up. Empty when no nTRN in the chain has more than one keyframe.</para>
    ///
    /// <para><b>Swap stream</b> (<see cref="shapeKeyframes"/>): the shape's own
    /// per-frame modelId sequence. Empty when the shape has a single model.
    /// Deduplication is implicit — multiple keyframes with the same modelId share
    /// the corresponding <see cref="VoxFile.models"/> entry.</para>
    /// </summary>
    [System.Serializable]
    public class VoxShapeAnimation
    {
        /// <summary>Model referenced at the shape's first keyframe (or the only model when the shape isn't a swap-animation).</summary>
        public int primaryModelId;

        /// <summary>Nearest ancestor nTRN's <c>_name</c> attribute. Null when the shape's chain has no named ancestor.</summary>
        public string name;

        /// <summary>World transform per keyframe, composed through the parent chain. Empty when the transform is static across the whole timeline.</summary>
        public List<TransformKeyframe> transformKeyframes = new List<TransformKeyframe>();

        /// <summary>Model-swap sequence. Empty when the shape has a single model.</summary>
        public List<ShapeKeyframe> shapeKeyframes = new List<ShapeKeyframe>();

        /// <summary>True when either stream carries actual keyframes beyond one entry — a quick gate for consumers.</summary>
        public bool HasAnimation =>
            (transformKeyframes != null && transformKeyframes.Count > 1) ||
            (shapeKeyframes    != null && shapeKeyframes.Count    > 1);
    }
}
