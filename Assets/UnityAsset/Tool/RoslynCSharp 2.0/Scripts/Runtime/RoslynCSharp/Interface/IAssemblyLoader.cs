using System.Reflection;

namespace RoslynCSharp
{
    /// <summary>
    /// Used to load an assembly into memory from a specific location.
    /// </summary>
    public interface IAssemblyLoader
    {
        // Methods
        /// <summary>
        /// Load an assembly from the specified path.
        /// </summary>
        /// <param name="assemblyPath">The assembly path to use</param>
        /// <param name="debugSymbolsPath">The optional path to the pdb symbols path</param>
        /// <returns></returns>
        Assembly LoadAssembly(string assemblyPath, string debugSymbolsPath = null);

        /// <summary>
        /// Load an assembly from the specified PE image bytes.
        /// </summary>
        /// <param name="assemblyImage">The portable executable image</param>
        /// <param name="debugSymbolsImage">The optional debug symbols image</param>
        /// <returns></returns>
        Assembly LoadAssembly(byte[] assemblyImage, byte[] debugSymbolsImage = null);
    }
}
