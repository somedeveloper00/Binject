#if UNITY_EDITOR || DEVELOPMENT_BUILD || DEBUG
    #define B_DEBUG
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Binject {
    [ExecuteAlways] 
    public static class BinjectManager {
        [NonSerialized] static readonly List<BContext> _contexts = new( 16 );
        [NonSerialized] static readonly Dictionary<Scene, BContext> _sceneRootContexts = new( 4 );
        [NonSerialized] static readonly Dictionary<ushort, List<BContext>> _groupedContexts = new( 8 );
        [NonSerialized] static BContext _rootContext;

#if B_DEBUG
        const bool DEBUG_LOG = true; 
#endif

        /// <summary>
        /// Finds the context holding the required dependencies of type <see cref="T"/> compatible with the given
        /// <see cref="Transform"/>. returns null if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining)]
        public static BContext FindContext<T>(Transform transform, ushort groupNumber = 0) {
            
            [MethodImpl( MethodImplOptions.AggressiveInlining)]
            bool sameTransform(BContext context) => context.transform == transform;
            
            [MethodImpl( MethodImplOptions.AggressiveInlining)]
            bool isCorrectGroup(BContext context) => groupNumber == 0 || groupNumber == context.Group;

            BContext firstContext = null;
            
            if (_contexts.Count != 0) {
                // check parents
                while (true) {
                    for (int i = 0; i < _contexts.Count; i++) {
                        var context = _contexts[i];
                        if (isCorrectGroup( context ) && sameTransform( context )) {
                            firstContext ??= context;
                            if (context.HasDependency<T>()) return context;
                        }
                    }
                    if (transform.parent) transform = transform.parent;
                    else break;
                } 
                
                // check scene root context
                if (_sceneRootContexts.Count > 0 && 
                    _sceneRootContexts.TryGetValue( transform.gameObject.scene, out var sceneRootContext )
                    && sceneRootContext && transform != sceneRootContext.transform) 
                {
                    if (isCorrectGroup( sceneRootContext ) && sceneRootContext.HasDependency<T>()) 
                        return sceneRootContext;
                }
                
                // check root context
                if (_rootContext && transform != _rootContext.transform) {
                    if (isCorrectGroup( _rootContext ) && _rootContext.HasDependency<T>()) return _rootContext;
                }
                
                // check grouped contexts from inappropriate contexts
                if (_groupedContexts.TryGetValue( groupNumber, out var list ) && list.Count > 0) {
                    for (int i = 0; i < list.Count; i++)
                        if (list[i].HasDependency<T>()) return list[i];
                }
                
                // no context found, return first parent in group if exists any
                if (firstContext is not null) return firstContext;
            }

            Debug.LogWarning( $"No context found containing the dependency type {typeof(T).FullName}" );

            return null;
        }

#region Helper Functions

        /// <summary>
        /// Returns the dependency of type <see cref="T"/> from a compatible context. returns default if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining)]
        public static T GetDependency<T>(Transform transform, ushort groupNumber = 0) where T : class {
            var context = FindContext<T>( transform, groupNumber );
            if (context == null) return default;
            return context.GetDependencyNoCheck<T>();
        }

        /// <summary>
        /// Checks if the dependency of type <see cref="T"/> exists in a compatible context.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool DependencyExists<T>(Transform transform, ushort groupNumber = 0) where T : class {
            var context = FindContext<T>( transform, groupNumber );
            return context != null;
        }
        
        /// <summary>
        /// Returns dependencies of the given type from a compatible context. for each dependency, returns default
        /// if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2) GetDependencies<T1, T2>(Transform transform, ushort groupNumber = 0)
            where T1 : class where T2 : class 
        {
            return (GetDependency<T1>( transform, groupNumber ), GetDependency<T2>( transform, groupNumber ));
        }

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3) GetDependencies<T1, T2, T3>(Transform transform, ushort groupNumber = 0)
            where T1 : class where T2 : class where T3 : class 
        {
            return (GetDependency<T1>( transform, groupNumber ), GetDependency<T2>( transform, groupNumber ), GetDependency<T3>( transform, groupNumber ));
        }

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3, T4) GetDependencies<T1, T2, T3, T4>(Transform transform, ushort groupNumber = 0)
            where T1 : class where T2 : class where T3 : class where T4 : class 
        {
            return (GetDependency<T1>( transform, groupNumber ), GetDependency<T2>( transform, groupNumber ), GetDependency<T3>( transform, groupNumber ), GetDependency<T4>( transform, groupNumber ));
        }

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3, T4, T5) GetDependencies<T1, T2, T3, T4, T5>(Transform transform, ushort groupNumber = 0)
            where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class 
        {
            return (GetDependency<T1>( transform, groupNumber ), GetDependency<T2>( transform, groupNumber ), GetDependency<T3>( transform, groupNumber ), GetDependency<T4>( transform, groupNumber ), GetDependency<T5>( transform, groupNumber ));
        }

        
        /// <summary>
        /// Returns the dependency of type <see cref="T"/> from a compatible context. returns default if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining)]
        public static T GetDependencyStruct<T>(Transform transform, ushort groupNumber = 0) where T : struct {
            var context = FindContext<T>( transform, groupNumber );
            if (context == null) return default;
            return context.GetDependencyStructNoCheck<T>();
        }

        /// <summary>
        /// Checks if the dependency of type <see cref="T"/> exists in a compatible context.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool DependencyStructExists<T>(Transform transform, ushort groupNumber = 0) where T : struct {
            var context = FindContext<T>( transform, groupNumber );
            return context != null;
        }
        
        /// <summary>
        /// Returns dependencies of the given type from a compatible context. for each dependency, returns default
        /// if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2) GetDependenciesStruct<T1, T2>(Transform transform, ushort groupNumber = 0)
            where T1 : struct where T2 : struct 
        {
            return (GetDependencyStruct<T1>( transform, groupNumber ), GetDependencyStruct<T2>( transform, groupNumber ));
        }

        /// <inheritdoc cref="GetDependencyStruct{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3) GetDependenciesStruct<T1, T2, T3>(Transform transform, ushort groupNumber = 0)
            where T1 : struct where T2 : struct where T3 : struct 
        {
            return (GetDependencyStruct<T1>( transform, groupNumber ), GetDependencyStruct<T2>( transform, groupNumber ), GetDependencyStruct<T3>( transform, groupNumber ));
        }

        /// <inheritdoc cref="GetDependencyStruct{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3, T4) GetDependenciesStruct<T1, T2, T3, T4>(Transform transform, ushort groupNumber = 0)
            where T1 : struct where T2 : struct where T3 : struct where T4 : struct 
        {
            return (GetDependencyStruct<T1>( transform, groupNumber ), GetDependencyStruct<T2>( transform, groupNumber ), GetDependencyStruct<T3>( transform, groupNumber ), GetDependencyStruct<T4>( transform, groupNumber ));
        }

        /// <inheritdoc cref="GetDependencyStruct{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3, T4, T5) GetDependenciesStruct<T1, T2, T3, T4, T5>(Transform transform, ushort groupNumber = 0)
            where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct 
        {
            return (GetDependencyStruct<T1>( transform, groupNumber ), GetDependencyStruct<T2>( transform, groupNumber ), GetDependencyStruct<T3>( transform, groupNumber ), GetDependencyStruct<T4>( transform, groupNumber ), GetDependencyStruct<T5>( transform, groupNumber ));
        }

