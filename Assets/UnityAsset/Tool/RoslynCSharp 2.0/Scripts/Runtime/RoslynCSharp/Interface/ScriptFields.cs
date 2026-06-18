using System;
using System.Collections.Generic;
using System.Reflection;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a collection of fields defined on a type or instance.
    /// Used for quickly accessing field values with caching.
    /// </summary>
    public sealed class ScriptFields
    {
        // Private
        private readonly object instance = null;
        private readonly ScriptType scriptType = null;
        private readonly BindingFlags bindingFlags = 0;

        private Dictionary<string, FieldInfo> cachedFields = null;

        // Properties
        /// <summary>
        /// Get oor set the value of a field by name.
        /// </summary>
        /// <param name="name">The name of the field to access</param>
        /// <returns>The value of the field with the specified name</returns>
        public object this[string name]
        {
            get { return Get<object>(name); }
            set { Set(name, value); }
        }

        // Constructor
        internal ScriptFields(object instance, ScriptType scriptType, BindingFlags bindFlags)
        {
            this.instance = instance;
            this.scriptType = scriptType;
            this.bindingFlags = bindFlags;
        }

        // Methods
        /// <summary>
        /// Get the value of a field with the specified name as the provided generic type.
        /// </summary>
        /// <typeparam name="T">The generic type to get the field value as</typeparam>
        /// <param name="name">The name of the field to get</param>
        /// <returns>The field value as the specified generic type</returns>
        /// <exception cref="TargetException">Could not convert field value to specified generic type</exception>
        public T Get<T>(string name)
        {
            try
            {
                // Try to get the field value
                return (T)FindField(name)
                    .GetValue(instance);
            }
            catch(InvalidCastException)
            {
                throw new TargetException(string.Format("Could not get field '{0}' value as type '{1}'", name, typeof(T)));
            }
        }

        /// <summary>
        /// Set the value of a field with the specified name.
        /// </summary>
        /// <typeparam name="T">The generic type to set the field value as</typeparam>
        /// <param name="name">The name of the field to set</param>
        /// <param name="value">The value to assign to the field</param>
        public void Set<T>(string name, T value)
        {
            // Try to set new value
            FindField(name)
                .SetValue(instance, value);
        }

        private FieldInfo FindField(string name)
        {
            // Check for null or disposed
            if (instance == null && (bindingFlags & BindingFlags.Instance) != 0)
                throw new InvalidOperationException("Associated instance is null or has been disposed");

            // Check for cache
            FieldInfo field = default;
            if (cachedFields != null)
            {
                // Try to find cached                
                if (cachedFields.TryGetValue(name, out field) == true)
                    return field;
            }

            // Search for the field
            field = scriptType.SystemType.GetField(name, bindingFlags);

            // Check for null
            if (field == null)
                throw new TargetException(string.Format("Field '{0}' could not be found on type '{1}'", name, scriptType));

            // Cache field
            if (cachedFields == null)
                cachedFields = new();

            cachedFields[name] = field;
            return field;
        }
    }
}
