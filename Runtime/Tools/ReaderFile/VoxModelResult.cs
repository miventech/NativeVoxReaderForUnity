using UnityEngine;

namespace Miventech.NativeUnityVoxReader.Tools
{
    public class VoxModelResult
    {
        public Mesh mesh;
        public Texture2D texture;
        public Material material;
        public VoxModelResult(Mesh mesh, Texture2D texture, Material material)
        {
            this.mesh = mesh;
            this.texture = texture;
            this.material = material;
        }
    }
}