#endregion

#region Helper Extensions

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T GetDependency<T>(this Component component) where T : class {
            return GetDependency<T>( component.transform );
        }
        
        /// <inheritdoc cref="DependencyExists{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static bool DependencyExists<T>(this Component component) where T : class {
            return DependencyExists<T>( component.transform );
        }
        
        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static (T1, T2) GetDependencies<T1, T2>(this Component component)
            where T1 : class where T2 : class 
        {
            return GetDependencies<T1, T2>( component.transform );
        }
        
        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3) GetDependencies<T1, T2, T3>(this Component component)
            where T1 : class where T2 : class where T3 : class 
        {
            return GetDependencies<T1, T2, T3>( component.transform );
        }

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3, T4) GetDependencies<T1, T2, T3, T4>(this Component component)
            where T1 : class where T2 : class where T3 : class where T4 : class 
        {
            return GetDependencies<T1, T2, T3, T4>( component.transform );
        }

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3, T4, T5) GetDependencies<T1, T2, T3, T4, T5>(this Component component)
            where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class 
        {
            return GetDependencies<T1, T2, T3, T4, T5>( component.transform );
        }
        
        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T GetDependencyStruct<T>(this Component component) where T : struct {
            return GetDependencyStruct<T>( component.transform );
        }
        
        /// <inheritdoc cref="DependencyExists{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static bool DependencyStructExists<T>(this Component component) where T : struct {
            return DependencyStructExists<T>( component.transform );
        }
        
        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static (T1, T2) GetDependenciesStruct<T1, T2>(this Component component)
            where T1 : struct where T2 : struct 
        {
            return GetDependenciesStruct<T1, T2>( component.transform );
        }
        
        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3) GetDependenciesStruct<T1, T2, T3>(this Component component)
            where T1 : struct where T2 : struct where T3 : struct 
        {
            return GetDependenciesStruct<T1, T2, T3>( component.transform );
        }

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3, T4) GetDependenciesStruct<T1, T2, T3, T4>(this Component component)
            where T1 : struct where T2 : struct where T3 : struct where T4 : struct 
        {
            return GetDependenciesStruct<T1, T2, T3, T4>( component.transform );
        }

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3, T4, T5) GetDependenciesStruct<T1, T2, T3, T4, T5>(this Component component)
            where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct 
        {
            return GetDependenciesStruct<T1, T2, T3, T4, T5>( component.transform );
        }

