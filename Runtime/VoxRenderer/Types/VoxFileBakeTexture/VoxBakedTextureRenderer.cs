using UnityEngine;
using Miventech.NativeVoxReader.Data;
using Miventech.NativeVoxReader.VoxRenderer;
using Miventech.NativeVoxReader.Tools.VoxFileBakeTexture;

namespace Miventech.NativeVoxReader.VoxRenderer.Types.VoxFileBakeTexture
{
    [AddComponentMenu("Miventech/Native Vox Reader/Renderers/Baked Texture Renderer")]
    public class VoxBakedTextureRenderer : VoxRenderAbstract
    {
        public override string RenderMethodName => "Baked Texture";
        public override System.Type SettingsType => typeof(VoxFileToUnityBakeTextureSetting);

        [Header("Settings")]
        public VoxFileToUnityBakeTextureSetting settings = new VoxFileToUnityBakeTextureSetting();

        public override void SetSettings(VoxRenderSettings settings)
        {
            if (settings is VoxFileToUnityBakeTextureSetting bakedSettings)
            {
                this.settings = bakedSettings;
            }
        }

        public override VoxModelResult[] Render(VoxFile file, Color32[] palette)
        {
            return VoxFileToUnityBakeTexture.Convert(file, palette, settings);
        }

        public override VoxModelResult RenderModel(VoxModel model, Color32[] palette)
        {
            return VoxFileToUnityBakeTexture.ConvertModel(model, palette, settings);
        }
    }
}
