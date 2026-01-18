using UnityEngine;

namespace Miventech.NativeUnityVoxReader.Data
{
    // Representa un solo vóxel en coordenadas locales
    [System.Serializable]
    public struct Voxel
    {
        public byte x;
        public byte y;
        public byte z;
        public byte colorIndex; // Índice en la paleta (1-255)

        public Voxel(byte x, byte y, byte z, byte colorIndex)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.colorIndex = colorIndex;
        }
    }
}


