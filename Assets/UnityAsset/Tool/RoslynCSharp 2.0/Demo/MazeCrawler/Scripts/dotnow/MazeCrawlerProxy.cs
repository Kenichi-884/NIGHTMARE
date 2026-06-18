#if DOTNOW //&& ENABLE_IL2CPP
using RoslynCSharp.Demo;
using System;
using System.Reflection;
using UnityEngine;

namespace dotnow.Interop
{
    /// <summary>
    /// Required by dotnow to allow inheritance of MazeCrawler from compiled code.
    /// </summary>
    [CLRProxyBinding(typeof(MazeCrawler))]
    public class MazeCrawlerProxy : MazeCrawler, ICLRProxy
    {
        // Private
        private ICLRInstance instance;
        private MethodInfo decideDirectionMethod = null;

        // Properties
        public ICLRInstance Instance => instance;

        // Methods
        public void Initialize(AppDomain domain, Type type, ICLRInstance instance)
        {
            this.instance = instance;
            this.decideDirectionMethod = type.GetMethod(nameof(DecideDirection), BindingFlags.Public | BindingFlags.Instance);
        }

        public override MazeDirection DecideDirection(Vector2Int position, bool canMoveLeft, bool canMoveRight, bool canMoveUp, bool canMoveDown)
        {
            return (MazeDirection)decideDirectionMethod.Invoke(instance, new object[] { position, canMoveLeft, canMoveRight, canMoveUp, canMoveDown });
        }
    }
}
#endif
