using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEngine;
using Miventech.NativeVoxReader.Data;

//TODO: I'm basing this on the repository https://github.com/Zarbuz/FileToVox/blob/master/SchematicToVoxCore/Converter/QbToSchematic.cs
//TODO HAHAHA SO FAR IT DOESN'T WORK, BUT IT SERVES AS A REFERENCE FOR READING THE NODES AND THE FILE STRUCTURE OF QBT/QBCL
namespace Miventech.NativeVoxReader.Runtime.Tools.ReaderFile
{
    public class ReaderQbclFile : BaseReaderFile
    {

        public override bool IsValidFile(string path)
        {
            string extension = Path.GetExtension(path).ToLower();
            if (extension != ".qb" || extension != ".qbt" || extension != ".qbcl") return false;
            return true;
        }

        public override VoxFile Read(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[ReaderQbclFile] File not found: {path}");
                return null;
            }

            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
                {
                    // Check Magic Bytes for QBT (Qubicle Binary Tree)
                    // Header: "QB 2" -> 0x32 0x20 0x42 0x51
                    byte[] magic = reader.ReadBytes(4);
                    // QBT check
                    bool isQbt = (magic[0] == 0x32 && magic[1] == 0x20 && magic[2] == 0x42 && magic[3] == 0x51);

                    // Also check for "Q" "B" " " "2" ascii just in case
                    if (!isQbt && magic[0] == 'Q' && magic[1] == 'B' && magic[2] == ' ' && magic[3] == '2') isQbt = true;

                    if (isQbt)
                    {
                        return ParseQbtFile(reader);
                    }
                    else
                    {
                        // Fallback: reset stream and try QB
                        reader.BaseStream.Seek(0, SeekOrigin.Begin);
                        return ParseQbFile(reader);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReaderQbclFile] Error parsing QBCL/QBT file: {e.Message}\n{e.StackTrace}");
                return null;
            }
        }

        // --- QBT (Qubicle Binary Tree) Implementation ---

        private static VoxFile ParseQbtFile(BinaryReader reader)
        {
            VoxFile voxFile = new VoxFile();
            voxFile.models = new List<VoxModel>();

            // Temporary palette construction
            Dictionary<uint, byte> colorToPaletteIndex = new Dictionary<uint, byte>();
            List<Color32> palette = new List<Color32>();
            palette.Add(new Color32(0, 0, 0, 0)); // Index 0 is empty

            // Header continued
            // Magic checked already.
            // Version Major (1 byte), Minor (1 byte)
            byte verMajor = reader.ReadByte();
            byte verMinor = reader.ReadByte();

            // Global Scale (3 floats)
            float globalScaleX = reader.ReadSingle();
            float globalScaleY = reader.ReadSingle();
            float globalScaleZ = reader.ReadSingle();

            // QBT creates a tree of nodes. We need to traverse it and flatten Matrices into VoxModels.

            ReadQbtNode(reader, voxFile, Vector3Int.zero, colorToPaletteIndex, palette);

            // Finalize Palette
            for (int i = palette.Count; i < 256; i++) palette.Add(new Color32(0, 0, 0, 0));
            voxFile.palette = palette.ToArray().ToAdvanceColorArray();

            return voxFile;
        }

