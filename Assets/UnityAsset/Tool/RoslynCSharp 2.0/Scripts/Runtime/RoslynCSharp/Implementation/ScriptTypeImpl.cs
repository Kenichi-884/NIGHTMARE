using System;
using UnityEngine;

namespace RoslynCSharp.Implementation
{
    internal sealed class ScriptTypeImpl : ScriptType
    {
        // Private
        private readonly Type type;

        // Properties
        public override Type SystemType => type;

        // Constructor
        public ScriptTypeImpl(ScriptAssembly assembly, ScriptType parent, Type type)
            : base(assembly, parent)
        {
            // Check for null
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            this.type = type;
        }

        // Methods
        protected override ScriptProxy CreateInstanceComponentImpl(GameObject parent)
        {
            // Add component
            Component component = parent.AddComponent(type);

            // Create proxy
            return new ScriptProxyImpl(this, component);
        }

        protected override ScriptProxy CreateInstanceScriptableObjectImpl()
        {
            // Create instance
            ScriptableObject scriptable = ScriptableObject.CreateInstance(type);

            // Create proxy
            return new ScriptProxyImpl(this, scriptable);
        }

        protected override ScriptProxy CreateInstanceImpl(object[] args)
        {
            // Create instance standard
            object instance = Activator.CreateInstance(type, args);

            // Create proxy
            return new ScriptProxyImpl(this, instance);
        }
    }
}
