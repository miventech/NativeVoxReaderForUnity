using UnityEngine;

namespace Miventech.NativeUnityVoxReader.Data
{
    // Representa un modelo individual dentro del archivo VOX (corresponde a los chunks SIZE y XYZI)
    [System.Serializable]
    public class VoxModel
    {
        public Vector3Int size; // Dimensiones del modelo
        public Vector3Int position; // Posición del modelo en el mundo
        public Voxel[] voxels;  // Lista de vóxeles que contiene

        public VoxModel()
        {
            size = Vector3Int.zero;
            position = Vector3Int.zero;
            voxels = new Voxel[0];
        }
    }
}


