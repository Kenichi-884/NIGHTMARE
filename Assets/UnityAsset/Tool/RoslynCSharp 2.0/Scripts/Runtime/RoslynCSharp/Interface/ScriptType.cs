using System;
using System.Reflection;
using UnityEngine;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a specific type loaded into a script domain.
    /// </summary>
    public abstract class ScriptType
    {
        // Private
        private readonly ScriptAssembly assembly;
        private readonly ScriptType parent;
        private readonly Lazy<ScriptFields> staticFields;
        private readonly Lazy<ScriptProperties> staticProperties;
        private readonly Lazy<ScriptMethods> staticMethods;

        // Properties
        /// <summary>
        /// Get the <see cref="System.Type"/> represented by this object.
        /// </summary>
        public abstract Type SystemType { get; }

        /// <summary>
        /// Get the script assembly that this type is defined in.
        /// </summary>
        public ScriptAssembly Assembly => assembly;
        /// <summary>
        /// Get the parent type if this is a nested type.
        /// </summary>
        public ScriptType Parent => parent;
        /// <summary>
        /// Get the name of the type without namespace.
        /// </summary>
        public virtual string Name => SystemType.Name;
        /// <summary>
        /// Get the namespace name of the type.
        /// </summary>
        public virtual string Namespace => SystemType.Namespace;
        /// <summary>
        /// Get the full name of the type including the namespace name if applicable.
        /// </summary>
        public virtual string FullName => SystemType.FullName;
        /// <summary>
        /// Return a value indicating whether the type is public.
        /// </summary>
        public virtual bool IsPublic => SystemType.IsPublic;
        /// <summary>
        /// Return a value indicating whether the type is a nested type defined within another type.
        /// </summary>
        public virtual bool IsNested => SystemType.IsNested;

        /// <summary>
        /// Return a value indicating whether this type is derived from <see cref="UnityEngine.Object"/>.
        /// </summary>
        public bool IsUnityObject => IsSubTypeOf<UnityEngine.Object>();
        /// <summary>
        /// Return a value indicating whether this type is derived from <see cref="UnityEngine.Component"/>.
        /// </summary>
        public bool IsComponent => IsSubTypeOf<UnityEngine.Component>();
        /// <summary>
        /// Return a value indicating whether this type is derived from <see cref="UnityEngine.MonoBehaviour"/>.
        /// </summary>
        public bool IsMonoBehaviour => IsSubTypeOf<UnityEngine.MonoBehaviour>();
        /// <summary>
        /// Return a value indicating whether this type is derived from <see cref="UnityEngine.ScriptableObject"/>.
        /// </summary>
        public bool IsScriptableObject => IsSubTypeOf<UnityEngine.ScriptableObject>();

        /// <summary>
        /// Quick access to the static fields defined on this type.
        /// </summary>
        public ScriptFields StaticFields => staticFields.Value;
        /// <summary>
        /// Quick access to the static properties defined on this type.
        /// </summary>
        public ScriptProperties StaticProperties  => staticProperties.Value;
        /// <summary>
        /// Quick access to the static methods defined on this type.
        /// </summary>
        public ScriptMethods StaticMethods => staticMethods.Value;

        // Constructor
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="assembly">The declaring assembly</param>
        /// <param name="parent">The optional parent type if this type is nested</param>
        protected ScriptType(ScriptAssembly assembly, ScriptType parent)
        {
            // Check for null
            if(assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            this.assembly = assembly;
            this.parent = parent;

            this.staticFields = new(() => new ScriptFields(null, this, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
            this.staticProperties = new(() => new ScriptProperties(null, this, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
            this.staticMethods = new(() => new ScriptMethods(null, this, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
        }

        // Methods
        /// <summary>
        /// Create a new instance of a component on the specified game object.
        /// This will create this instance using <see cref="GameObject.AddComponent(Type)"/>.
        /// </summary>
        /// <param name="parent">The game object to attach the component to</param>
        /// <returns></returns>
        protected abstract ScriptProxy CreateInstanceComponentImpl(GameObject parent);
        /// <summary>
        /// Create a new instance of a scriptable object.
        /// This will create the instance using <see cref="ScriptableObject.CreateInstance(Type)"/>.
        /// </summary>
        /// <returns></returns>
        protected abstract ScriptProxy CreateInstanceScriptableObjectImpl();
        /// <summary>
        /// Create a new instance of a type using the constructor appropriate to the provided arguments.
        /// </summary>
        /// <param name="args">The optional arguments of the constructor</param>
        /// <returns></returns>
        protected abstract ScriptProxy CreateInstanceImpl(object[] args);


        /// <summary>
        /// Create a proxy instance of this type.
        /// A proxy instance is an object which wraps the instance and provides a quick and easy way to access members and perform reflection.
        /// Note that this will construct the type using the correct method. For example: MonoBehaviour will use `AddComponent`, ScriptableObject will use `CreateInstance`, and other types will use the best matching or default constructor.
        /// </summary>
        /// <param name="parent">The optional game object parent which is required if the type is a MonoBehaviour</param>
        /// <param name="args">The optional arguments for the target constructor if the type is a non-Unity object</param>
        /// <returns>A <see cref="ScriptProxy"/> object for the newly created instance</returns>
        /// <exception cref="InvalidOperationException">Parent is null but the type is derived from MonoBehaviour</exception>
        public ScriptProxy CreateInstance(GameObject parent = null, params object[] args)
        {
            // Check for component
            if (IsComponent == true)
            {
                // Check for no parent
                if (parent == null)
                    throw new InvalidOperationException("Creating a component instance required a valid parent game object to be provided");

                // Create instance
                return CreateInstanceComponentImpl(parent);
            }
            // Check for scriptable
            else if(IsScriptableObject == true)
            {
                // Create instance
                return CreateInstanceScriptableObjectImpl();
            }
            else
            {
                // Create from args
                return CreateInstanceImpl(args);
            }
        }

        /// <summary>
        /// Create a new instance of this type and return the raw instance.
        /// Note that this will construct the type using the correct method. For example: MonoBehaviour will use `AddComponent`, ScriptableObject will use `CreateInstance`, and other types will use the best matching or default constructor.
        /// </summary>
        /// <param name="parent">The optional game object parent which is required if the type is a MonoBehaviour</param>
        /// <param name="args">The optional arguments for the target constructor if the type is a non-Unity object</param>
        /// <returns>The raw instance</returns>
        /// <exception cref="InvalidOperationException">Parent is null but the type is derived from MonoBehaviour</exception>
        public object CreateInstanceAs(GameObject parent = null, params object[] args)
        {
            ScriptProxy proxy = CreateInstance(parent, args);
            return proxy != null ? proxy.GetInstanceAs<object>(false) : default;
        }

        /// <summary>
        /// Create a new instance of this type and return the object as the specified generic type, base type or interface.
        /// Return value will be null if the type is not convertible to the specified generic type.
        /// Note that this will construct the type using the correct method. For example: MonoBehaviour will use `AddComponent`, ScriptableObject will use `CreateInstance`, and other types will use the best matching or default constructor.
        /// </summary>
        /// <typeparam name="T">The generic type to create the instance as</typeparam>
        /// <param name="parent">The optional game object parent which is required if the type is a MonoBehaviour</param>
        /// <param name="args">The optional arguments for the target constructor if the type is a non-Unity object</param>
        /// <returns>The instance as T or null if the instance could bnot be converted to T</returns>
        /// <exception cref="InvalidOperationException">Parent is null but the type is derived from MonoBehaviour</exception>
        public T CreateInstanceAs<T>(GameObject parent = null, params object[] args)
        {
            ScriptProxy proxy = CreateInstance(parent, args);
            return proxy != null ? proxy.GetInstanceAs<T>(false) : default;
        }

        /// <summary>
        /// Return a value indicating whether this type is derived from the specified generic type.
        /// </summary>
        /// <typeparam name="T">The generic type to check</typeparam>
        /// <returns></returns>
        public virtual bool IsSubTypeOf<T>()
        {
            return IsSubTypeOf(typeof(T));
        }

        /// <summary>
        /// Return a value indicating whether this type is derived from the specified type.
        /// </summary>
        /// <param name="subType">The type to check</param>
        /// <returns></returns>
        public virtual bool IsSubTypeOf(Type subType)
        {
            return subType.IsAssignableFrom(SystemType);
        }
    }
}
