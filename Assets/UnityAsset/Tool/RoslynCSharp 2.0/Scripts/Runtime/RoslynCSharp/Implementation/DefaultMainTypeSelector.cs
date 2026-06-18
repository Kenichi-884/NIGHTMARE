using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RoslynCSharp.Implementation
{
    internal sealed class DefaultMainTypeSelector : IMainTypeSelector
    {
        // Methods
        public ScriptType SelectMainType(ScriptAssembly definingAssembly, IReadOnlyList<ScriptType> allTypes)
        {
            // Order by public and ignore the module type
            IEnumerable<ScriptType> orderedTypes = allTypes
                .OrderByDescending(t => t.SystemType.IsPublic);

            ScriptType mainType = null;

            try
            {
                // Check for mono behaviour first, preferably public
                mainType = orderedTypes
                    .FirstOrDefault(t => t.IsMonoBehaviour);
            }
            // UnityEngine not available??
            catch (FileNotFoundException) { }

            // Check for class, preferably public
            if (mainType == null)
            {
                mainType = orderedTypes
                    .FirstOrDefault(t => t.SystemType.IsClass == true);
            }

            // Get any public type
            if (mainType == null)
            {
                mainType = orderedTypes
                    .FirstOrDefault(t => t.SystemType.IsPublic == true);
            }

            // Get any type
            if (mainType == null)
            {
                mainType = orderedTypes
                    .FirstOrDefault();
            }

            return mainType;
        }
    }
}
