using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Miventech.NativeVoxReader.Data;
using Miventech.NativeVoxReader.Readers.Data;

namespace Miventech.NativeVoxReader.Readers
{
    public class ReaderVoxFile : BaseReaderFile
    {
        public override bool IsValidFile(string path)
        {
            string extension = Path.GetExtension(path).ToLower();
            if (extension != ".vox") return false;
            return true;
        }

        public override VoxFile Read(string path)
        {
            var loadedVoxFile = ParseVoxFile(path);
            if (loadedVoxFile != null)
            {
                if (BaseReaderFile.VerboseLogging) Debug.Log($"Loaded VOX file with {loadedVoxFile.models.Count} models.");
                // Default palette fallback: only when the file shipped no RGBA chunk.
                if (!loadedVoxFile.paletteLoaded)
                {
                    loadedVoxFile.palette = GetDefaultPalette();
                }
            }
            return loadedVoxFile;
        }

        private static VoxFile ParseVoxFile(string path)
        {
            VoxFile voxFile = new VoxFile();
            List<VoxNode> allNodes = new List<VoxNode>();

            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                try
                {
                    // 1. Validate Header "VOX "
                    string header = new string(reader.ReadChars(4));
                    if (header != "VOX ")
                    {
                        Debug.LogError("Error: Invalid VOX header.");
                        return null;
                    }

                    // 2. Version
                    voxFile.version = reader.ReadInt32();

                    // 3. Main Chunk reading loop
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        ReadChunk(reader, voxFile, allNodes);
                    }

                    ApplyTransformations(voxFile, allNodes);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing VOX file: {e.Message}");
                    return null;
                }
            }