#endregion

        
        internal static void AddContext(BContext context) {
#if B_DEBUG
            if (DEBUG_LOG) {
                Debug.Log( $"adding {context.name} to [{string.Join( ", ", _contexts.Select( c => c.name ) )}]" );
            }
#endif
            _contexts.Add( context );
            if (!_groupedContexts.TryGetValue( context.Group, out var list ))
                _groupedContexts[context.Group] = list = new List<BContext>( 4 );
            list.Add( context );
            UpdateSceneRootContexts();
            UpdateRootContext();
        }

        internal static void RemoveContext(BContext context) {
#if B_DEBUG
            if (DEBUG_LOG) {
                Debug.Log( $"removing {(context ? context.name : "null")} from [{string.Join( ", ", _contexts.Select( c => c ? c.name : "null" ) )}]" );
            }
#endif
            _contexts.Remove( context );
            if (_groupedContexts.TryGetValue( context.Group, out var list )) 
                list.Remove( context );
            UpdateSceneRootContexts();
            UpdateRootContext();
        }


        /// <summary>
        /// Updates <see cref="_sceneRootContexts"/>
        /// </summary>
        internal static void UpdateSceneRootContexts() {
            _sceneRootContexts.Clear();
            Dictionary<Scene, int> sceneRootHierarchyOrders = new( 4 );
            for (int i = 0; i < SceneManager.sceneCount; i++) {
                _sceneRootContexts.Add( SceneManager.GetSceneAt( i ), null );
                sceneRootHierarchyOrders.Add( SceneManager.GetSceneAt( i ), int.MaxValue );
            }
            for (int i = 0; i < _contexts.Count; i++) {
                var order = GetHierarchyOrder( _contexts[i].transform );
                var scene = _contexts[i].gameObject.scene;
                if (sceneRootHierarchyOrders[scene] > order) {
                    sceneRootHierarchyOrders[scene] = order;
                    _sceneRootContexts[scene] = _contexts[i];
                }
            }

#if B_DEBUG
            if (DEBUG_LOG) {
                Debug.Log( $"scene root contexts: [{string.Join( ", ", _sceneRootContexts.Select( s => $"{s.Key.name}:{(s.Value ? s.Value.name : "null")}" ) )}]" );
            }
#endif
        }

        /// <summary>
        /// Updates <see cref="_rootContext"/> based on <see cref="_sceneRootContexts"/>. Be sure to use this after
        /// <see cref="UpdateSceneRootContexts"/>.
        /// </summary>
        internal static void UpdateRootContext() {
            // save scene indexes
            Dictionary<Scene, int> sceneOrder = new( _sceneRootContexts.Count );
            for (int i = 0; i < SceneManager.sceneCount; i++) 
                sceneOrder.Add( SceneManager.GetSceneAt( i ), i );
            // find first index
            int rootContextSceneIndex = int.MaxValue;
            foreach (var (scene, context) in _sceneRootContexts) {
                var order = sceneOrder[scene];
                if (rootContextSceneIndex > order) {
                    rootContextSceneIndex = order;
                    _rootContext = context;
                }
            }
#if B_DEBUG
            if (DEBUG_LOG) {
                Debug.Log( $"root context is {_rootContext?.name ?? "null"}" );
            }
#endif
        }

        /// <summary>
        /// Returns the index of which the transform will show up in hierarchy if everything is expanded
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        static int GetHierarchyOrder(Transform transform) {
            int order = 0;
            do {
                order += transform.GetSiblingIndex();
                transform = transform.parent;
            } while (transform);

            return order;
        }
    }
}