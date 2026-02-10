using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Miventech.NativeVoxReader.Data;
using Miventech.NativeVoxReader.Tools.ReaderFile.Data;

namespace Miventech.NativeVoxReader.Runtime.Tools.ReaderFile
{
    public class ReaderVoxFile: BaseReaderFile
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
                Debug.Log($"Loaded VOX file with {loadedVoxFile.models.Count} models.");
                // Default palette fallback
                if (loadedVoxFile.palette[0].a == 0 && loadedVoxFile.palette[0].r == 0 && loadedVoxFile.palette[0].g == 0 && loadedVoxFile.palette[0].b == 0)
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
                    break;

                case "nTRN":
                    TransformNode trn = new TransformNode();
                    trn.id = reader.ReadInt32();
                    trn.attributes = ReadDictionary(reader);
                    trn.childId = reader.ReadInt32();
                    reader.ReadInt32(); // reserved
                    reader.ReadInt32(); // layer id
                    int numFrames = reader.ReadInt32();

                    // Using the first frame for the base position
                    if (numFrames > 0)
                    {
                        var frameAttr = ReadDictionary(reader);
                        if (frameAttr.ContainsKey("_t"))
                        {
                            trn.translation = ParseVector3Int(frameAttr["_t"]);
                        }
                        // Skip remaining frames (usually 1)
                        for (int i = 1; i < numFrames; i++) ReadDictionary(reader);
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
                    // Typically a Shape Node points to a single model
                    if (numModels > 0)
                    {
                        shp.modelId = reader.ReadInt32();
                        ReadDictionary(reader); // skip model attributes
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

        private static void ApplyTransformations(VoxFile voxFile, List<VoxNode> nodes)
        {
            // Quick ID to node mapping
            Dictionary<int, VoxNode> nodeMap = new Dictionary<int, VoxNode>();
            foreach (var node in nodes) nodeMap[node.id] = node;

            // Traverse TransformNodes to find child models
            foreach (var node in nodes)
            {
                if (node is TransformNode trn)
                {
                    FindAndApplyToModel(trn.childId, trn.translation, nodeMap, voxFile);
                }
            }
        }

        private static void FindAndApplyToModel(int nodeId, Vector3Int translation, Dictionary<int, VoxNode> nodeMap, VoxFile voxFile)
        {
            if (!nodeMap.ContainsKey(nodeId)) return;

            VoxNode node = nodeMap[nodeId];
            if (node is ShapeNode shp)
            {
                if (shp.modelId < voxFile.models.Count)
                {
                    voxFile.models[shp.modelId].position = translation;
                }
            }
            else if (node is GroupNode grp)
            {
                foreach (int childId in grp.childrenIds)
                {
                    FindAndApplyToModel(childId, translation, nodeMap, voxFile);
                }
            }
            else if (node is TransformNode nextTrn)
            {
                FindAndApplyToModel(nextTrn.childId, translation + nextTrn.translation, nodeMap, voxFile);
            }
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

