using System;
using UnityEngine;

namespace Miventech.NativeVoxReader.Data
{
    public static class AdvanceColorExtensions
    {
        /// <summary>
        /// Convierte un array de Color32 a un array de AdvanceColor.
        /// </summary>
        public static AdvanceColor[] ToAdvanceColorArray(this Color32[] colors)
        {
            if (colors == null) return null;
            AdvanceColor[] result = new AdvanceColor[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                result[i] = colors[i]; // Utiliza el operador de conversión implícita que creamos
            }
            return result;
        }

        /// <summary>
        /// Convierte un array de AdvanceColor a un array de Color32.
        /// </summary>
        public static Color32[] ToColor32Array(this AdvanceColor[] advColors)
        {
            if (advColors == null) return null;
            Color32[] result = new Color32[advColors.Length];
            for (int i = 0; i < advColors.Length; i++)
            {
                result[i] = advColors[i]; // Utiliza el operador de conversión implícita
            }
            return result;
        }

        /// <summary>
        /// Convierte un array de Color a un array de AdvanceColor.
        /// </summary>
        public static AdvanceColor[] ToAdvanceColorArray(this Color[] colors)
        {
            if (colors == null) return null;
            AdvanceColor[] result = new AdvanceColor[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                result[i] = colors[i];
            }
            return result;
        }
    }
}