            return voxFile;
        }

        private static void ReadChunk(BinaryReader reader, VoxFile voxFile, List<VoxNode> allNodes)
        {
            string chunkId = new string(reader.ReadChars(4));
            int contentSize = reader.ReadInt32();
            int childrenSize = reader.ReadInt32();

            long startChunkPosition = reader.BaseStream.Position;

            switch (chunkId)
            {
                case "MAIN":
                    // MAIN chunk is a container; continue reading for children (SIZE, XYZI, etc.)
                    break;

                case "SIZE":
                    int sizeX = reader.ReadInt32();
                    int sizeY = reader.ReadInt32();
                    int sizeZ = reader.ReadInt32();

                    VoxModel newModel = new VoxModel();
                    newModel.size = new Vector3Int(sizeX, sizeY, sizeZ);
                    voxFile.models.Add(newModel);
                    break;

                case "XYZI":
                    if (voxFile.models.Count > 0)
                    {
                        VoxModel currentModel = voxFile.models[voxFile.models.Count - 1];
                        int numVoxels = reader.ReadInt32();
                        currentModel.voxels = new Voxel[numVoxels];

                        for (int i = 0; i < numVoxels; i++)
                        {
                            byte x = reader.ReadByte();
                            byte y = reader.ReadByte();
                            byte z = reader.ReadByte();
                            byte colorIndex = reader.ReadByte();
                            currentModel.voxels[i] = new Voxel(x, y, z, colorIndex);
                        }
                    }
                    else
                    {
                        // Consume data if XYZI is found without a preceding SIZE (unexpected)
                        reader.ReadBytes(contentSize);
                    }
                    break;

                case "RGBA":
                    for (int i = 0; i < 256; i++)
                    {
                        byte r = reader.ReadByte();
                        byte g = reader.ReadByte();
                        byte b = reader.ReadByte();
                        byte a = reader.ReadByte();
                        voxFile.palette[i] = new Color32(r, g, b, a);
                    }
                    voxFile.paletteLoaded = true;
                    break;

                case "nTRN":
                    TransformNode trn = new TransformNode();
                    trn.id = reader.ReadInt32();
                    trn.attributes = ReadDictionary(reader);
                    trn.childId = reader.ReadInt32();
                    reader.ReadInt32(); // reserved
                    reader.ReadInt32(); // layer id
                    int numFrames = reader.ReadInt32();

                    // Pull _name from the outer attributes so leaf writes can
                    // preserve artist naming intent on the resulting VoxModel.
                    // MagicaVoxel stores it on the outer nTRN dict, not the
                    // frame dict.
                    if (trn.attributes != null && trn.attributes.TryGetValue("_name", out var trnName))
                        trn.name = trnName;

                    // Parse EVERY frame into the keyframes list. Frame 0 also
                    // writes to translation/rotation for backwards compat with
                    // frame-0-only consumers (VoxelTextureGenerator, the
                    // single-object build tool, etc.).
                    //
                    // MagicaVoxel writes _t (translation, "x y z"), _r (rotation,
                    // single byte in 24-orientation encoding), and _f (integer
                    // frame index — absent means frame 0) into each frame's
                    // attribute dict. Absent _t/_r = identity.
                    for (int f = 0; f < numFrames; f++)
                    {
                        var frameAttr = ReadDictionary(reader);
                        var kf = new TransformKeyframe();
                        if (frameAttr.TryGetValue("_f", out var fStr) && int.TryParse(fStr, out int fIdx))
                            kf.frameIndex = fIdx;
                        else
                            kf.frameIndex = f;   // Fallback: assume dense frames when _f is absent.
                        if (frameAttr.TryGetValue("_t", out var tStr))
                            kf.translation = ParseVector3Int(tStr);
                        if (frameAttr.TryGetValue("_r", out var rStr) && byte.TryParse(rStr, out byte rByte))
                            kf.rotation = DecodeMVRotationToUnityQuaternion(rByte);
                        trn.keyframes.Add(kf);

                        // Frame 0 also populates the single-frame fields.
                        if (f == 0)
                        {
                            trn.translation = kf.translation;
                            trn.rotation = kf.rotation;
                        }
                    }
                    allNodes.Add(trn);
                    break;

                case "nGRP":
                    GroupNode grp = new GroupNode();
                    grp.id = reader.ReadInt32();
                    grp.attributes = ReadDictionary(reader);
                    int numChildren = reader.ReadInt32();
                    for (int i = 0; i < numChildren; i++)
                    {
                        grp.childrenIds.Add(reader.ReadInt32());
                    }
                    allNodes.Add(grp);
                    break;

                case "nSHP":
                    ShapeNode shp = new ShapeNode();
                    shp.id = reader.ReadInt32();
                    shp.attributes = ReadDictionary(reader);
                    int numModels = reader.ReadInt32();
                    // Parse EVERY (modelId, _f) entry — model-swap animation
                    // stores numModels > 1 with a per-model _f keyframe index.
                    // Prior versions of this reader read only the first entry
                    // and let the outer "consume remaining bytes" padding
                    // catch the misalignment; that silently dropped every
                    // animation frame past the first. First entry still writes
                    // to shp.modelId for the frame-0 code path.
                    for (int m = 0; m < numModels; m++)
                    {
                        int mid = reader.ReadInt32();
                        var mAttr = ReadDictionary(reader);
                        var sk = new ShapeKeyframe { modelId = mid };
                        if (mAttr.TryGetValue("_f", out var fStr) && int.TryParse(fStr, out int fIdx))
                            sk.frameIndex = fIdx;
                        else
                            sk.frameIndex = m;
                        shp.keyframes.Add(sk);
                        if (m == 0) shp.modelId = mid;
                    }
                    allNodes.Add(shp);
                    break;

                default:
                    // Unknown or unimplemented chunk -> skip content
                    reader.ReadBytes(contentSize);
                    break;
            }

            // Ensure entire contentSize has been consumed
            long bytesRead = reader.BaseStream.Position - startChunkPosition;
            if (bytesRead < contentSize)
            {
                reader.ReadBytes((int)(contentSize - bytesRead));
            }
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            return new string(reader.ReadChars(length));
        }

        private static Dictionary<string, string> ReadDictionary(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var dict = new Dictionary<string, string>();
            for (int i = 0; i < count; i++)
            {
                string key = ReadString(reader);
                string value = ReadString(reader);
                dict[key] = value;
            }
            return dict;
        }

        private static Vector3Int ParseVector3Int(string v)
        {
            string[] parts = v.Split(' ');
            if (parts.Length >= 3)
            {
                // In VOX: parts[0]=X, parts[1]=Y (depth), parts[2]=Z (height)
                // For Unity: X=X, Y=Z (height), Z=Y (depth)
                return new Vector3Int(int.Parse(parts[0]), int.Parse(parts[2]), int.Parse(parts[1]));
            }
            return Vector3Int.zero;
        }

        /// <summary>
        /// Decode MagicaVoxel's nTRN rotation byte into a Unity quaternion,
        /// converting from the .vox spec's right-handed Z-up basis into Unity's
        /// left-handed Y-up basis in the same step so downstream accumulation is
        /// pure Unity math.
        ///
        /// <para>Byte layout per the .vox format spec:</para>
        /// <list type="bullet">
        ///   <item>bits 0-1 : index (0/1/2 = X/Y/Z) of the non-zero column in row 0</item>
        ///   <item>bits 2-3 : index of the non-zero column in row 1 (only two remain)</item>
        ///   <item>bit 4    : sign of row 0's non-zero (0 = +, 1 = -)</item>
        ///   <item>bit 5    : sign of row 1's non-zero</item>
        ///   <item>bit 6    : sign of row 2's non-zero</item>
        /// </list>
        /// This gives a 3×3 signed permutation matrix — one of 24 axis-aligned
        /// orientations. Row 2's column is determined by elimination (the one
        /// index of X/Y/Z not used by rows 0 and 1).
        ///
        /// <para>The basis change is applied as <c>R_unity = B * R_mv * B</c>
        /// where <c>B</c> swaps Y↔Z. Concretely: swap rows 1↔2 of the parsed
        /// matrix, then swap columns 1↔2 within each row. This preserves the
        /// physical rotation but expresses it against Unity's basis, so
        /// <c>Matrix4x4.rotation</c> extracts a correct <see cref="Quaternion"/>.
        /// Identity (<c>_r = 4</c>) round-trips to <see cref="Quaternion.identity"/>.</para>
        /// </summary>
        private static Quaternion DecodeMVRotationToUnityQuaternion(byte r)
        {
            int col0 = r & 0x03;
            int col1 = (r >> 2) & 0x03;
            int col2 = 3 - col0 - col1; // implied by elimination (0+1+2=3)

            // Validate: the three columns must be a permutation of {0,1,2}. Corrupted
            // files can carry out-of-range bytes (e.g. _r = 0) — fall back to identity
            // rotation instead of throwing and aborting the whole file parse.
            if (col0 > 2 || col1 > 2 || col0 == col1 || col2 < 0 || col2 > 2)
            {
                Debug.LogWarning($"Invalid MagicaVoxel rotation byte 0x{r:X2}; defaulting to identity rotation.");
                return Quaternion.identity;
            }

            float sign0 = ((r >> 4) & 1) == 0 ? 1f : -1f;
            float sign1 = ((r >> 5) & 1) == 0 ? 1f : -1f;
            float sign2 = ((r >> 6) & 1) == 0 ? 1f : -1f;

            // Build the MV-space rotation matrix as three rows.
            Vector3 row0 = Vector3.zero; row0[col0] = sign0;
            Vector3 row1 = Vector3.zero; row1[col1] = sign1;
            Vector3 row2 = Vector3.zero; row2[col2] = sign2;

            // Basis change B (swap Y↔Z). First swap rows 1↔2:
            Vector3 tmp = row1; row1 = row2; row2 = tmp;
            // Then swap columns Y↔Z within each row:
            (row0.y, row0.z) = (row0.z, row0.y);
            (row1.y, row1.z) = (row1.z, row1.y);
            (row2.y, row2.z) = (row2.z, row2.y);

            Matrix4x4 m = Matrix4x4.identity;
            m.SetRow(0, new Vector4(row0.x, row0.y, row0.z, 0));
            m.SetRow(1, new Vector4(row1.x, row1.y, row1.z, 0));
            m.SetRow(2, new Vector4(row2.x, row2.y, row2.z, 0));
            return m.rotation;
        }

        private static void ApplyTransformations(VoxFile voxFile, List<VoxNode> nodes)
        {
            // Quick ID → node mapping.
            Dictionary<int, VoxNode> nodeMap = new Dictionary<int, VoxNode>();
            foreach (var node in nodes) nodeMap[node.id] = node;

            // The scene-graph parent/child relationships live implicitly across
            // nGRP.childrenIds and nTRN.childId. Any node id that appears as a
            // child of another node is NOT a root; only unreferenced nTRN nodes
            // are true recursion roots.
            //
            // Previously the outer loop iterated every nTRN as if it were a
            // root, which caused "last writer wins" overwrites at the leaves —
            // grouped scenes lost their parent-chain offsets (and any rotation
            // never made it through at all because _r wasn't parsed). Restricting
            // to real roots fixes the position accumulation cleanly.
            HashSet<int> childIds = new HashSet<int>();
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case TransformNode t: childIds.Add(t.childId); break;
                    case GroupNode g:
                        foreach (int c in g.childrenIds) childIds.Add(c);
                        break;
                }
            }

            foreach (var node in nodes)
            {
                if (node is TransformNode trn && !childIds.Contains(trn.id))
                {
                    // Root nTRN — start accumulation from identity.
                    // Feed the root's own translation/rotation/name in as the
                    // starting accumulator; downstream recursion composes each
                    // additional nTRN it walks through.
                    var chain = new List<TransformNode> { trn };
                    FindAndApplyToModel(
                        trn.childId,
                        (Vector3)trn.translation,
                        trn.rotation,
                        trn.name,
                        chain,
                        nodeMap,
                        voxFile);
                }
            }
        }

        private static void FindAndApplyToModel(
            int nodeId,
            Vector3 accumulatedPosition,
            Quaternion accumulatedRotation,
            string currentName,
            List<TransformNode> parentChain,
            Dictionary<int, VoxNode> nodeMap,
            VoxFile voxFile)
        {
            if (!nodeMap.ContainsKey(nodeId)) return;

            VoxNode node = nodeMap[nodeId];
            if (node is ShapeNode shp)
            {
                if (shp.modelId < voxFile.models.Count)
                {
                    // Leaf — commit the accumulated transform + most recent
                    // named nTRN to the model. Position stored as Vector3Int
                    // (rounded); voxel-scale scenes never need sub-voxel
                    // precision on model placement.
                    var model = voxFile.models[shp.modelId];
                    model.position = new Vector3Int(
                        Mathf.RoundToInt(accumulatedPosition.x),
                        Mathf.RoundToInt(accumulatedPosition.y),
                        Mathf.RoundToInt(accumulatedPosition.z));
                    model.rotation = accumulatedRotation;
                    // Only overwrite when we actually have a name — preserves
                    // whatever another traversal path might have set if the
                    // .vox reuses a model across shapes (uncommon but legal).
                    if (!string.IsNullOrEmpty(currentName)) model.name = currentName;

                    // Emit a VoxShapeAnimation entry when this shape or any
                    // ancestor nTRN carries keyframe animation. Static shapes
                    // (single-frame everywhere, single-model swap) are skipped
                    // — the frame-0 fields above are enough for them, and
                    // consumers that don't care about animation see an empty
                    // voxFile.animations list.
                    bool chainAnimated = false;
                    for (int i = 0; i < parentChain.Count && !chainAnimated; i++)
                        if (parentChain[i].keyframes.Count > 1) chainAnimated = true;
                    bool swapAnimated = shp.keyframes.Count > 1;

                    if (chainAnimated || swapAnimated)
                    {
                        var anim = new VoxShapeAnimation
                        {
                            primaryModelId = shp.modelId,
                            name = currentName,
                        };
                        if (chainAnimated)
                            anim.transformKeyframes = ComposeChainKeyframes(parentChain);
                        if (swapAnimated)
                        {
                            // Shallow-copy so downstream mutation of the
                            // returned list can't scramble the reader's
                            // internal ShapeNode state.
                            for (int i = 0; i < shp.keyframes.Count; i++)
                                anim.shapeKeyframes.Add(shp.keyframes[i]);
                        }
                        voxFile.animations.Add(anim);
                    }
                }
            }
            else if (node is GroupNode grp)
            {
                foreach (int childId in grp.childrenIds)
                {
                    FindAndApplyToModel(childId, accumulatedPosition, accumulatedRotation, currentName, parentChain, nodeMap, voxFile);
                }
            }
            else if (node is TransformNode nextTrn)
            {
                // Standard SE(3) composition: child's local translation is
                // rotated by the parent's world rotation before being added.
                // Rotation composes on the right (world = parent * local).
                Vector3 nextPos = accumulatedPosition + accumulatedRotation * (Vector3)nextTrn.translation;
                Quaternion nextRot = accumulatedRotation * nextTrn.rotation;
                string nextName = string.IsNullOrEmpty(nextTrn.name) ? currentName : nextTrn.name;
                parentChain.Add(nextTrn);
                FindAndApplyToModel(nextTrn.childId, nextPos, nextRot, nextName, parentChain, nodeMap, voxFile);
                parentChain.RemoveAt(parentChain.Count - 1);
            }
        }

        /// <summary>
        /// Compose the per-frame world transforms for a shape whose parent
        /// nTRN chain contains at least one animated node. Algorithm:
        /// <list type="number">
        ///   <item>Union all frame indices across every animated nTRN in the chain (indices sorted, deduplicated).</item>
        ///   <item>For each union frame index N, sample every nTRN's local (t, r) at frame N — lerp translation, slerp rotation between the node's own two adjacent keyframes (clamped at the endpoints).</item>
        ///   <item>Compose bottom-up (standard SE(3)): <c>world_t = parent_R * local_t + parent_t; world_R = parent_R * local_R</c>.</item>
        ///   <item>Emit one <see cref="TransformKeyframe"/> per union frame carrying (N, world_t, world_r).</item>
        /// </list>
        /// The composed stream is what the build tool and runtime playback
        /// system consume directly — no further hierarchy walk needed downstream.
        /// </summary>
        private static List<TransformKeyframe> ComposeChainKeyframes(List<TransformNode> chain)
        {
            // Union frame indices — small set, use SortedSet.
            SortedSet<int> unionFrames = new SortedSet<int>();
            for (int i = 0; i < chain.Count; i++)
            {
                var kfs = chain[i].keyframes;
                for (int k = 0; k < kfs.Count; k++)
                    unionFrames.Add(kfs[k].frameIndex);
            }

            var result = new List<TransformKeyframe>(unionFrames.Count);
            foreach (int N in unionFrames)
            {
                Vector3 worldT = Vector3.zero;
                Quaternion worldR = Quaternion.identity;
                for (int i = 0; i < chain.Count; i++)
                {
                    SampleLocalAtFrame(chain[i], N, out Vector3 localT, out Quaternion localR);
                    worldT += worldR * localT;
                    worldR = worldR * localR;
                }
                result.Add(new TransformKeyframe
                {
                    frameIndex = N,
                    translation = new Vector3Int(
                        Mathf.RoundToInt(worldT.x),
                        Mathf.RoundToInt(worldT.y),
                        Mathf.RoundToInt(worldT.z)),
                    rotation = worldR,
                });
            }
            return result;
        }

        /// <summary>
        /// Linearly interpolate an nTRN's local transform at an arbitrary
        /// frame index within its own keyframe list. Endpoints are clamped —
        /// frames before the first keyframe use the first, frames after the
        /// last use the last. Single-frame nTRNs (static nodes) always
        /// return their sole keyframe's values regardless of <paramref name="frame"/>.
        /// </summary>
        private static void SampleLocalAtFrame(TransformNode node, int frame, out Vector3 t, out Quaternion r)
        {
            var kfs = node.keyframes;
            if (kfs == null || kfs.Count == 0)
            {
                t = (Vector3)node.translation;
                r = node.rotation;
                return;
            }
            if (kfs.Count == 1 || frame <= kfs[0].frameIndex)
            {
                t = (Vector3)kfs[0].translation;
                r = kfs[0].rotation;
                return;
            }
            if (frame >= kfs[kfs.Count - 1].frameIndex)
            {
                var last = kfs[kfs.Count - 1];
                t = (Vector3)last.translation;
                r = last.rotation;
                return;
            }
            // Find the two adjacent keyframes bracketing `frame`.
            for (int i = 0; i < kfs.Count - 1; i++)
            {
                var a = kfs[i];
                var b = kfs[i + 1];
                if (frame >= a.frameIndex && frame <= b.frameIndex)
                {
                    float span = Mathf.Max(1, b.frameIndex - a.frameIndex);
                    float u = (frame - a.frameIndex) / span;
                    t = Vector3.Lerp((Vector3)a.translation, (Vector3)b.translation, u);
                    r = Quaternion.Slerp(a.rotation, b.rotation, u);
                    return;
                }
            }
            // Unreachable given the clamps above, but keep the compiler happy.
            t = (Vector3)node.translation;
            r = node.rotation;
        }

        private static AdvanceColor[] GetDefaultPalette()
        {
            // MagicaVoxel default palette fallback
            AdvanceColor[] palette = new AdvanceColor[256];
            for (int i = 0; i < 256; i++)
            {
                uint color = DefaultPalette[i];
                byte r = (byte)(color & 0xFF);
                byte g = (byte)((color >> 8) & 0xFF);
                byte b = (byte)((color >> 16) & 0xFF);
                byte a = (byte)((color >> 24) & 0xFF);
                palette[i] = new AdvanceColor(new Color32(r, g, b, 255), 0, 0, null); // Default Alpha 255
            }
            return palette;
        }

        private static readonly uint[] DefaultPalette = new uint[]
        {
        0x00000000, 0xffffffff, 0xffccffff, 0xff99ffff, 0xff66ffff, 0xff33ffff, 0xff00ffff, 0xffffccff, 0xffccccff, 0xff99ccff, 0xff66ccff, 0xff33ccff, 0xff00ccff, 0xffff99ff, 0xffcc99ff, 0xff9999ff,
        0xff6699ff, 0xff3399ff, 0xff0099ff, 0xffff66ff, 0xffcc66ff, 0xff9966ff, 0xff6666ff, 0xff3366ff, 0xff0066ff, 0xffff33ff, 0xffcc33ff, 0xff9933ff, 0xff6633ff, 0xff3333ff, 0xff0033ff, 0xffff00ff,
        0xffcc00ff, 0xff9900ff, 0xff6600ff, 0xff3300ff, 0xff0000ff, 0xffffffcc, 0xffccffcc, 0xff99ffcc, 0xff66ffcc, 0xff33ffcc, 0xff00ffcc, 0xffffcccc, 0xffcccccc, 0xff99cccc, 0xff66cccc, 0xff33cccc,
        0xff00cccc, 0xffff99cc, 0xffcc99cc, 0xff9999cc, 0xff6699cc, 0xff3399cc, 0xff0099cc, 0xffff66cc, 0xffcc66cc, 0xff9966cc, 0xff6666cc, 0xff3366cc, 0xff0066cc, 0xffff33cc, 0xffcc33cc, 0xff9933cc,
        0xff6633cc, 0xff3333cc, 0xff0033cc, 0xffff00cc, 0xffcc00cc, 0xff9900cc, 0xff6600cc, 0xff3300cc, 0xff0000cc, 0xffffff99, 0xffccff99, 0xff99ff99, 0xff66ff99, 0xff33ff99, 0xff00ff99, 0xffffcc99,
        0xffcccc99, 0xff99cc99, 0xff66cc99, 0xff33cc99, 0xff00cc99, 0xffff9999, 0xffcc9999, 0xff999999, 0xff669999, 0xff339999, 0xff009999, 0xffff6699, 0xffcc6699, 0xff996699, 0xff666699, 0xff336699,
        0xff006699, 0xffff3399, 0xffcc3399, 0xff993399, 0xff663399, 0xff333399, 0xff003399, 0xffff0099, 0xffcc0099, 0xff990099, 0xff660099, 0xff330099, 0xff000099, 0xffffff66, 0xffccff66, 0xff99ff66,
        0xff66ff66, 0xff33ff66, 0xff00ff66, 0xffffcc66, 0xffcccc66, 0xff99cc66, 0xff66cc66, 0xff33cc66, 0xff00cc66, 0xffff9966, 0xffcc9966, 0xff999966, 0xff669966, 0xff339966, 0xff009966, 0xffff6666,
        0xffcc6666, 0xff996666, 0xff666666, 0xff336666, 0xff006666, 0xffff3366, 0xffcc3366, 0xff993366, 0xff663366, 0xff333366, 0xff003366, 0xffff0066, 0xffcc0066, 0xff990066, 0xff660066, 0xff330066,
        0xff000066, 0xffffff33, 0xffccff33, 0xff99ff33, 0xff66ff33, 0xff33ff33, 0xff00ff33, 0xffffcc33, 0xffcccc33, 0xff99cc33, 0xff66cc33, 0xff33cc33, 0xff00cc33, 0xffff9933, 0xffcc9933, 0xff999933,
        0xff669933, 0xff339933, 0xff009933, 0xffff6633, 0xffcc6633, 0xff996633, 0xff666633, 0xff336633, 0xff006633, 0xffff3333, 0xffcc3333, 0xff993333, 0xff663333, 0xff333333, 0xff003333, 0xffff0033,
        0xffcc0033, 0xff990033, 0xff660033, 0xff330033, 0xff000033, 0xffffff00, 0xffccff00, 0xff99ff00, 0xff66ff00, 0xff33ff00, 0xff00ff00, 0xffffcc00, 0xffcccc00, 0xff99cc00, 0xff66cc00, 0xff33cc00,
        0xff00cc00, 0xffff9900, 0xffcc9900, 0xff999900, 0xff669900, 0xff339900, 0xff009900, 0xffff6600, 0xffcc6600, 0xff996600, 0xff666600, 0xff336600, 0xff006600, 0xffff3300, 0xffcc3300, 0xff993300,
        0xff663300, 0xff333300, 0xff003300, 0xffff0000, 0xffcc0000, 0xff990000, 0xff660000, 0xff330000, 0xff0000ee, 0xff0000dd, 0xff0000cc, 0xff0000bb, 0xff0000aa, 0xff000099, 0xff000088, 0xff000077,
        0xff000066, 0xff000055, 0xff000044, 0xff000033, 0xff000022, 0xff000011, 0xff00ee00, 0xff00dd00, 0xff00cc00, 0xff00bb00, 0xff00aa00, 0xff009900, 0xff008800, 0xff007700, 0xff006600, 0xff005500,
        0xff004400, 0xff003300, 0xff002200, 0xff001100, 0xffee0000, 0xffdd0000, 0xffcc0000, 0xffbb0000, 0xffaa0000, 0xff990000, 0xff880000, 0xff770000, 0xff660000, 0xff550000, 0xff440000, 0xff330000,
        0xff220000, 0xff110000, 0xffeeeeee, 0xffdddddd, 0xffcccccc, 0xffbbbbbb, 0xffaaaaaa, 0xff999999, 0xff888888, 0xff777777, 0xff666666, 0xff555555, 0xff444444, 0xff333333, 0xff222222, 0xff111111
        };


    }
}

