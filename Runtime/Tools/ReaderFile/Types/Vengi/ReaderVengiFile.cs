using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Miventech.NativeVoxReader.Data;

namespace Miventech.NativeVoxReader.Runtime.Tools.ReaderFile
{
    public class ReaderVengiFile : BaseReaderFile
    {
        public AdvanceColor[] TempPalette { get; private set; }
        public override bool IsValidFile(string path)
        {
            string extension = Path.GetExtension(path).ToLower();
            return extension == ".vengi";
        }

        public override VoxFile Read(string path)
        {
            TempPalette = null;
            Debug.Log("Reading VENGI file: " + path);
            VoxFile voxFile = new VoxFile();
            voxFile.models = new List<VoxModel>();
            TempPalette = null;
            // Initialize default palette (MagicaVoxel fallback) just in case PALC is missing
            for (int i = 0; i < 256; i++)
            {
                uint color = DefaultPalette[i];
                // Assuming DefaultPalette values are stored as 0xAARRGGBB or similar where swap is needed
                // Swapping R and B extraction to fix color mismatch
                byte b = (byte)(color & 0xFF);          // Was r
                byte g = (byte)((color >> 8) & 0xFF);   // Keeps g
                byte r = (byte)((color >> 16) & 0xFF);  // Was b
                byte a = (byte)((color >> 24) & 0xFF);
                
                // Force alpha 255 to avoid invisible voxels if default has 0 alpha
                voxFile.palette[i] = new Color32(r, g, b, 255);
            }

            try
            {
                using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    // 1. Header "VENG"
                    byte[] header = reader.ReadBytes(4);
                    string headerString = Encoding.ASCII.GetString(header);
                    if (headerString != "VENG")
                    {
                        Debug.LogError("Invalid VENGI header.");
                        return null;
                    }
                    else
                    {
                        Debug.Log("Valid VENGI header found.");
                    }

                    // 2. Compressed Stream
                    // Vengi uses zlib compression (Deflate + Zlib header).
                    // We skip the first 2 bytes of the Zlib header (CMF, FLG) to use DeflateStream.
                    fs.Seek(2, SeekOrigin.Current);

                    using (DeflateStream dStream = new DeflateStream(fs, CompressionMode.Decompress))
                    using (BinaryReader dReader = new BinaryReader(dStream))
                    {
                        ReadContent(dReader, voxFile);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing VENGI file: {e.Message}\n{e.StackTrace}");
                // Return what we have loaded so far
            }

            return voxFile;
        }

        private void ReadContent(BinaryReader reader, VoxFile voxFile)
        {
            // 3. Version (Global)
            uint version = reader.ReadUInt32();

            // 4. Read Chunks
            while (true)
            {
                string chunkMagic = ReadFourCC(reader);
                if (string.IsNullOrEmpty(chunkMagic)) break;

                bool handled = ProcessChunk(chunkMagic, reader, voxFile, version, Vector3Int.zero);
                if (!handled)
                {
                    Debug.LogWarning($"Stopping parse at unknown top-level chunk: {chunkMagic}");
                    break;
                }
            }
        }

        private bool ProcessChunk(string chunkMagic, BinaryReader reader, VoxFile voxFile, uint version, Vector3Int parentOffset)
        {
            Debug.Log("Processing chunk: " + chunkMagic);
            switch (chunkMagic)
            {
                case "NODE":
                    ReadNode(reader, voxFile, version, parentOffset);
                    return true;
                case "PALC":
                    // PALC can appear at root level in some versions or files
                    voxFile.palette = ReadPalette(reader, voxFile, version);
                    return true;
                default:
                    return false;
            }
        }

        private void ReadNode(BinaryReader reader, VoxFile voxFile, uint version, Vector3Int parentOffset)
        {
            // Node Header
            string name = ReadPascalString(reader);
            string type = ReadPascalString(reader);

            if (version >= 6)
            {
                // UUID (16 bytes)
                reader.ReadBytes(16);
            }

            if (version >= 2)
            {
                int fileNodeId = reader.ReadInt32();
                int referenceNodeId = reader.ReadInt32();
            }

            bool visible = reader.ReadBoolean();
            bool locked = reader.ReadBoolean();

            uint colorRGBA = reader.ReadUInt32(); // Node color

            Vector3Int currentOffset = parentOffset;
            if (version >= 3)
            {
                float px = reader.ReadSingle();
                float py = reader.ReadSingle();
                float pz = reader.ReadSingle();

                // Unity: X, Y, Z. Map straight and round to int for grid based model
                currentOffset += new Vector3Int(Mathf.RoundToInt(px), Mathf.RoundToInt(py), Mathf.RoundToInt(pz));
            }

            // Sub-chunks loop
            while (true)
            {
                string chunkMagic = ReadFourCC(reader);
                if (string.IsNullOrEmpty(chunkMagic)) break;
                Debug.Log("Reading chunk in NODE: " + chunkMagic);
                if (chunkMagic == "ENDN")
                {
                    break; // End of this node
                }

                bool handled = false;
                
                switch (chunkMagic)
                {
                    case "NODE":
                        ReadNode(reader, voxFile, version, currentOffset);
                        handled = true;
                        break;
                    case "DATA": // Main voxel data chunk for this node voxels, position,
                        ReadNodeData(reader, voxFile, version, currentOffset);
                        handled = true;
                        break;
                    case "PALC":
                        TempPalette = ReadPalette(reader, voxFile, version);
                        handled = true;
                        break;
                    case "PROP":
                        ReadProperties(reader);
                        handled = true;
                        break;
                    case "PALI":
                        ReadPali(reader);
                        handled = true;
                        break;
                    case "ANIM":
                        ReadAnim(reader, version);
                        handled = true;
                        break;
                    case "IKCO":
                        ReadIkco(reader);
                        handled = true;
                        break;
                    case "PALN":
                        ReadPaln(reader);
                        handled = true;
                        break;
                    default:
                        // Unknown size/structure details for these optional chunks 
                        // Unable to skip correctly without knowing structure
                        handled = false;
                        break;
                }

                if (!handled)
                {
                    Debug.LogWarning($"Unknown or unhandled chunk inside NODE: {chunkMagic}. Aborting node read to prevent desync.");
                    // Stop reading this node's children as we don't know where the next chunk starts
                    break;
                }
            }
        }

        private void ReadNodeData(BinaryReader reader, VoxFile voxFile, uint version, Vector3Int offset)
        {
            // Bounding Box
            int minX = reader.ReadInt32();
            int minY = reader.ReadInt32();
            int minZ = reader.ReadInt32();
            int maxX = reader.ReadInt32();
            int maxY = reader.ReadInt32();
            int maxZ = reader.ReadInt32();

            int sizeX = maxX - minX + 1;
            int sizeY = maxY - minY + 1;
            int sizeZ = maxZ - minZ + 1;

            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0) return;

            VoxModel model = new VoxModel();
            model.size = new Vector3Int(sizeX, sizeY, sizeZ);
            Vector3Int modelPosition = offset + new Vector3Int(minX, minZ, minY); // Swap Y and Z for Unity's coordinate system
            model.position = new Vector3Int(modelPosition.x, modelPosition.z, modelPosition.y);

            if (TempPalette != null)
            {
                model.UsePaletteCustom = true;
                model.CustomPalette = TempPalette;
            }
            
            List<Voxel> voxels = new List<Voxel>();

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        bool isAir = reader.ReadBoolean();
                        if (!isAir)
                        {
                            byte color = 0;
                            // byte normal = 0;
                            if (version >= 4)
                            {
                                color = reader.ReadByte();
                                byte normal = reader.ReadByte(); // consume normal
                            }
                            else
                            {
                                color = reader.ReadByte();
                            }

                            int localX = x - minX;
                            int localY = y - minY;
                            int localZ = z - minZ;

                            // Check bounds for byte
                            if (localX >= 0 && localX < 256 &&
                               localY >= 0 && localY < 256 &&
                               localZ >= 0 && localZ < 256)
                            {
                                voxels.Add(new Voxel((byte)localX, (byte)localZ,(byte)localY, (byte)(color + 1)));
                            }
                        }
                    }
                }
            }

            model.voxels = voxels.ToArray();
            voxFile.models.Add(model);
            TempPalette = null; // Clear temp palette after applying to model, as it should only apply to the next model if present
        }

        private AdvanceColor[] ReadPalette(BinaryReader reader, VoxFile voxFile, uint version)
        {
            ReadPascalString(reader); // Palette Name
            int count = reader.ReadInt32();

            Debug.Log($"Reading PALC with {count} colors. Version: {version}");
            AdvanceColor[] palette = new AdvanceColor[255];
            // 1. Colors
            for (int i = 0; i < count; i++)
            {
                byte r = reader.ReadByte();
                byte g = reader.ReadByte();
                byte b = reader.ReadByte();
                byte a = reader.ReadByte();
                
                // Keep alpha 255 for opaque rendering in Unity
                if (i < 256)
                {
                    palette[i] = new Color32(r, g, b, a);
                    Debug.Log("Read color " + i + ": " + palette[i] + $" (raw RGBA: {r},{g},{b},{a})");
                }
            }

            // 2. Emit Colors (Aux) - Count * 4 bytes (UInt32)
            // Verified: C++ source writes another UInt32 per color (usually 0 or emissions)
            for (int i = 0; i < count; i++)
            {
                reader.ReadUInt32();
            }

            // 3. read UI Indices (UInt8 per color) - NOT proper names, just indices for UI sorting

            for (int i = 0; i < count; i++)
            {
                palette[i].ColorIndex = reader.ReadByte();;
            }

            // 4: Read Names Materials
            for (int i = 0; i < count; i++)
            {
                palette[i].Name = ReadPascalString(reader); // Color Names (Strings) - Unverified structure, may not be present in all versions or files
            }


            //5: read again number colors for materials, seems to be duplicated count in some versions
            reader.ReadUInt32(); 


            //6: Read Material Types and Properties - Unverified structure, may not be present in all versions or files
            for (int i = 0; i < count; i++)
            {
                palette[i].MaterialType = reader.ReadInt32(); // Material Type (0=none, 1=diffuse, 2=metal, 3=glass, etc.)
                ushort MaterialMax = reader.ReadByte();
                for(ushort j = 0; j < MaterialMax; j++)
                {
                    string PropertyName = ReadPascalString(reader);
                    float PropertyValue = reader.ReadSingle();
                    palette[i].properties.Add(PropertyName, PropertyValue);
                    Debug.Log($"Material (type: {palette[i].MaterialType}) {i} Property: {PropertyName} : {PropertyValue}");
                }
            }
            return palette;
        }

        private void ReadProperties(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string NameProp = ReadPascalString(reader); // key
                string valueProp = ReadPascalString(reader); // value
                Debug.Log($"Node Property: {NameProp} : {valueProp}");
            }
        }

        private void ReadPali(BinaryReader reader)
        {
            ReadPascalString(reader); // Palette Name
        }

        private void ReadAnim(BinaryReader reader, uint version)
        {
            ReadPascalString(reader); // Animation Name

            while (true)
            {
                string chunkMagic = ReadFourCC(reader);
                if (string.IsNullOrEmpty(chunkMagic)) break;

                if (chunkMagic == "ENDA")
                {
                    break;
                }
                else if (chunkMagic == "KEYF")
                {
                    reader.ReadInt32(); // frame
                    reader.ReadBoolean(); // long rotation
                    ReadPascalString(reader); // interpolation

                    // Transform Matrix (4x4 = 16 floats)
                    for (int i = 0; i < 16; i++) reader.ReadSingle();

                    if (version <= 2)
                    {
                        // Pivot
                        reader.ReadSingle();
                        reader.ReadSingle();
                        reader.ReadSingle();
                    }
                }
                else
                {
                    Debug.LogWarning($"Unknown chunk inside ANIM: {chunkMagic}. Stopping animation read.");
                    break;
                }
            }
        }
        /// <summary>
        /// IK Constraints chunk, unverified structure based on limited documentation and examples. May not be fully correct or supported in all versions/files. Contains IK effector data and swing limits for animation rigs. Not currently used in Unity importer but read to maintain file integrity during parsing.
        /// </summary>
        /// <param name="reader"></param>
        private void ReadIkco(BinaryReader reader)
        {
            reader.ReadInt32(); // effectorNodeId
            reader.ReadSingle(); // rollMin
            reader.ReadSingle(); // rollMax
            reader.ReadBoolean(); // visible
            reader.ReadBoolean(); // anchor

            // Swing Limits
            // Note: Use UInt32 or Int32 depending on typical implementation logic
            // Spec said uint32, usually read as int32 in C# for loop
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                reader.ReadSingle(); // cx
                reader.ReadSingle(); // cy
                reader.ReadSingle(); // r
            }
        }

        private void ReadPaln(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            // Consume normals (count * 4 bytes)
            // Just skip them as we don't use them yet, or store if needed
            if (count > 0)
            {
                reader.ReadBytes(count * 4);
            }
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
        0xff004400, 0xff003300, 0xff002200, 0xff001100, 0xffee0000, 0xffdd0000, 0xffcc0000, 0xffbb0000, 0xffaa0000, 0xff990000, 0xff880000, 0xff777770, 0xff666666, 0xff555555, 0xff444444, 0xff333333, 0xff222222, 0xff111111
        };

        private string ReadPascalString(BinaryReader reader)
        {
            UInt16 length = reader.ReadUInt16();
            if (length == 0) return "";
            byte[] bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        private string ReadFourCC(BinaryReader reader)
        {
            try
            {
                byte[] bytes = reader.ReadBytes(4);
                if (bytes.Length < 4) return null;
                return Encoding.ASCII.GetString(bytes);
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }
    }
}