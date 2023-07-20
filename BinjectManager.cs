#if (UNITY_EDITOR || DEVELOPMENT_BUILD || DEBUG) && BINJECT_VERBOSE && !BINJECT_SILENT
    #define B_DEBUG
#endif

#if !BINJECT_SILENT
    #define WARN
#endif

using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Runtime.CompilerServices;

namespace Binject {
    [ExecuteAlways] 
    public static class BinjectManager {

#if B_DEBUG
        static BinjectManager() => Debug.Log( "Binject Domain Reloaded" );
#endif

        /// <summary>
        /// Topmost scene handle. can be used to detect the top root context
        /// </summary>
        [NonSerialized] static int _topMostScene;
        
        /// <summary>
        /// Contexts grouped per scene (key is <see cref="Scene.handle"/>). index 0 is root. only scenes with at least 1 <see cref="BContext"/> are
        /// contained here; when they reach zero length, they'll be removed from the dictionary altogether.
        /// </summary>
        [NonSerialized] static readonly Dictionary<int, List<BContext>> _sceneContexts = new( 64 );
        
        /// <summary>
        /// contexts grouped per <see cref="BContext.Group"/>. only groups with at least 1 <see cref="BContext"/> are
        /// contained here; when they reach zero length, they'll be removed from the dictionary altogether.
        /// </summary>
        [NonSerialized] static readonly Dictionary<ushort, List<BContext>> _groupedContexts = new( 64 );

#region Publics

        /// <summary>
        /// Returns the root <see cref="BContext"/> of this scene. It will create new <see cref="BContext"/> component
        /// on this gameObject instead.
        /// </summary>
        public static BContext GetSceneRootContext(Transform transform) {
            if (_sceneContexts.TryGetValue( transform.gameObject.scene.handle, out var list ))
                return list[0];
#if WARN
            Debug.LogWarning( $"No context found in scene {transform.gameObject.scene}, Creating a new " +
                              $"{nameof(BContext)} component on the game object instead.", transform );
#endif
            return transform.gameObject.AddComponent<BContext>();
        }

