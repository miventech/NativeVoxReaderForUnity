using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;
using Miventech.NativeVoxReader.Data;
using Miventech.NativeVoxReader.VoxRenderer;
using Miventech.NativeVoxReader.VoxRenderer.Types;
using Miventech.NativeVoxReader.VoxRenderer.Types.VoxFileBakeTexture;
using Miventech.NativeVoxReader.Readers;

namespace Miventech.NativeVoxReader.Editor
{
    /// <summary>
    /// Custom importer to handle .vox files as native 3D assets in Unity.
    /// This allows dragging .vox files directly into the scene or using them as prefabs.
    /// </summary>
    [ScriptedImporter(1, "vox")]
    public class NativeVoxImporter : ScriptedImporter
    {
        [HideInInspector]
        public string selectedRenderType = "VoxBakedTextureRenderer";

        public bool overridePalette = false;
        public Color32[] customPalette;

        [SerializeReference]
        public VoxRenderSettings settings;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            // 1. Read the .vox file data
            VoxFile loadedVoxFile = new ReaderVoxFile().Read(ctx.assetPath);
            if (loadedVoxFile == null)
            {
                Debug.LogError($"VoxImporter: Failed to read vox file at {ctx.assetPath}");
                return;
            }

            // 2. Resolve the renderer type
            System.Type rendererType = VoxRenderAbstract.GetTypeByName(selectedRenderType);
            if (rendererType == null)
            {
                // Fallback to Baked Texture if not found
                rendererType = typeof(VoxBakedTextureRenderer);
            }

            // 3. Create a temporary renderer instance
            GameObject tempGo = new GameObject("TempVoxRenderer");
            tempGo.hideFlags = HideFlags.HideAndDontSave;
            VoxRenderAbstract renderer = (VoxRenderAbstract)tempGo.AddComponent(rendererType);

            // 4. Configure and Render
            Color32[] palette = loadedVoxFile.palette.ToColor32Array();
            if (overridePalette && customPalette != null && customPalette.Length == palette.Length)
            {
                palette = customPalette;
            }
            else if (overridePalette)
            {
                // Init customPalette if sizes don't match
                customPalette = (Color32[])palette.Clone();
                palette = customPalette;
            }

            // Ensure settings are valid and match the renderer
            if (settings == null || settings.GetType() != renderer.SettingsType)
            {
                settings = (VoxRenderSettings)System.Activator.CreateInstance(renderer.SettingsType);
            }

            renderer.SetSettings(settings);
            VoxModelResult[] results = renderer.Render(loadedVoxFile, palette);

            if (results == null)
            {
                Debug.LogWarning($"VoxImporter: No models found in {ctx.assetPath}");
                Object.DestroyImmediate(tempGo);
                return;
            }

            // 5. Create the root GameObject
            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));

            // Extract scale from settings if it exists (using reflection for convenience in base class)
            float currentScale = 0.1f;
            var scaleField = settings.GetType().GetField("Scale") ?? settings.GetType().GetField("scale");
            if (scaleField != null) currentScale = (float)scaleField.GetValue(settings);

            // 6. Process results
            for (int i = 0; i < results.Length; i++)
            {
                var result = results[i];
                if (result == null || result.mesh == null) continue;

                var voxModel = loadedVoxFile.models[i];
                // Prefer the artist's name from the .vox scene graph when
                // available; fall back to Model_<i> for unnamed transforms so
                // multi-object files still get a stable index-based label.
                string childName = !string.IsNullOrEmpty(voxModel.name)
                    ? voxModel.name
                    : (results.Length > 1 ? $"Model_{i}" : root.name);
                GameObject modelGo = new GameObject(childName);
                modelGo.transform.SetParent(root.transform);
                modelGo.transform.localPosition = (Vector3)voxModel.position * currentScale;
                modelGo.transform.localRotation = voxModel.rotation;
                var meshFilter = modelGo.AddComponent<MeshFilter>();
                var meshRenderer = modelGo.AddComponent<MeshRenderer>();

                meshFilter.sharedMesh = result.mesh;
                meshRenderer.sharedMaterial = result.material;

                string baseName = results.Length > 1 ? $"{root.name}_{i}" : root.name;
                result.mesh.name = $"{baseName}_mesh";
                result.material.name = $"{baseName}_mat";
                result.texture.name = $"{baseName}_tex";

                ctx.AddObjectToAsset(result.mesh.name, result.mesh);
                ctx.AddObjectToAsset(result.texture.name, result.texture);
                ctx.AddObjectToAsset(result.material.name, result.material);
            }

            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);

            // Cleanup
            Object.DestroyImmediate(tempGo);

            Debug.Log($"VoxImporter [{selectedRenderType}]: Successfully imported {ctx.assetPath} with {results.Length} models.");
        }

        private void ApplyImporterSettings(VoxRenderAbstract renderer)
        {
            // No longer needed as we pass settings directly
        }
    }
}


