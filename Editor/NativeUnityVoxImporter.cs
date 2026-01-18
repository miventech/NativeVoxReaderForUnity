using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;
using Miventech.NativeUnityVoxReader.Tools;
using Miventech.NativeUnityVoxReader;
using Miventech.NativeUnityVoxReader.Data;
using Miventech.NativeUnityVoxReader.Tools.VoxFileBakeTexture;


namespace Miventech.NativeUnityVoxReader.Editor
{
    /// <summary>
    /// Custom importer to handle .vox files as native 3D assets in Unity.
    /// This allows dragging .vox files directly into the scene or using them as prefabs.
    /// </summary>
    [ScriptedImporter(1, "vox")]
    public class NativeUnityVoxImporter : ScriptedImporter
    {
        public int maxAtlasSize = 4096;
        public int maxQuadSize = 64;
        public float scale = 0.1f;
        public override void OnImportAsset(AssetImportContext ctx)
        {
            // 1. Read the .vox file data
            VoxFile loadedVoxFile = ReaderVoxFile.Read(ctx.assetPath);
            if (loadedVoxFile == null)
            {
                Debug.LogError($"VoxImporter: Failed to read vox file at {ctx.assetPath}");
                return;
            }

            // 2. Create the root GameObject for the asset
            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));

            // 3. Convert VOX data to Unity Meshes and Materials using the existing baking logic
            VoxFileToUnityBakeTextureSetting settings = new VoxFileToUnityBakeTextureSetting()
            {
                maxAtlasSize = maxAtlasSize,
                maxQuadSize = maxQuadSize,
                Scale = scale
            };

            VoxModelResult[] results = VoxFileToUnityBakeTexture.Convert(loadedVoxFile, loadedVoxFile.palette, settings);

            if (results == null || results.Length == 0)
            {
                Debug.LogWarning($"VoxImporter: No models found in {ctx.assetPath}");
                ctx.AddObjectToAsset("root", root);
                ctx.SetMainObject(root);
                return;
            }

            // 4. Iterate through models and create child objects
            for (int i = 0; i < results.Length; i++)
            {
                var result = results[i];
                if (result == null || result.mesh == null) continue;

                GameObject modelGo = new GameObject(results.Length > 1 ? $"Model_{i}" : root.name);
                modelGo.transform.SetParent(root.transform);
                modelGo.transform.localPosition = (Vector3)loadedVoxFile.models[i].position * settings.Scale;
                var meshFilter = modelGo.AddComponent<MeshFilter>();
                var meshRenderer = modelGo.AddComponent<MeshRenderer>();

                meshFilter.sharedMesh = result.mesh;
                meshRenderer.sharedMaterial = result.material;

                // Set meaningful names for sub-assets
                string baseName = results.Length > 1 ? $"{root.name}_{i}" : root.name;
                result.mesh.name = $"{baseName}_mesh";
                result.material.name = $"{baseName}_mat";
                result.texture.name = $"{baseName}_tex";

                // Register sub-assets so they are saved within the .vox asset file
                ctx.AddObjectToAsset(result.mesh.name, result.mesh);
                ctx.AddObjectToAsset(result.texture.name, result.texture);
                ctx.AddObjectToAsset(result.material.name, result.material);
            }

            // 5. Register the root GameObject as the main asset object
            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);

            Debug.Log($"VoxImporter: Successfully imported {ctx.assetPath} with {results.Length} models.");
        }
    }
}


