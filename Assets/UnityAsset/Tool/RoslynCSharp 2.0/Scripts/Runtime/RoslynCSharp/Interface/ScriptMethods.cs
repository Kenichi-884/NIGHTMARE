using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a collection of methods defined on a type or instance.
    /// Used for calling methods or coroutines with caching.
    /// </summary>
    public sealed class ScriptMethods
    {
        // Private
        private readonly object instance = null;
        private readonly ScriptType scriptType = null;
        private readonly BindingFlags bindingFlags = 0;

        private Dictionary<string, MethodInfo> cachedMethods = null;

        // Constructor
        internal ScriptMethods(object instance, ScriptType scriptType, BindingFlags bindFlags) 
        { 
            this.instance = instance;
            this.scriptType = scriptType;
            this.bindingFlags = bindFlags;
        }

        // Methods
        /// <summary>
        /// Call a method with the specified name as a coroutine.
        /// Note that the method must be an instance method defined on a MonoBehaviour derived type.
        /// Not supported for static methods since coroutines implicitly need an associated behaviour component to run and manage the coroutine.
        /// </summary>
        /// <param name="name">The name of the method to call</param>
        /// <param name="args">The optional arguments for the method</param>
        /// <returns>A coroutine that has been started</returns>
        /// <exception cref="InvalidOperationException">Method is not a coroutine (Does not return IEnumerator) or is defined as static or on a type not deriving from MonoBehaviour</exception>
        public Coroutine CallCoroutine(string name, params object[] args)
        {
            try
            {
                // Call the method
                object result = FindMethod(name, args)?
                    .Invoke(instance, args);

                // Check for IEnumerator
                if ((result is IEnumerator routine) == false)
                    throw new InvalidOperationException(string.Format("Method '{0}' is not a coroutine", name));

                // Check for behaviour
                if ((instance is MonoBehaviour behaviour) == false)
                    throw new InvalidOperationException(string.Format("Method '{0}' is a coroutine, but it is not defined on a MonoBehaviour type", name));

                // Start a coroutine
                return behaviour.StartCoroutine(routine);
            }
            catch (TargetInvocationException e)
            {
                // Capture user exception
                ExceptionDispatchInfo userExceptionInfo = ExceptionDispatchInfo.Capture(e);

                // Throw user exception with user call stack - not interested in the call stack from this location
                userExceptionInfo.Throw();
                return default;
            }
        }

        /// <summary>
        /// Call a method with the specified name and arguments.
        /// </summary>
        /// <param name="name">The name of the method to call</param>
        /// <param name="args">The optional arguments for the method</param>
        /// <returns>The method return value or null if the method does not return anything</returns>
        public object Call(string name, params object[] args)
        {
            try
            {
                // Call the method
                return FindMethod(name, args)?
                    .Invoke(instance, args);
            }
            catch (TargetInvocationException e)
            {
                // Capture user exception
                ExceptionDispatchInfo userExceptionInfo = ExceptionDispatchInfo.Capture(e);

                // Throw user exception with user call stack - not interested in the call stack from this location
                userExceptionInfo.Throw();
                return default;
            }
        }

        /// <summary>
        /// Call a method with the specified name and arguments, and returns the value as the specified generic type.
        /// </summary>
        /// <param name="name">The name of the method to call</param>
        /// <param name="args">The optional arguments for the method</param>
        /// <typeparam name="T">The generic type to get the return value as</typeparam>
        /// <returns>The method return value or null if the method does not return anything</returns>
        /// <exception cref="TargetException">Could not convert the return value of the called method to the specified generic type</exception>
        public T Call<T>(string name, params object[] args)
        {
            try
            {
                return (T)FindMethod(name, args)?
                    .Invoke(instance, args);
            }
            catch(InvalidCastException)
            {
                throw new TargetException(string.Format("Could not get method '{0}' return value as type '{1}'", name, typeof(T)));
            }
            catch(TargetInvocationException e)
            {
                // Capture user exception
                ExceptionDispatchInfo userExceptionInfo = ExceptionDispatchInfo.Capture(e);

                // Throw user exception with user call stack - not interested in the call stack from this location
                userExceptionInfo.Throw();
                return default;
            }
        }

        private MethodInfo FindMethod(string name, object[] args)
        {
            // Check for null or disposed
            if (instance == null && (bindingFlags & BindingFlags.Instance) != 0)
                throw new InvalidOperationException("Associated instance is null or has been disposed");

            // Check for cache
            MethodInfo method = default;
            if(cachedMethods != null)
            {
                // Try to find cached
                if (cachedMethods.TryGetValue(name, out method) == true)
                    return method;
            }

            // Get arg types
            Type[] argTypes = args != null && args.Length > 0
                ? args.Select(a => a.GetType()).ToArray()
                : Type.EmptyTypes;

            // Search for the method
            method = scriptType.SystemType.GetMethod(name, bindingFlags, Type.DefaultBinder, argTypes, null);

            // Check for null
            if (method == null)
                throw new TargetException(string.Format("Method '{0}' could not be found on type '{1}'", name, scriptType));

            // Cache method
            if (cachedMethods == null)
                cachedMethods = new();

            cachedMethods[name] = method;
            return method;
        }
    }
}
