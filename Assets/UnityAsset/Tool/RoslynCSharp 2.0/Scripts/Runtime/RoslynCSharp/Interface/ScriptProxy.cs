using System;
using System.Reflection;
using UnityEngine;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents an instance of a type loaded and instantiated in a script domain.
    /// </summary>
    public abstract class ScriptProxy : IDisposable
    {
        // Private
        private readonly Lazy<ScriptFields> scriptFields;
        private readonly Lazy<ScriptProperties> scriptProperties;
        private readonly Lazy<ScriptMethods> scriptMethods;
        private bool isDisposed = false;

        // Properties
        /// <summary>
        /// Get the <see cref="RoslynCSharp.ScriptType"/> of the wrapped instance.
        /// </summary>
        public abstract ScriptType ScriptType { get; }
        /// <summary>
        /// Get the raw wrapped instance.
        /// </summary>
        public abstract object Instance { get; }

        /// <summary>
        /// Returns true if the wrapped instance is derived from <see cref="UnityEngine.Object"/>.
        /// </summary>
        public virtual bool IsUnityObject
        {
            get
            {
                CheckDisposed();
                return Instance is UnityEngine.Object;
            }
        }

        /// <summary>
        /// Returns true if the wrapped instance is derived from <see cref="UnityEngine.Component"/>.
        /// </summary>
        public virtual bool IsComponent
        {
            get
            {
                CheckDisposed();
                return Instance is UnityEngine.Component;
            }
        }

        /// <summary>
        /// Returns true if the wrapped instance is derived from <see cref="MonoBehaviour"/>.
        /// </summary>
        public virtual bool IsMonoBehaviour
        {
            get
            {
                CheckDisposed();
                return Instance is MonoBehaviour;
            }
        }

        /// <summary>
        /// Returns true if the wrapped instance is derived from <see cref="ScriptableObject"/>.
        /// </summary>
        public virtual bool IsScriptableObject
        {
            get
            {
                CheckDisposed();
                return Instance is ScriptableObject;
            }
        }

        /// <summary>
        /// Returns the wrapped instance as <see cref="UnityEngine.Object"/> if it derived from, or null if not.
        /// </summary>
        public virtual UnityEngine.Object UnityInstance => GetInstanceAs<UnityEngine.Object>(false);
        /// <summary>
        /// Returns the wrapped instance as <see cref="UnityEngine.Component"/> if it is derived from, or null if not.
        /// </summary>
        public virtual UnityEngine.Component ComponentInstance => GetInstanceAs<UnityEngine.Component>(false);
        /// <summary>
        /// Returns the wrapped instance as <see cref="MonoBehaviour"/> if it derived from, or null if not.
        /// </summary>
        public virtual MonoBehaviour MonoBehaviourInstance => GetInstanceAs<MonoBehaviour>(false);
        /// <summary>
        /// Returns the wrapped instance as <see cref="ScriptableObject"/> if it derived from, or null if not.
        /// </summary>
        public virtual ScriptableObject ScriptableObjectInstance => GetInstanceAs<ScriptableObject>(false);
        /// <summary>
        /// Quick access to the fields defined on this instance.
        /// Note that static fields should be accessed via the <see cref="ScriptType.StaticFields"/>.
        /// </summary>
        public ScriptFields Fields => scriptFields.Value;
        /// <summary>
        /// Quick access to the properties defined on this instance.
        /// Note that static properties should be accessed via the <see cref="ScriptType.StaticProperties"/>.
        /// </summary>
        public ScriptProperties Properties => scriptProperties.Value;
        /// <summary>
        /// Quick access to the methods defined on this instance.
        /// Note that static methods should be accessed via the <see cref="ScriptType.StaticMethods"/>.
        /// </summary>
        public ScriptMethods Methods => scriptMethods.Value;
        /// <summary>
        /// Check if this instance has been disposed.
        /// </summary>
        public bool IsDisposed => isDisposed;

        // Constructor
        /// <summary>
        /// Create a new instance.
        /// </summary>
        protected ScriptProxy() 
        {
            // Must be lazy because instance and type are not available until parent constructor has run in best case
            this.scriptFields = new(() => new ScriptFields(Instance, ScriptType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
            this.scriptProperties = new(() => new ScriptProperties(Instance, ScriptType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
            this.scriptMethods = new(() => new ScriptMethods(Instance, ScriptType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
        }

        // Methods
        /// <summary>
        /// Dispose of this object and the managed instance.
        /// This will cause <see cref="UnityEngine.Object"/> instances to be destroyed, and for `Dispose` method to be called if the type implements <see cref="IDisposable"/>
        /// </summary>
        protected virtual void OnDispose()
        {
            // Check for game object
            if (Instance is GameObject go)
            {
                if (Application.isPlaying == false)
                {
                    // Destroy the game object
                    GameObject.Destroy(go);
                }
                else
                {
                    // Destroy in editor
                    GameObject.DestroyImmediate(go);
                }
            }
            // Check for component
            else if(Instance is Component component)
            {
                if (Application.isPlaying == false)
                {
                    // Destroy the component
                    Component.Destroy(component);
                }
                else
                {
                    // Destroy in editor
                    Component.DestroyImmediate(component);
                }
            }

            // Check for IDisposable
            if (Instance is IDisposable disposable)
                disposable.Dispose();
        }

        /// <summary>
        /// Get the type of the instance that is managed by this object.
        /// </summary>
        /// <returns>The type of this wrapped instance</returns>
        /// <exception cref="InvalidOperationException">Instance is null or has been destroyed</exception>
        public virtual Type GetInstanceType()
        {
            CheckDisposed();

            // Check for null
            if (Instance == null)
                throw new InvalidOperationException("Instance is null");

            // Get instance type
            return Instance.GetType();
        }

        /// <summary>
        /// Checks whether this is an instance of the specified generic type.
        /// </summary>
        /// <typeparam name="T">The generic type to check for an instance of</typeparam>
        /// <returns>True if the managed instance is assignable from the specified generic type, or false if not</returns>
        public virtual bool IsInstanceOf<T>()
        {
            // Check for disposed
            CheckDisposed();

            // Check for instance
            return Instance is T;
        }

        /// <summary>
        /// Attempts to get the managed instance as the specified generic type.
        /// </summary>
        /// <typeparam name="T">The generic type to return the instance as</typeparam>
        /// <param name="throwOnError">When false, any exceptions caused by the conversion will be caught and will result in a default value being returned. When true, any exceptions will not be handled.</param>
        /// <param name="errorValue">The value to return when 'throwOnError' is false and an error occurs"/></param>
        /// <returns>The managed instance as the specified generic type or the default value for the generic type if an error occurred</returns>
        public virtual T GetInstanceAs<T>(bool throwOnError, T errorValue = default(T))
        {
            // Check for disposed
            CheckDisposed();

            // Try a direct cast
            if (throwOnError == true)
                return (T)Instance;

            try
            {
                // Try to cast and catch any InvalidCast exceptions.
                T result = (T)Instance;

                // Return the result
                return result;
            }
            catch
            {
                // Error value
                return errorValue;
            }
        }

        /// <summary>
        /// If the wrapped instance is a Unity type then this method will call 'Don'tDestroyOnLoad' to ensure that the object is able to survive scene loads.
        /// </summary>
        public virtual void MakePersistent()
        {
            // Make the instance survive scene loads
            if (IsUnityObject == true)
                UnityEngine.Object.DontDestroyOnLoad(UnityInstance);
        }

        /// <summary>
        /// Dispose of this object and the managed instance.
        /// This will cause <see cref="UnityEngine.Object"/> instances to be destroyed, and for `Dispose` method to be called if the type implements <see cref="IDisposable"/>
        /// </summary>
        public void Dispose()
        {
            if(isDisposed == false)
            {
                isDisposed = true;
                OnDispose();
            }
        }

        /// <summary>
        /// Check if this object is disposed and throw an exception if that is the case.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This object has already been disposed</exception>
        protected void CheckDisposed()
        {
            if (isDisposed == true)
                throw new ObjectDisposedException("Script proxy");
        }
    }
}
