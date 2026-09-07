using UnityEngine;
using Miventech.NativeVoxReader.Data;
using Miventech.NativeVoxReader.VoxRenderer;
using Miventech.NativeVoxReader.Tools.VoxFilePaletteStyle;

namespace Miventech.NativeVoxReader.VoxRenderer.Types.VoxFilePaletteStyle
{
    [AddComponentMenu("Miventech/Native Vox Reader/Renderers/Palette Style Renderer")]
    public class VoxPaletteStyleRenderer : VoxRenderAbstract
    {
        public override string RenderMethodName => "Palette Style";
        public override System.Type SettingsType => typeof(VoxFileToUnityPaletteStyleSetting);

        [Header("Settings")]
        public VoxFileToUnityPaletteStyleSetting settings = new VoxFileToUnityPaletteStyleSetting();

        public override void SetSettings(VoxRenderSettings settings)
        {
            if (settings is VoxFileToUnityPaletteStyleSetting paletteSettings)
            {
                this.settings = paletteSettings;
            }
        }

        public override VoxModelResult[] Render(VoxFile file, Color32[] palette)
        {
            return VoxFileToUnityPaletteStyle.Convert(file, palette, settings);
        }

        public override VoxModelResult RenderModel(VoxModel model, Color32[] palette)
        {
            return VoxFileToUnityPaletteStyle.ConvertModel(model, palette, settings);
        }
    }
}
