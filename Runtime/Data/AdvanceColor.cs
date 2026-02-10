using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Miventech.NativeVoxReader.Data
{
    // Represents a single voxel in local coordinates
    [System.Serializable]
    public struct AdvanceColor
    {
        public Color32 Color;
        public byte r => Color.r;
        public byte g => Color.g;
        public byte b => Color.b;
        public byte a => Color.a;
        public string Name; // Optional name for the color (e.g., "Red", "Metallic Silver", etc.)
        //TODO Unuse for now, but we can use this struct to store more advanced material properties in the future if needed
        public int MaterialType; // Material Type (0=none, 1=diffuse, 2=metal, 3=glass, etc.)
        public byte ColorIndex; //not needed for now, this struct place into Array index is position in palette, but we can use this to store original color index from palette if needed for reference
        // //TODO Unuse for now, but we can use this to store additional material properties in the future if needed
        public Dictionary<string, float> properties; // For advanced material properties (e.g., roughness, metallic, etc.) 
        public AdvanceColor(byte r, byte g, byte b, byte a, int materialType, byte colorIndex, Dictionary<string, float> properties = null)
        {
            Name = "default";
            Color = new Color32((byte)r, (byte)g, (byte)b, (byte)a);
            this.MaterialType = materialType;
            this.ColorIndex = colorIndex;

            if(properties == null){
                properties = new Dictionary<string, float>();
            }
            this.properties = properties;
        }
        
         public AdvanceColor(Color32 color, int materialType, byte colorIndex, Dictionary<string,float> properties = null)
        {
            Name = "default";
            Color = color;
            this.MaterialType = materialType;
            this.ColorIndex = colorIndex;

            if(properties == null){
                properties = new Dictionary<string, float>();
            }
            this.properties = properties;
        }

        // Implicit conversions to make the struct operational with Unity Color types
        public static implicit operator AdvanceColor(Color32 color)
        {
            return new AdvanceColor(color.r, color.g, color.b, color.a, 0, 0, null);
        }

        public static implicit operator Color32(AdvanceColor adv)
        {
            return new Color32(adv.r, adv.g, adv.b, adv.a);
        }

        public static implicit operator AdvanceColor(Color color) => (Color32)color;
        public static implicit operator Color(AdvanceColor adv) => (Color32)adv;
    }
}


