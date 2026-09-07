using UnityEngine;
using Miventech.NativeVoxReader.Data;
using Miventech.NativeVoxReader.Abstract;
using Miventech.NativeVoxReader.Tools.VoxFilePaletteStyle;

namespace Miventech.NativeVoxReader.CreatorObjects
{
    /// <summary>
    /// Implementation of VoxCreateObjectAbstract using the Palette Style (Basic Vox).
    /// Uses a 256x1 texture as a color palette and maps mesh greedily to it.
    /// </summary>
    public class PaletteVoxCreateObject : VoxCreateObjectAbstract
    {
        public float scale = 0.1f;

        public override void BuildObject(VoxModel model, Color32[] palette)
        {
            GameObject ChildObject = new GameObject("VoxModel_Palette");
            ChildObject.transform.SetParent(this.transform);

            // Note: VoxModel position is in Voxel coordinates, adjusted by scale
            ChildObject.transform.localPosition = (Vector3)model.position * scale;
            // Apply the model rotation so runtime creation matches the editor import pipeline.
            ChildObject.transform.localRotation = model.rotation;
            ChildObject.transform.localScale = Vector3.one;

            MeshFilter meshFilter = ChildObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = ChildObject.AddComponent<MeshRenderer>();

            // Use the Utility class to convert the model
            var result = VoxFileToUnityPaletteStyle.ConvertModel(model, palette, new VoxFileToUnityPaletteStyleSetting()
            {
                Scale = scale
            });

            if (result != null)
            {
                meshFilter.mesh = result.mesh;
                meshRenderer.material = result.material;
            }
        }
    }
}