        /// <summary>
        /// Finds the first context in self or it's parent. It'll go to other scenes if didn't find any. if nothing was
        /// found, it'll create a new <see cref="BContext"/> component on the gameObject.
        /// </summary>
        public static BContext FindNearestContext(Transform transform, ushort groupNumber = 0) {
            List<BContext> groupList = null;
            if (groupNumber != 0 && !_groupedContexts.TryGetValue( groupNumber, out groupList )) {
                goto CreateComponent;
            }

            if (_sceneContexts.TryGetValue( transform.gameObject.scene.handle, out var contextsInScene )) {
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
        /// Finds the compatible context holding the specified dependency. returns null if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining)]
        public static BContext FindContext<T>(Transform transform, ushort groupNumber = 0) {

            var scene = transform.gameObject.scene;
            
            // search in scene
            if (_sceneContexts.TryGetValue( scene.handle, out var contextsInScene )) {
                
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
            if (_topMostScene != scene.handle) {
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

#endregion

#region Non Publics
        
        /// <summary>
        /// Adds the context to internal lists and updates caches
        /// </summary>
        internal static void AddContext(BContext context) {
#if B_DEBUG
            Debug.Log( $"adding {context.name}({context.gameObject.scene.name}). all: {CreateStringListOfAllContexts()}" );
#endif
            // add to lists
            if (!_groupedContexts.TryGetValue( context.Group, out var glist ))
                _groupedContexts[context.Group] = glist = new List<BContext>( 4 );
            glist.Add( context );
            if (!_sceneContexts.TryGetValue( context.gameObject.scene.handle, out var slist ))
                _sceneContexts[context.gameObject.scene.handle] = slist = new List<BContext>( 4 );
            slist.Add( context );

            UpdateAllRootContextsAndTopmostScene();
        }

        /// <summary>
        /// Removes the context from internal lists and updates caches
        /// </summary>
        internal static void RemoveContext(BContext context) {
#if B_DEBUG
            Debug.Log( $"removing {(context ? $"{context.name}({context.gameObject.scene.name})" : "null")}. all: {CreateStringListOfAllContexts()}" );
#endif
            bool changed = false;
            
            // remove from lists
            if (_groupedContexts.TryGetValue( context.Group, out var glist )) {
                changed = glist.Remove( context );
                if (changed && glist.Count == 0)
                    _groupedContexts.Remove( context.Group );
            }
            if (_sceneContexts.TryGetValue( context.gameObject.scene.handle, out var slist )) {
                changed |= slist.Remove( context );
                if (changed && slist.Count == 0) 
                    _sceneContexts.Remove( context.gameObject.scene.handle );
            }

            if (changed) UpdateAllRootContextsAndTopmostScene();
        }
        
#if B_DEBUG
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        static string CreateStringListOfAllContexts() =>
            $"[{string.Join( ", ", _sceneContexts.SelectMany( s => s.Value ).Select( c => $"{c.name}({c.gameObject.scene.name})" ) )}]";
#endif

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
                if (index != 0) {
                    (contexts[0], contexts[index]) = (contexts[index], contexts[0]);
#if B_DEBUG
                    Debug.Log( $"Root of scene '{contexts[0].gameObject.scene.name}' changed: {contexts[0].name}({contexts[0].gameObject.scene.name})" );
#endif
                }
            }
            
            // group roots (and topmost scene at the same time)
            Dictionary<Scene, int> sceneOrder = new( SceneManager.sceneCount );
            bool foundTopmostScene = false;
            for (int i = 0; i < SceneManager.sceneCount; i++) {
                sceneOrder[SceneManager.GetSceneAt( i )] = i;
                
                // resolving topmost scene right here
                var scene = SceneManager.GetSceneAt( i );
                if (!foundTopmostScene && _sceneContexts.ContainsKey( scene.handle )) {
#if B_DEBUG
                    if (_topMostScene != scene.handle) Debug.Log( $"Topmost scene changed: {scene.name}" );
#endif
                    _topMostScene = scene.handle;
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
                if (index != 0) {
                    (contexts[0], contexts[index]) = (contexts[index], contexts[0]);
#if B_DEBUG
                    Debug.Log( $"Root of group '{contexts[0].Group}' changed: {contexts[0].name}({contexts[0].gameObject.scene.name})" );
#endif
                }
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
        

#endregion

#region Public Helpers

        /// <summary>
        /// Returns the dependency from a compatible context. returns default if not found any.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T GetDependency<T>(Transform transform, ushort groupNumber = 0) {
            var context = FindContext<T>( transform, groupNumber );
            return context == null ? default : context.GetDependencyNoCheck<T>();
        }

        /// <summary>
        /// Returns the dependency from a compatible context. returns default if not found any. <para/>
        /// Use this for `struct`s to avoid boxing and get better performance.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T GetDependencyStruct<T>(Transform transform, ushort groupNumber = 0) where T : struct {
            var context = FindContext<T>( transform, groupNumber );
            return context == null ? default : context.GetDependencyStructNoCheck<T>();
        }

        
        /// <summary>
        /// Finds the dependency from a compatible context and returns `true` if found any, and `false` if didn't.
        /// <see cref="result"/> will be default if didn't find any. <para/>
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool TryGetDependency<T>(Transform transform, out T result, ushort groupNumber = 0) {
            var context = FindContext<T>( transform, groupNumber );
            result = context is null ? default : context.GetDependencyNoCheck<T>();
            return context is not null;
        }

        /// <summary>
        /// Finds the dependency from a compatible context and returns `true` if found any, and `false` if didn't.
        /// <see cref="result"/> will be default if didn't find any. <para/>
        /// Use this for `struct`s to avoid boxing and get better performance.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool TryGetDependencyStruct<T>(Transform transform, out T result, ushort groupNumber = 0) where T : struct {
            var context = FindContext<T>( transform, groupNumber );
            result = context == null ? default : context.GetDependencyStructNoCheck<T>();
            return context is not null;
        }

        /// <summary>
        /// Checks if the dependency exists in a compatible context.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool DependencyExists<T>(Transform transform, ushort groupNumber = 0) {
            return FindContext<T>( transform, groupNumber ) != null;
        }

#endregion

#region Extensions

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T GetDependency<T>(this Component component, ushort groupName = 0) {
            return GetDependency<T>( component.transform, groupName );
        }

        /// <inheritdoc cref="GetDependencyStruct{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T GetDependencyStruct<T>(this Component component, ushort groupNumber = 0) where T : struct {
            return GetDependencyStruct<T>( component.transform, groupNumber );
        }
        
        /// <inheritdoc cref="TryGetDependency{T}(UnityEngine.Transform,out T,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool TryGetDependency<T>(this Component component, out T result, ushort groupNumber = 0) => 
            TryGetDependency( component.transform, out result, groupNumber );
        
        /// <inheritdoc cref="TryGetDependencyStruct{T}(UnityEngine.Transform,out T,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool TryGetDependencyStruct<T>(this Component component, out T result, ushort groupNumber = 0) where T : struct => 
            TryGetDependencyStruct( component.transform, out result, groupNumber );

        /// <inheritdoc cref="DependencyExists{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl (MethodImplOptions.AggressiveInlining )]
        public static bool DependencyExists<T>(this Component component, ushort groupName = 0) {
            return DependencyExists<T>( component.transform, groupName );
        }

        /// <inheritdoc cref="FindNearestContext(UnityEngine.Transform,ushort)"/>
        public static BContext FindNearestContext(this Component component, ushort groupNumber = 0) =>
            FindNearestContext( component.transform, groupNumber );

        /// <inheritdoc cref="FindContext{T}(UnityEngine.Transform,ushort)"/>
        public static BContext FindContext<T>(this Component component, ushort groupNumber = 0) =>
            FindContext<T>( component.transform, groupNumber );

        /// <inheritdoc cref="GetSceneRootContext(UnityEngine.Transform)"/>
        public static BContext GetSceneRootContext(this Component component) =>
            GetSceneRootContext( component.transform );


#region Multis

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static void GetDependency<T1, T2>(this Component component, out T1 result1, out T2 result2, ushort groupName = 0) {
            result1 = GetDependency<T1>( component.transform, groupName );
            result2 = GetDependency<T2>( component.transform, groupName );
        }

        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static void GetDependency<T1, T2, T3>(this Component component, out T1 result1, out T2 result2, out T3 result3, ushort groupName = 0) {
            result1 = GetDependency<T1>( component.transform, groupName );
            result2 = GetDependency<T2>( component.transform, groupName );
            result3 = GetDependency<T3>( component.transform, groupName );
        }
        
        /// <inheritdoc cref="GetDependency{T}(UnityEngine.Transform,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static void GetDependency<T1, T2, T3, T4>(this Component component, out T1 result1, out T2 result2, out T3 result3, out T4 result4, ushort groupName = 0) {
            result1 = GetDependency<T1>( component.transform, groupName );
            result2 = GetDependency<T2>( component.transform, groupName );
            result3 = GetDependency<T3>( component.transform, groupName );
            result4 = GetDependency<T4>( component.transform, groupName );
        }

        
        /// <inheritdoc cref="GetDependencyStruct{T}(UnityEngine.Component,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static void GetDependencyStruct<T1, T2>(this Component component, out T1 result1, out T2 result2, ushort groupName = 0) 
            where T1 : struct where T2 : struct 
        {
            result1 = GetDependencyStruct<T1>( component.transform, groupName );
            result2 = GetDependencyStruct<T2>( component.transform, groupName );
        }

        /// <inheritdoc cref="GetDependencyStruct{T}(UnityEngine.Component,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static void GetDependencyStruct<T1, T2, T3>(this Component component, out T1 result1, out T2 result2, out T3 result3, ushort groupName = 0) 
            where T1 : struct where T2 : struct where T3 : struct
        {
            result1 = GetDependencyStruct<T1>( component.transform, groupName );
            result2 = GetDependencyStruct<T2>( component.transform, groupName );
            result3 = GetDependencyStruct<T3>( component.transform, groupName );
        }
        
        /// <inheritdoc cref="GetDependencyStruct{T}(UnityEngine.Component,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static void GetDependencyStruct<T1, T2, T3, T4>(this Component component, out T1 result1, out T2 result2, out T3 result3, out T4 result4, ushort groupName = 0)  
            where T1 : struct where T2 : struct where T3 : struct where T4 : struct
        {
            result1 = GetDependencyStruct<T1>( component.transform, groupName );
            result2 = GetDependencyStruct<T2>( component.transform, groupName );
            result3 = GetDependencyStruct<T3>( component.transform, groupName );
            result4 = GetDependencyStruct<T4>( component.transform, groupName );
        }
#endregion

#endregion
    }
}