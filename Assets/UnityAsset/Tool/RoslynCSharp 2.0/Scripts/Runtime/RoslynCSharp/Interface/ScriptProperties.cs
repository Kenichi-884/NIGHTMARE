using System;
using System.Collections.Generic;
using System.Reflection;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a collection of properties defined on a type or instance.
    /// Used for quickly accessing property values with caching.
    /// </summary>
    public sealed class ScriptProperties
    {
        // Private
        private readonly object instance = null;
        private readonly ScriptType scriptType = null;
        private readonly BindingFlags bindingFlags = 0;

        private Dictionary<string, PropertyInfo> cachedProperties = null;

        // Properties
        /// <summary>
        /// Get or set the value of a property by name.
        /// </summary>
        /// <param name="name">The name of the property to access</param>
        /// <returns>The value of the property with the specified name</returns>
        public object this[string name]
        {
            get { return Get<object>(name); }
            set { Set(name, value); }
        }

        // Constructor
        internal ScriptProperties(object instance, ScriptType scriptType, BindingFlags bindFlags)
        {
            this.instance = instance;
            this.scriptType = scriptType;
            this.bindingFlags = bindFlags;
        }

        // Methods
        /// <summary>
        /// Get the value of a property with the specified name as the provided generic type.
        /// </summary>
        /// <typeparam name="T">The generic type to get the property value as</typeparam>
        /// <param name="name">The name of the property to get</param>
        /// <returns>The property value as the specified generic type</returns>
        /// <exception cref="TargetException">Could not convert property value to specified generic type</exception>
        public T Get<T>(string name)
        {
            try
            {
                // Try to get the property value
                return (T)FindProperty(name)
                    .GetValue(instance);
            }
            catch (InvalidCastException)
            {
                throw new TargetException(string.Format("Could not get property '{0}' value as type '{1}'", name, typeof(T)));
            }
        }

        /// <summary>
        /// Set the value of a property with the specified name.
        /// </summary>
        /// <typeparam name="T">The generic type to set the property value as</typeparam>
        /// <param name="name">The name of the property to set</param>
        /// <param name="value">The value to assign to the property</param>
        public void Set<T>(string name, T value)
        {
            // Try to set new value
            FindProperty(name)
                .SetValue(instance, value);
        }

        private PropertyInfo FindProperty(string name)
        {
            // Check for null or disposed
            if (instance == null && (bindingFlags & BindingFlags.Instance) != 0)
                throw new InvalidOperationException("Associated instance is null or has been disposed");

            // Check for cache
            PropertyInfo property = default;
            if (cachedProperties != null)
            {
                // Try to find cached                
                if (cachedProperties.TryGetValue(name, out property) == true)
                    return property;
            }

            // Search for the property
            property = scriptType.SystemType.GetProperty(name, bindingFlags);

            // Check for null
            if (property == null)
                throw new TargetException(string.Format("Property '{0}' could not be found on type '{1}'", name, scriptType));

            // Cache field
            if (cachedProperties == null)
                cachedProperties = new();

            cachedProperties[name] = property;
            return property;
        }
    }
}