        private static void ReadQbtNode(BinaryReader reader, VoxFile voxFile, Vector3Int parentPosition, Dictionary<uint, byte> colorMap, List<Color32> palette)
        {
            // Node Header
            uint typeId = reader.ReadUInt32(); // 0=Matrix, 1=Model, 2=Compound
            uint dataSize = reader.ReadUInt32();

            long startDataPos = reader.BaseStream.Position;

            // --- Node Data ---
            // Name
            uint nameLen = reader.ReadUInt32();
            string name = "";
            if (nameLen > 0)
            {
                name = new string(reader.ReadChars((int)nameLen));
            }

            // Props
            // Position (3 floats) - relative to parent
            float localPosX = reader.ReadSingle();
            float localPosY = reader.ReadSingle();
            float localPosZ = reader.ReadSingle();

            Vector3Int localPos = new Vector3Int(Mathf.RoundToInt(localPosX), Mathf.RoundToInt(localPosY), Mathf.RoundToInt(localPosZ));
            Vector3Int absolutePos = parentPosition + localPos;

            // Pivot (3 floats) 
            reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle();

            // Size (3 uint)
            uint sizeX = reader.ReadUInt32();
            uint sizeY = reader.ReadUInt32();
            uint sizeZ = reader.ReadUInt32();

            // Type Specific Data
            if (typeId == 0) // Matrix
            {
                uint compressedSize = reader.ReadUInt32();
                if (compressedSize > 0)
                {
                    byte[] compressedData = reader.ReadBytes((int)compressedSize);

                    // Decompress ZLib
                    // Skip first 2 bytes (0x78, ...) if it is standard zlib header for DeflateStream
                    // Valid Zlib header is usually 2 bytes. DeflateStream expects raw deflate stream.
                    int offset = 2;
                    if (compressedData.Length <= 2) offset = 0; // Safety check

                    using (MemoryStream ms = new MemoryStream(compressedData, offset, compressedData.Length - offset))
                    using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                    using (BinaryReader voxReader = new BinaryReader(ds))
                    {
                        // Data is X * Y * Z * 4 bytes (RGBA)
                        VoxModel model = new VoxModel();
                        model.size = new Vector3Int((int)sizeX, (int)sizeY, (int)sizeZ);
                        model.position = absolutePos;

                        List<Voxel> voxels = new List<Voxel>();

                        try
                        {
                            for (int x = 0; x < sizeX; x++)
                            {
                                for (int z = 0; z < sizeZ; z++)
                                {
                                    for (int y = 0; y < sizeY; y++)
                                    {
                                        byte r = voxReader.ReadByte();
                                        byte g = voxReader.ReadByte();
                                        byte b = voxReader.ReadByte();
                                        byte a = voxReader.ReadByte();

                                        if (a == 0) continue;

                                        uint colorHash = (uint)(r | (g << 8) | (b << 16) | (a << 24));
                                        byte pIndex;
                                        if (!colorMap.TryGetValue(colorHash, out pIndex))
                                        {
                                            if (palette.Count < 256)
                                            {
                                                pIndex = (byte)palette.Count;
                                                palette.Add(new Color32(r, g, b, a));
                                                colorMap[colorHash] = pIndex;
                                            }
                                            else pIndex = 1;
                                        }

                                        voxels.Add(new Voxel((byte)x, (byte)y, (byte)z, pIndex));
                                    }
                                }
                            }
                        }
                        catch (EndOfStreamException)
                        {
                            // Suppress end of stream if compressed data ended slightly early or padding issue
                        }

                        model.voxels = voxels.ToArray();
                        voxFile.models.Add(model);
                    }
                }
            }

            // Skip to end of Data Block
            long currentPos = reader.BaseStream.Position;
            long bytesRead = currentPos - startDataPos;
            if (bytesRead < dataSize)
            {
                reader.ReadBytes((int)(dataSize - bytesRead));
            }

            // --- Children ---
            uint childCount = reader.ReadUInt32();
            for (int i = 0; i < childCount; i++)
            {
                ReadQbtNode(reader, voxFile, absolutePos, colorMap, palette);
            }
        }

        // --- Legacy QB Implementation (Fallback) ---
        private const uint QB_CODEFLAG = 2;
        private const uint QB_NEXTSLICEFLAG = 6;

        private static VoxFile ParseQbFile(BinaryReader reader)
        {
            VoxFile voxFile = new VoxFile();
            Dictionary<uint, byte> colorToPaletteIndex = new Dictionary<uint, byte>();
            List<Color32> palette = new List<Color32>();

            palette.Add(new Color32(0, 0, 0, 0));

            uint version = reader.ReadUInt32();
            uint colorFormat = reader.ReadUInt32();
            uint zAxisOrientation = reader.ReadUInt32();
            uint compressed = reader.ReadUInt32();
            uint visibilityMaskEncoded = reader.ReadUInt32();
            uint numMatrices = reader.ReadUInt32();

            voxFile.version = (int)version;

            for (int i = 0; i < numMatrices; i++)
            {
                VoxModel model = ReadQbMatrix(reader, compressed == 1, colorFormat == 1, zAxisOrientation, colorToPaletteIndex, palette);
                if (model != null) voxFile.models.Add(model);
            }

            for (int i = palette.Count; i < 256; i++) palette.Add(new Color32(0, 0, 0, 0));
            voxFile.palette = palette.ToArray().ToAdvanceColorArray();

            return voxFile;
        }

