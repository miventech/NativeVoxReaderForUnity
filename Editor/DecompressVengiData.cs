using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

public class DecompressVengiData : MonoBehaviour
{
    public string filePath;
    [ContextMenu("Decompress Vengi Data")]
    public void Decompress()
    {
        string PathToSave = filePath + ".bin";

        using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
        using (BinaryReader reader = new BinaryReader(fs))
        {
            // 1. Header "VENG"
            byte[] header = reader.ReadBytes(4);
            string headerString = Encoding.ASCII.GetString(header);
            if (headerString != "VENG")
            {
                Debug.LogError("Invalid VENGI header.");
                return;
            }
            else
            {
                Debug.Log("Valid VENGI header found.");
            }

            fs.Seek(2, SeekOrigin.Current);

            using (DeflateStream dStream = new DeflateStream(fs, CompressionMode.Decompress))
            using (BinaryReader dReader = new BinaryReader(dStream))
            {
                using (FileStream outFile = File.Create(PathToSave))
                {
                    using (BinaryWriter writer = new BinaryWriter(outFile))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = dReader.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            writer.Write(buffer, 0, bytesRead);
                        }
                        Debug.Log("Decompression complete. Output saved to: " + PathToSave);
                    }
                }
            }
        }
    }

}