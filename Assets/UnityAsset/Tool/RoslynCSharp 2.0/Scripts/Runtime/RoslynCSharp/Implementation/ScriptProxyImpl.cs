using System;

namespace RoslynCSharp.Implementation
{
    internal sealed class ScriptProxyImpl : ScriptProxy
    {
        // Private
        private readonly ScriptType scriptType;
        private readonly object instance;

        // Properties
        public override ScriptType ScriptType => scriptType;
        public override object Instance => instance;

        // Constructor
        internal ScriptProxyImpl(ScriptType scriptType, object instance)
        {
            // Check for null
            if(scriptType == null)
                throw new ArgumentNullException(nameof(scriptType));

            if(instance == null)
                throw new ArgumentNullException(nameof(instance));

            this.scriptType = scriptType;
            this.instance = instance;
        }
    }
}
