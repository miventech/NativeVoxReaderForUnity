using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using Miventech.NativeVoxReader;
using Miventech.NativeVoxReader.Data;
using Miventech.NativeVoxReader.Abstract;
using Miventech.NativeVoxReader.Tools;
using System;
using Miventech.NativeVoxReader.Runtime.Tools.ReaderFile;
namespace Miventech.NativeVoxReader
{
    public class VoxReader : MonoBehaviour
    {
        public bool UseRelativePath = true;
        public string FilePathVox;

        // Cache for editor/runtime use
        public VoxFile loadedVoxFile;

        public VoxCreateObjectAbstract meshBuilder;


        [ContextMenu("Read Vox File")]
        public void ReadVoxFile()
        {
            var PathToRead = FilePathVox;
            if (UseRelativePath)
            {
                PathToRead = Path.Combine(Application.dataPath, FilePathVox);
            }
            
            if (string.IsNullOrEmpty(PathToRead) || !File.Exists(PathToRead))
            {
                Debug.LogError("File path is invalid or file does not exist.");
                return;
            }

            if (meshBuilder == null)
            {
                Debug.LogError("Mesh Builder is not assigned.");
                return;
            }

            string extension = Path.GetExtension(PathToRead).ToLower();
            loadedVoxFile = null;
            foreach (var ReaderClass in BaseReaderFile.GetAllReaders())
            {
                if (!ReaderClass.IsValidFile(PathToRead)) continue;
                loadedVoxFile = ReaderClass.Read(PathToRead);
            }
            if(loadedVoxFile == null)
            {
                Debug.LogError($"Unsupported file extension: {extension}");
                Debug.LogError($"No suitable reader found for file: {PathToRead}");
                return;
            }

            RemoveInternalObject();
            
            if (loadedVoxFile == null) return;

            foreach (VoxModel voxModel in loadedVoxFile.models)
            {
                meshBuilder.BuildObject(voxModel, loadedVoxFile.palette.ToColor32Array());
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

