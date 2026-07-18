using UnityEngine;

namespace Miventech.NativeVoxReader.Tools.ReaderFile.Data
{
    internal class TransformNode : VoxNode
    {
        public int childId;
        public Vector3Int translation;
        // Local rotation encoded from the _r byte attribute, converted from the
        // MV right-handed Z-up basis into Unity's left-handed Y-up basis at
        // parse time so downstream accumulation is pure Unity math. Defaults
        // to identity when the nTRN has no _r attribute.
        public Quaternion rotation = Quaternion.identity;
        // Optional _name attribute from the outer attributes dict. Null when
        // not present. Preserved so leaf writes can propagate artist naming
        // intent to VoxModel.
        public string name;
    }
}


