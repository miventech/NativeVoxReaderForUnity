using UnityEngine;
using Miventech.NativeVoxReader.Data;
using Miventech.NativeVoxReader.VoxRenderer;
using System;
using System.Linq;

namespace Miventech.NativeVoxReader.VoxRenderer.Types
{
    /// <summary>
    /// Abstract base class for converting VoxFile data into Unity-compatible mesh results.
    /// </summary>
    public abstract class VoxRenderAbstract : MonoBehaviour
    {
        public abstract string RenderMethodName { get; }
        public abstract System.Type SettingsType { get; }

        /// <summary>
        /// Sets the settings for the renderer.
        /// </summary>
        public abstract void SetSettings(VoxRenderSettings settings);

        /// <summary>
        /// Converts the provided VoxFile into an array of VoxModelResult.
        /// Each element in the array corresponds to a model in the VoxFile.
        /// </summary>
        /// <param name="file">The voxel file data.</param>
        /// <param name="palette">The color palette to use.</param>
        /// <returns>An array of results containing meshes, textures, and materials.</returns>
        public abstract VoxModelResult[] Render(VoxFile file, Color32[] palette);

        /// <summary>
        /// Converts a single VoxModel into a VoxModelResult.
        /// </summary>
        /// <param name="model">The voxel model data.</param>
        /// <param name="palette">The color palette to use.</param>
        /// <returns>A result containing mesh, texture, and material.</returns>
        public abstract VoxModelResult RenderModel(VoxModel model, Color32[] palette);

        public static Type GetTypeByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return GetAllRenderTypes().FirstOrDefault(t => t.Name == name || t.FullName == name);
        }

        private static Type[] _cachedRenderTypes;
        public static Type[] GetAllRenderTypes()
        {
            if (_cachedRenderTypes != null) return _cachedRenderTypes;

            Type baseType = typeof(VoxRenderAbstract);
            _cachedRenderTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch (System.Reflection.ReflectionTypeLoadException e)
                    {
                        // Some types might fail to load if their dependencies are missing
                        return e.Types.Where(t => t != null);
                    }
                    catch (Exception)
                    {
                        return Array.Empty<Type>();
                    }
                })
                .Where(type => type != null && type.IsSubclassOf(baseType) && !type.IsAbstract)
                .ToArray();

            return _cachedRenderTypes;
        }
    }
}
