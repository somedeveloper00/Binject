#if (UNITY_EDITOR || DEVELOPMENT_BUILD || DEBUG) && BINJECT_VERBOSE && !BINJECT_SILENT
    #define B_DEBUG
#endif

#if !BINJECT_SILENT
    #define WARN
#endif


using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Binject {
    [ExecuteAlways] 
    public static class BinjectManager {

        /// <summary>
        /// Topmost scene. can be used to detect the top root context
        /// </summary>
        [NonSerialized] static Scene _topMostScene;
        
        /// <summary>
        /// Contexts grouped per scene. index 0 is root. only scenes with at least 1 <see cref="BContext"/> are
        /// contained here; when they reach zero length, they'll be removed from the dictionary altogether.
        /// </summary>
        [NonSerialized] static readonly Dictionary<Scene, List<BContext>> _sceneContexts = new( 64 );
        
        /// <summary>
        /// contexts grouped per <see cref="BContext.Group"/>. only groups with at least 1 <see cref="BContext"/> are
        /// contained here; when they reach zero length, they'll be removed from the dictionary altogether.
        /// </summary>
        [NonSerialized] static readonly Dictionary<ushort, List<BContext>> _groupedContexts = new( 64 );

        /// <summary>
        /// Finds the first context in self or it's parent. it'll go to other scenes if didn't find any. if nothing was
        /// found, it'll create a new <see cref="BContext"/> on the given <see cref="transform"/>.
        /// </summary>
        public static BContext GetNearestContext(Transform transform, ushort groupNumber = 0) {
            List<BContext> groupList = null;
            if (groupNumber != 0 && !_groupedContexts.TryGetValue( groupNumber, out groupList )) {
                goto CreateComponent;
            }


            if (_sceneContexts.TryGetValue( transform.gameObject.scene, out var contextsInScene )) {
                var originalTransform = transform;
                // parents
                while (transform) {
                    for (int i = 0; i < contextsInScene.Count; i++)
                        if (transform == contextsInScene[i].transform && isCorrectGroup( contextsInScene[i], groupNumber ))
                            return contextsInScene[i];
                    transform = transform.parent;
                }

                // scene root
                if (isCorrectGroup( contextsInScene[0], groupNumber ))
                    return contextsInScene[0];
                transform = originalTransform;
            }

            // topmost root
            var topmostRoot = _sceneContexts[_topMostScene][0];
            if (isCorrectGroup( topmostRoot, groupNumber ))
                return topmostRoot;

            // root of grouped contexts
            if (groupList is not null)
                return groupList[0];
            
            // create a component
            CreateComponent:
#if WARN
            Debug.LogWarning( $"No context found with the group {groupNumber}. Creating a new one on the game " +
                              "object instead", transform );
#endif
            return transform.gameObject.AddComponent<BContext>();
        }

        /// <summary>
        /// Finds the context holding the required dependencies of type <see cref="T"/> compatible with the given
        /// <see cref="Transform"/>. returns null if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining)]
        public static BContext FindContext<T>(Transform transform, ushort groupNumber = 0) {

            var scene = transform.gameObject.scene;
            
            // search in scene
            if (_sceneContexts.TryGetValue( scene, out var contextsInScene )) {
                
                // check parents
                while (transform) {
                    for (int i = 0; i < contextsInScene.Count; i++) {
                        var context = contextsInScene[i];
                        if (isCorrectGroup( context, groupNumber ) && context.transform == transform) {
                            if (context.HasDependency<T>()) return context;
                        }
                    }
                    transform = transform.parent;
                }

                // check scene root context
                if (isCorrectGroup( contextsInScene[0], groupNumber ) && contextsInScene[0].HasDependency<T>())
                    return contextsInScene[0];
            }

            // check topmost scene root context
            if (_topMostScene != scene) {
                var root = _sceneContexts[_topMostScene][0];
                if (isCorrectGroup( root, groupNumber ) && root.HasDependency<T>())
                    return root;
            }

            // check grouped contexts from any scene
            if (_groupedContexts.TryGetValue( groupNumber, out var list ) && list.Count > 0) {
                for (int i = 0; i < list.Count; i++) // starting from 0 so we'll get root first
                    if (list[i].HasDependency<T>())
                        return list[i];
            }

#if WARN
            Debug.LogWarning( $"No context found containing the dependency type {typeof(T).FullName}" );
#endif
            return null;
        }
   
        
        /// <summary>
        /// Adds the context to internal lists and updates caches
        /// </summary>
        internal static void AddContext(BContext context) {
#if B_DEBUG
            Debug.Log( $"adding {context.name}" );
#endif
            // add to lists
            if (!_groupedContexts.TryGetValue( context.Group, out var glist ))
                _groupedContexts[context.Group] = glist = new List<BContext>( 4 );
            glist.Add( context );
            if (!_sceneContexts.TryGetValue( context.gameObject.scene, out var slist ))
                _sceneContexts[context.gameObject.scene] = slist = new List<BContext>( 4 );
            slist.Add( context );

            UpdateAllRootContextsAndTopmostScene();
        }

        /// <summary>
        /// Removes the context from internal lists and updates caches
        /// </summary>
        internal static void RemoveContext(BContext context) {
#if B_DEBUG
            Debug.Log( $"removing {(context ? context.name : "null")}" );
#endif
            bool changed = false;
            
            // remove from lists
            if (_groupedContexts.TryGetValue( context.Group, out var glist )) {
                changed = glist.Remove( context );
                if (changed && glist.Count == 0)
                    _groupedContexts.Remove( context.Group );
            }
            if (_sceneContexts.TryGetValue( context.gameObject.scene, out var slist )) {
                changed |= slist.Remove( context );
                if (changed && slist.Count == 0) 
                    _sceneContexts.Remove( context.gameObject.scene );
            }

            if (changed) UpdateAllRootContextsAndTopmostScene();
        }

        /// <summary>
        /// Updates <see cref="_sceneContexts"/>'s roots, <see cref="_groupedContexts"/>'s roots and
        /// <see cref="_topMostScene"/>. (root is index 0 of a <see cref="BContext"/> list)
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        static void UpdateAllRootContextsAndTopmostScene() {
            if (_sceneContexts.Count == 0) {
                _topMostScene = default;
                return;
            }
            
            // scene roots
            int rootOrder;
            foreach (var contexts in _sceneContexts.Values) {
                rootOrder = GetHierarchyOrder( contexts[0].transform );
                int index = 0;
                for (int i = 1; i < contexts.Count; i++) {
                    var order = GetHierarchyOrder( contexts[i].transform );
                    if (order < rootOrder) {
                        rootOrder = order;
                        index = i;
                    }
                }
                // swap old root with new root
                if (index != 0) 
                    (contexts[0], contexts[index]) = (contexts[index], contexts[0]);
            }
            
            // group roots (and topmost scene at the same time)
            Dictionary<Scene, int> sceneOrder = new( SceneManager.sceneCount );
            bool foundTopmostScene = false;
            for (int i = 0; i < SceneManager.sceneCount; i++) {
                sceneOrder[SceneManager.GetSceneAt( i )] = i;
                
                // resolving topmost scene right here
                var scene = SceneManager.GetSceneAt( i );
                if (!foundTopmostScene && _sceneContexts.ContainsKey( scene )) {
                    _topMostScene = scene;
                    foundTopmostScene = true;
                }
            }
            
            foreach (var contexts in _groupedContexts.Values) {
                const int SCENE_BENEFIT = 1_000_000;
                rootOrder = GetHierarchyOrder( contexts[0].transform ) * sceneOrder[contexts[0].gameObject.scene] * SCENE_BENEFIT;
                int index = 0;
                for (int i = 1; i < contexts.Count; i++) {
                    var order = GetHierarchyOrder( contexts[i].transform ) * sceneOrder[contexts[i].gameObject.scene] * SCENE_BENEFIT;
                    if (order < rootOrder) {
                        rootOrder = order;
                        index = i;
                    }
                }
                // swap old root with new root
                if (index != 0) 
                    (contexts[0], contexts[index]) = (contexts[index], contexts[0]);
            }
        }

        /// <summary>
        /// checks whether or not the <see cref="context"/> is compatible with the given <see cref="groupNumber"/>
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining)]
        static bool isCorrectGroup(BContext context, ushort groupNumber) => groupNumber == 0 || groupNumber == context.Group;
        
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

#region Class Dependencies

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
        

#endregion

#region Struct Dependencies

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

        /// <inheritdoc cref="GetNearestContext(UnityEngine.Transform,ushort)"/>
        public static BContext GetNearestContext(this Component component, ushort groupNumber = 0) =>
            GetNearestContext( component.transform, groupNumber );

        /// <inheritdoc cref="FindContext{T}(UnityEngine.Transform,ushort)"/>
        public static BContext FindContext<T>(this Component component, ushort groupNumber = 0) =>
            FindContext<T>( component.transform, groupNumber );

#endregion

    }
}