using System;
using System.IO;
using System.Reflection;

namespace RoslynCSharp.Implementation
{
    internal sealed class DefaultAssemblyLoader : IAssemblyLoader
    {
        // Private
        private readonly AppDomain appDomain;

        // Constructor
        public DefaultAssemblyLoader(AppDomain domain)
        {
            this.appDomain = domain == null
                ? AppDomain.CurrentDomain
                : domain;
        }

        // Methods
        public Assembly LoadAssembly(string assemblyPath, string debugSymbolsPath = null)
        {
            // Check for IL2CPP
            if (ScriptDomain.IsIL2CPPBackend == true)
            {
                throw new InvalidOperationException("Cannot load assembly using IL2CPP backend! You may need to use dotnow if targeting an Il2CPP platform");
            }

            // Load image from path
            byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
            byte[] symbolBytes = null;

            // Check for symbols
            if(debugSymbolsPath != null)
            {
                // Load symbols from path
                symbolBytes = File.ReadAllBytes(debugSymbolsPath);
            }

            // Call through - It means we can avoid a file lock on the assembly file so it can be overwritten for other requests
            return LoadAssembly(assemblyBytes, symbolBytes);
        }

        public Assembly LoadAssembly(byte[] assemblyImage, byte[] debugSymbolsImage = null)
        {
            // Check for IL2CPP
            if (ScriptDomain.IsIL2CPPBackend == true)
            {
                throw new InvalidOperationException("Cannot load assembly using IL2CPP backend! You may need to use dotnow if targeting an Il2CPP platform");
            }
            
            // Check for symbols
            if (debugSymbolsImage != null && debugSymbolsImage.Length > 0)
                return appDomain.Load(assemblyImage, debugSymbolsImage);

            // Just load image only
            return appDomain.Load(assemblyImage);
        }
    }
}
