
using System;
using System.Linq;
using Miventech.NativeVoxReader.Data;
using Miventech.NativeVoxReader.Readers.Data;
using UnityEngine;


namespace Miventech.NativeVoxReader.Readers
{
    public abstract class BaseReaderFile
    {
        /// <summary>
        /// When true, readers emit verbose trace logs (per chunk, per palette color,
        /// per property). Disabled by default so the end user's console stays clean.
        /// </summary>
        public static bool VerboseLogging = false;
        /// <summary>
        /// Checks if the file at the given path is valid for this reader.
        /// </summary>
        public abstract bool IsValidFile(string path);
        /// <summary>
        /// Reads the file at the given path and returns a VoxFile object.
        /// </summary> 
        public abstract VoxFile Read(string path);
        private static BaseReaderFile[] CacheReaders;
        public static BaseReaderFile[] GetAllReaders()
        {
            if (CacheReaders != null) return CacheReaders;
            var baseType = typeof(BaseReaderFile);

            CacheReaders = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (BaseReaderFile)Activator.CreateInstance(t))
                .ToArray();

            return CacheReaders;
        }
    }
}