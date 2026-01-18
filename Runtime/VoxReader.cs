using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using Miventech.NativeUnityVoxReader;
using Miventech.NativeUnityVoxReader.Data;
using Miventech.NativeUnityVoxReader.Abstract;
using Miventech.NativeUnityVoxReader.Tools;
namespace Miventech.NativeUnityVoxReader
{
    public class VoxReader : MonoBehaviour
    {
        public string FilePathVox;

        // Cache for editor/runtime use
        public VoxFile loadedVoxFile;

        public VoxCreateObjectAbstract meshBuilder;


        [ContextMenu("Read Vox File")]
        public void ReadVoxFile()
        {
            if (string.IsNullOrEmpty(FilePathVox) || !File.Exists(FilePathVox))
            {
                Debug.LogError("File path is invalid or file does not exist.");
                return;
            }

            if (meshBuilder == null)
            {
                Debug.LogError("Mesh Builder is not assigned.");
                return;
            }

            loadedVoxFile = ReaderVoxFile.Read(FilePathVox);

            RemoveInternalObject();
            
            if (loadedVoxFile == null) return;

            foreach (VoxModel voxModel in loadedVoxFile.models)
            {
                meshBuilder.BuildObject(voxModel, loadedVoxFile.palette);
            }
        }
        


         private void RemoveInternalObject()
        {
            foreach (Transform child in transform)
            {
                if (Application.isEditor)
                    DestroyImmediate(child.gameObject);
                else
                    Destroy(child.gameObject);
            }    
        }
        
    }
}

