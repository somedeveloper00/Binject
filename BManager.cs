using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Binject {
    public static class BManager {
        static readonly List<BContext> _contexts = new List<BContext>( 16 );
        static int _rootContextIndex;

        /// <summary>
        /// Finds the context holding the required dependencies of type <see cref="T"/> compatible with the given
        /// <see cref="Transform"/>. returns null if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining)]
        public static BContext FindContext<T>(Transform transform) where T : struct, IBDependency {
            
            [MethodImpl( MethodImplOptions.AggressiveInlining)]
            bool isTheCorrectContext(int i) {
                return _contexts[i].transform == transform && _contexts[i].HasDependency<T>();
            }
            
            do {
                for (int i = 0; i < _contexts.Count; i++)
                    if (isTheCorrectContext( i )) return _contexts[i];

                if (!transform.parent && transform != _contexts[_rootContextIndex].transform) {
                    // root context check
                    if (isTheCorrectContext( _rootContextIndex )) return _contexts[_rootContextIndex];
                }
                transform = transform.parent;
            } while (transform);

            return null;
        }

#region Helper Functions

        /// <summary>
        /// Returns the dependency of type <see cref="T"/> from a compatible context. returns default if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining)]
        public static T GetDependency<T>(Transform transform) where T : struct, IBDependency {
            var context = FindContext<T>( transform );
            if (context == null) return default;
            return context.GetDependencyNoCheck<T>();
        }

        /// <summary>
        /// Checks if the dependency of type <see cref="T"/> exists in a compatible context.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool DependencyExists<T>(Transform transform) where T : struct, IBDependency {
            var context = FindContext<T>( transform );
            return context != null;
        }
        
        /// <summary>
        /// Returns dependencies of the given type from a compatible context. for each dependency, returns default
        /// if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2) GetDependencies<T1, T2>(Transform transform)
            where T1 : struct, IBDependency
            where T2 : struct, IBDependency 
        {
            return (GetDependency<T1>( transform ), GetDependency<T2>( transform ));
        }

        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3) GetDependencies<T1, T2, T3>(Transform transform)
            where T1 : struct, IBDependency
            where T2 : struct, IBDependency 
            where T3 : struct, IBDependency 
        {
            return (GetDependency<T1>( transform ), GetDependency<T2>( transform ), GetDependency<T3>( transform ));
        }

        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3, T4) GetDependencies<T1, T2, T3, T4>(Transform transform)
            where T1 : struct, IBDependency
            where T2 : struct, IBDependency 
            where T3 : struct, IBDependency 
            where T4 : struct, IBDependency 
        {
            return (GetDependency<T1>( transform ), GetDependency<T2>( transform ), GetDependency<T3>( transform ), GetDependency<T4>( transform ));
        }

        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static (T1, T2, T3, T4, T5) GetDependencies<T1, T2, T3, T4, T5>(Transform transform)
            where T1 : struct, IBDependency
            where T2 : struct, IBDependency 
            where T3 : struct, IBDependency 
            where T4 : struct, IBDependency 
            where T5 : struct, IBDependency 
        {
            return (GetDependency<T1>( transform ), GetDependency<T2>( transform ), GetDependency<T3>( transform ), GetDependency<T4>( transform ), GetDependency<T5>( transform ));
        }

#endregion

#region Helper Extensions

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T GetDependency<T>(this Component component) where T : struct, IBDependency {
            return GetDependency<T>( component.transform );
        }
        
        /// <inheritdoc cref="DependencyExists{T}(UnityEngine.Transform)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static bool DependencyExists<T>(this Component component) where T : struct, IBDependency {
            return DependencyExists<T>( component.transform );
        }
        
        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static void GetDependencies<T1, T2>(this Component component, out T1 t1, out T2 t2)
            where T1 : struct, IBDependency
            where T2 : struct, IBDependency 
        {
            (t1, t2) = GetDependencies<T1, T2>( component.transform );
        }
        
        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static void GetDependencies<T1, T2, T3>(this Component component, out T1 t1, out T2 t2, out T3 t3)
            where T1 : struct, IBDependency
            where T2 : struct, IBDependency 
            where T3 : struct, IBDependency 
        {
            (t1, t2, t3) = GetDependencies<T1, T2, T3>( component.transform );
        }

        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static void GetDependencies<T1, T2, T3, T4>(this Component component, out T1 t1, out T2 t2, out T3 t3, out T4 t4)
            where T1 : struct, IBDependency
            where T2 : struct, IBDependency 
            where T3 : struct, IBDependency 
            where T4 : struct, IBDependency 
        {
            (t1, t2, t3, t4) = GetDependencies<T1, T2, T3, T4>( component.transform );
        }

        /// <inheritdoc cref="GetDependencies{T1,T2}(UnityEngine.Transform)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static void GetDependencies<T1, T2, T3, T4, T5>(this Component component, out T1 t1, out T2 t2, out T3 t3, out T4 t4, out T5 t5)
            where T1 : struct, IBDependency
            where T2 : struct, IBDependency 
            where T3 : struct, IBDependency 
            where T4 : struct, IBDependency 
            where T5 : struct, IBDependency 
        {
            (t1, t2, t3, t4, t5) = GetDependencies<T1, T2, T3, T4, T5>( component.transform );
        }

#endregion

        
        internal static void AddContext(BContext context) {
            _contexts.Add( context );
            UpdateRootContext( new HashSet<int> { _contexts.Count - 1 } );
        }

        internal static void RemoveContext(BContext context) {
            var ind = _contexts.IndexOf( context );
            _contexts.RemoveAt( ind );
        }


        /// <summary>
        /// Sorts contexts based on their hierarchy depth and index.
        /// </summary>
        static void UpdateRootContext(HashSet<int> dirties) {
            int rootContextHierarchyIndex = -1;
            for (int i = 0; i < _contexts.Count; i++) {
                var ind = GetHierarchyIndex( _contexts[i].transform );
                if (rootContextHierarchyIndex < ind) {
                    rootContextHierarchyIndex = ind;
                    _rootContextIndex = i;
                }
            }
        }

        /// <summary>
        /// Returns the index of which the transform will show up in hierarchy if everything is expanded
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        static int GetHierarchyIndex(Transform transform) {
            int c = 0;
            do {
                c += transform.GetSiblingIndex();
                transform = transform.parent;
            } while (transform.parent);

            return c;
        }


    }
}