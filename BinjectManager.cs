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
        [NonSerialized] static BContext _rootContext;

#if UNITY_EDITOR
        const bool DEBUG_LOG = false; 
#endif

        /// <summary>
        /// Finds the context holding the required dependencies of type <see cref="T"/> compatible with the given
        /// <see cref="Transform"/>. returns null if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining)]
        public static BContext FindContext<T>(Transform transform) where T : IBDependency {
            
            [MethodImpl( MethodImplOptions.AggressiveInlining)]
            bool isTheCorrectContext(BContext context) {
                return context.transform == transform && context.HasDependency<T>();
            }

            do {
                for (int i = 0; i < _contexts.Count; i++)
                    if (isTheCorrectContext( _contexts[i] )) return _contexts[i];

                if (!transform.parent && transform != _rootContext.transform) {
                    // root context check
                    transform = _rootContext.transform;
                    if (isTheCorrectContext( _rootContext )) return _rootContext;
                }

                transform = transform.parent;
            } while (transform);

            Debug.LogWarning( $"No context found containing the dependency type {typeof(T).FullName}" );

            return null;
        }

#region Helper Functions

        /// <summary>
        /// Returns the dependency of type <see cref="T"/> from a compatible context. returns default if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining)]
        public static T GetDependency<T>(Transform transform) where T : IBDependency {
            var context = FindContext<T>( transform );
            if (context == null) return default;
            return context.GetDependencyNoCheck<T>();
        }

        /// <summary>
        /// Checks if the dependency of type <see cref="T"/> exists in a compatible context.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool DependencyExists<T>(Transform transform) where T : IBDependency {
            var context = FindContext<T>( transform );
            return context != null;
        }
        
        /// <summary>
        /// Returns dependencies of the given type from a compatible context. for each dependency, returns default
        /// if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2) GetDependencies<T1, T2>(Transform transform)
            where T1 : IBDependency
            where T2 : IBDependency 
        {
            return (GetDependency<T1>( transform ), GetDependency<T2>( transform ));
        }

        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3) GetDependencies<T1, T2, T3>(Transform transform)
            where T1 : IBDependency
            where T2 : IBDependency 
            where T3 : IBDependency 
        {
            return (GetDependency<T1>( transform ), GetDependency<T2>( transform ), GetDependency<T3>( transform ));
        }

        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3, T4) GetDependencies<T1, T2, T3, T4>(Transform transform)
            where T1 : IBDependency
            where T2 : IBDependency 
            where T3 : IBDependency 
            where T4 : IBDependency 
        {
            return (GetDependency<T1>( transform ), GetDependency<T2>( transform ), GetDependency<T3>( transform ), GetDependency<T4>( transform ));
        }

        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3, T4, T5) GetDependencies<T1, T2, T3, T4, T5>(Transform transform)
            where T1 : IBDependency
            where T2 : IBDependency 
            where T3 : IBDependency 
            where T4 : IBDependency 
            where T5 : IBDependency 
        {
            return (GetDependency<T1>( transform ), GetDependency<T2>( transform ), GetDependency<T3>( transform ), GetDependency<T4>( transform ), GetDependency<T5>( transform ));
        }

#endregion

#region Helper Extensions

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T GetDependency<T>(this Component component) where T : IBDependency {
            return GetDependency<T>( component.transform );
        }
        
        /// <inheritdoc cref="DependencyExists{T}(UnityEngine.Transform)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static bool DependencyExists<T>(this Component component) where T : IBDependency {
            return DependencyExists<T>( component.transform );
        }
        
        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static void GetDependencies<T1, T2>(this Component component, out T1 t1, out T2 t2)
            where T1 : IBDependency
            where T2 : IBDependency 
        {
            (t1, t2) = GetDependencies<T1, T2>( component.transform );
        }
        
        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static void GetDependencies<T1, T2, T3>(this Component component, out T1 t1, out T2 t2, out T3 t3)
            where T1 : IBDependency
            where T2 : IBDependency 
            where T3 : IBDependency 
        {
            (t1, t2, t3) = GetDependencies<T1, T2, T3>( component.transform );
        }

        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static void GetDependencies<T1, T2, T3, T4>(this Component component, out T1 t1, out T2 t2, out T3 t3, out T4 t4)
            where T1 : IBDependency
            where T2 : IBDependency 
            where T3 : IBDependency 
            where T4 : IBDependency 
        {
            (t1, t2, t3, t4) = GetDependencies<T1, T2, T3, T4>( component.transform );
        }

        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static void GetDependencies<T1, T2, T3, T4, T5>(this Component component, out T1 t1, out T2 t2, out T3 t3, out T4 t4, out T5 t5)
            where T1 : IBDependency
            where T2 : IBDependency 
            where T3 : IBDependency 
            where T4 : IBDependency 
            where T5 : IBDependency 
        {
            (t1, t2, t3, t4, t5) = GetDependencies<T1, T2, T3, T4, T5>( component.transform );
        }

#endregion

        
        internal static void AddContext(BContext context) {
#if UNITY_EDITOR
            if (DEBUG_LOG) {
                Debug.Log( $"adding {context.name} to [{string.Join( ", ", _contexts.Select( c => c.name ) )}]" );
            }
#endif
            _contexts.Add( context );
            UpdateRootContext();
        }

        internal static void RemoveContext(BContext context) {
#if UNITY_EDITOR
            if (DEBUG_LOG) {
                Debug.Log( $"removing {context.name} from [{string.Join( ", ", _contexts.Select( c => c.name ) )}]" );
            }
#endif
            var ind = _contexts.IndexOf( context );
            _contexts.RemoveAt( ind );
        }


        /// <summary>
        /// Sorts contexts based on their hierarchy depth and index.
        /// </summary>
        internal static void UpdateRootContext() {
#if UNITY_EDITOR
            bool changed = false;
#endif
            int rootContextHierarchyOrder = _rootContext ? GetHierarchyOrder( _rootContext.transform ) : int.MaxValue;
            for (int i = 0; i < _contexts.Count; i++) {
                if (!ReferenceEquals( _rootContext, _contexts[i] )) {
                    var order = GetHierarchyOrder( _contexts[i].transform );
                    if (rootContextHierarchyOrder > order) {
                        rootContextHierarchyOrder = order;
                        _rootContext = _contexts[i];
#if UNITY_EDITOR
                        changed = true;
#endif
                    }
                }
            }
#if UNITY_EDITOR
            if (DEBUG_LOG && changed) {
                Debug.Log( $"root updated: {_rootContext.name}" );
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