        private static VoxModel ReadQbMatrix(BinaryReader reader, bool isCompressed, bool isBgra, uint zAxisOrientation, Dictionary<uint, byte> colorMap, List<Color32> palette)
        {
            VoxModel model = new VoxModel();
            byte nameLen = reader.ReadByte();
            string name = new string(reader.ReadChars(nameLen));

            uint sizeX = reader.ReadUInt32();
            uint sizeY = reader.ReadUInt32();
            uint sizeZ = reader.ReadUInt32();
            int posX = reader.ReadInt32();
            int posY = reader.ReadInt32();
            int posZ = reader.ReadInt32();

            model.size = new Vector3Int((int)sizeX, (int)sizeY, (int)sizeZ);
            model.position = new Vector3Int(posX, posY, posZ);

            List<Voxel> voxelList = new List<Voxel>();
            uint[,,] colorGrid = new uint[sizeX, sizeY, sizeZ];

            if (isCompressed) ReadQbCompressedData(reader, colorGrid, sizeX, sizeY, sizeZ);
            else ReadQbUncompressedData(reader, colorGrid, sizeX, sizeY, sizeZ);

            for (int z = 0; z < sizeZ; z++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int x = 0; x < sizeX; x++)
                    {
                        uint colorVal = colorGrid[x, y, z];
                        if (colorVal == 0) continue;

                        byte alpha = (byte)((colorVal >> 24) & 0xFF);
                        if (alpha == 0) continue;

                        byte r, g, b;
                        if (isBgra) { b = (byte)((colorVal) & 0xFF); g = (byte)((colorVal >> 8) & 0xFF); r = (byte)((colorVal >> 16) & 0xFF); }
                        else { r = (byte)((colorVal) & 0xFF); g = (byte)((colorVal >> 8) & 0xFF); b = (byte)((colorVal >> 16) & 0xFF); }

                        uint packedColorHash = (uint)(r | (g << 8) | (b << 16) | (alpha << 24));
                        byte index;

                        if (!colorMap.TryGetValue(packedColorHash, out index))
                        {
                            if (palette.Count < 256)
                            {
                                index = (byte)palette.Count;
                                palette.Add(new Color32(r, g, b, alpha));
                                colorMap[packedColorHash] = index;
                            }
                            else index = 1;
                        }
                        voxelList.Add(new Voxel((byte)x, (byte)y, (byte)z, index));
                    }
                }
            }
            model.voxels = voxelList.ToArray();
            return model;
        }

        private static void ReadQbUncompressedData(BinaryReader reader, uint[,,] grid, uint sx, uint sy, uint sz)
        {
            for (int z = 0; z < sz; z++)
                for (int y = 0; y < sy; y++)
                    for (int x = 0; x < sx; x++)
                        grid[x, y, z] = reader.ReadUInt32();
        }

        private static void ReadQbCompressedData(BinaryReader reader, uint[,,] grid, uint sx, uint sy, uint sz)
        {
            int x = 0, y = 0, z = 0;
            while (z < sz)
            {
                uint data = reader.ReadUInt32();
                if (data == QB_NEXTSLICEFLAG) { z++; y = 0; x = 0; continue; }

                uint count = 1;
                uint color = data;
                if (data == QB_CODEFLAG) { count = reader.ReadUInt32(); color = reader.ReadUInt32(); }

                for (int i = 0; i < count; i++)
                {
                    if (z >= sz) break;
                    grid[x, y, z] = color;
                    x++; if (x >= sx) { x = 0; y++; if (y >= sy) { y = 0; z++; } }
                }
            }
        }


    }
}
