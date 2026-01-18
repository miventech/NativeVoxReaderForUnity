using UnityEngine;
using System.Collections.Generic;
using Miventech.NativeUnityVoxReader.Data;
using Miventech.NativeUnityVoxReader.Abstract;
using Miventech.NativeUnityVoxReader.Tools;
using Miventech.NativeUnityVoxReader.Tools.VoxFileBakeTexture;

namespace Miventech.NativeUnityVoxReader.CreatorObjects
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
            ChildObject.transform.localRotation = Quaternion.identity;
            ChildObject.transform.localScale = Vector3.one;
            MeshFilter meshFilter = ChildObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = ChildObject.AddComponent<MeshRenderer>();
            var bakedModel = VoxFileToUnityBakeTexture.ConvertModel(model, palette, new VoxFileToUnityBakeTextureSetting()
            {
                maxAtlasSize = maxAtlasSize,
                maxQuadSize = maxQuadSize,
                Scale = scale
            });

            meshFilter.mesh = bakedModel.mesh;
            meshRenderer.material = bakedModel.material;
        }
        
    }
}

