using UnityEngine;
using Miventech.NativeUnityVoxReader;
using Miventech.NativeUnityVoxReader.Data;

/// <summary>
/// Abstract base class for building Unity Meshes from VoxFile data.
/// Implementations should convert voxel data into Unity Mesh objects.
/// </summary>
namespace Miventech.NativeUnityVoxReader.Abstract
{
    public abstract class VoxCreateObjectAbstract: MonoBehaviour
    {
        /// <summary>
        /// Builds a Unity Mesh from a given VoxModel and palette.
        /// </summary>
        /// <param name="model">The VoxModel containing voxel data.</param>
        /// <param name="palette">The color palette to use for voxels.</param>
        /// <returns>A Unity Mesh representing the voxel model.</returns>
        public abstract void BuildObject(VoxModel model, Color32[] palette);
    }
}

