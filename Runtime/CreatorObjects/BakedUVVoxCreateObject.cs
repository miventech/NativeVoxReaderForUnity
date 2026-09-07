using UnityEngine;
using System.Collections.Generic;
using Miventech.NativeVoxReader.Data;
using Miventech.NativeVoxReader.Abstract;
using Miventech.NativeVoxReader.VoxRenderer;
using Miventech.NativeVoxReader.Tools.VoxFileBakeTexture;

namespace Miventech.NativeVoxReader.CreatorObjects
{

    /// <summary>
    /// Implementation that bakes textures into an Atlas.
    /// Uses PackTextures to create a unique texture atlas for the model.
    /// Optimizes mesh topology by merging coplanar faces regardless of color, 
    /// and bakes the color variations into the texture.
    /// </summary>
    public class BakedUVVoxCreateObject : VoxCreateObjectAbstract
    {
        public int maxAtlasSize = 4096;
        [Tooltip("Max width/height in voxels for a single generated quad.")]
        public int maxQuadSize = 64;
        public float scale = 0.1f;
        public override void BuildObject(VoxModel model, Color32[] palette)
        {
            GameObject ChildObject = new GameObject("VoxModel");
            ChildObject.transform.SetParent(this.transform);
            ChildObject.transform.localPosition = (Vector3)model.position * scale;
            // Apply the model rotation so runtime creation matches the editor import pipeline.
            ChildObject.transform.localRotation = model.rotation;
            ChildObject.transform.localScale = Vector3.one;
            MeshFilter meshFilter = ChildObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = ChildObject.AddComponent<MeshRenderer>();
            var bakedModel = VoxFileToUnityBakeTexture.ConvertModel(model, palette, new VoxFileToUnityBakeTextureSetting()
            {
                maxAtlasSize = maxAtlasSize,
                maxQuadSize = maxQuadSize,
                Scale = scale
            });

            // ConvertModel can return null when the model produces no quads (empty mesh).
            if (bakedModel != null && bakedModel.mesh != null)
            {
                meshFilter.mesh = bakedModel.mesh;
                meshRenderer.material = bakedModel.material;
            }
            else
            {
                if (Application.isEditor) Object.DestroyImmediate(ChildObject);
                else Object.Destroy(ChildObject);
            }
        }

    }
}

