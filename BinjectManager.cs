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
        [NonSerialized] static SceneHandle _topMostScene;
        
        /// <summary>
        /// Contexts grouped per scene (key is <see cref="Scene.handle"/>). only scenes with at least 1 <see cref="BContext"/> are
        /// contained here; when they reach zero length, they'll be removed from the dictionary altogether.
        /// </summary>
        [NonSerialized] static readonly Dictionary<SceneHandle, BContextList> _sceneContexts = new( 4 );
        
        /// <summary>
        /// contexts grouped per <see cref="BContext.Group"/>. only groups with at least 1 <see cref="BContext"/> are
        /// contained here; when they reach zero length, they'll be removed from the dictionary altogether.
        /// </summary>
        [NonSerialized] static readonly Dictionary<ushort, BContextList> _groupedContexts = new( 4 );
        
#region Publics

        /// <summary>
        /// Returns the root <see cref="BContext"/> of this scene. It will create new <see cref="BContext"/> component
        /// on this gameObject instead.
        /// </summary>
        public static BContext GetSceneRootContext(Transform transform) {
            if (_sceneContexts.TryGetValue( new( transform.gameObject.scene ), out var list ))
                return list.GetRoot();
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
            BContextList groupList = null;
            if (groupNumber != 0 && !_groupedContexts.TryGetValue( groupNumber, out groupList )) {
                goto CreateComponent;
            }

            if (_sceneContexts.TryGetValue( new( transform.gameObject.scene ), out var contextsInScene )) {
                var originalTransform = transform;
                // parents
                while (transform is not null) {
                    for (int i = 0; i < contextsInScene.Count; i++) {
                        var context = contextsInScene[i];
                        if (isCorrectGroup( context, groupNumber ) && ReferenceEquals( transform, context.transform )) {
                            contextsInScene.AddPoint( i );
                            return context;
                        }
                    }

                    transform = transform.parent;
                }

                // scene root
                var root = contextsInScene.GetRoot();
                if (isCorrectGroup( root, groupNumber ))
                    return root;
                transform = originalTransform;
            }

            // topmost root
            var topmostRoot = _sceneContexts[_topMostScene].GetRoot();
            if (isCorrectGroup( topmostRoot, groupNumber ))
                return topmostRoot;

            // root of grouped contexts
            if (groupList is not null)
                return groupList.GetRoot();
            
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

            var sceneHandle = new SceneHandle( transform.gameObject.scene );

            // search in scene
            if (_sceneContexts.TryGetValue( sceneHandle, out var contextsInScene )) {
                
                // check parents
                while (transform is not null) {
                    // fast check
                    if (contextsInScene.ContainsTransform( transform )) {
                        // find
                        for (int i = 0; i < contextsInScene.Count; i++) { 
                            var context = contextsInScene[i];
                            if (isCorrectGroup( context, groupNumber ) && context.transform == transform ) {
                                if (context.HasDependency<T>()) {
                                    contextsInScene.AddPoint( i );
                                    return context;
                                }
                            }
                        }
                    }
                    transform = transform.parent;
                }

                // check scene root context
                var root = contextsInScene.GetRoot();
                if (isCorrectGroup( root, groupNumber ) && root.HasDependency<T>()) 
                    return root;
            }

            // check topmost scene root context
            if (_topMostScene.Equals( sceneHandle )) {
                var root = _sceneContexts[_topMostScene].GetRoot();
                if (isCorrectGroup( root, groupNumber ) && root.HasDependency<T>())
                    return root;
            }

            // check grouped contexts from any scene
            if (_groupedContexts.TryGetValue( groupNumber, out var list ) && list.Count > 0) {
                for (int i = 0; i < list.Count; i++) {
                    var context = list[i];
                    if (context.HasDependency<T>()) {
                        list.AddPoint( i );
                        return context;
                    }
                }
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
        internal static void AddContext(BContext context, SceneHandle sceneHandle) {
#if B_DEBUG
            Debug.Log( $"adding {context.name}({context.gameObject.scene.name}). all: {CreateStringListOfAllContexts()}", context );
#endif
            // add to lists
            if (!_groupedContexts.TryGetValue( context.Group, out var glist ))
                _groupedContexts[context.Group] = glist = new( 4 );
            glist.Add( context );
            if (!_sceneContexts.TryGetValue( sceneHandle, out var slist ))
                _sceneContexts[sceneHandle] = slist = new( 4 );
            slist.Add( context );

            UpdateAllRootContextsAndTopmostScene();
        }

        /// <summary>
        /// Removes the context from internal lists and updates caches
        /// </summary>
        internal static void RemoveContext(BContext context, SceneHandle sceneHandle) {
#if B_DEBUG
            Debug.Log( $"removing {(context ? $"{context.name}({context.gameObject.scene.name})" : "null")}. all: {CreateStringListOfAllContexts()}", context );
#endif
            bool changed = false;
            
            // remove from lists
            if (_groupedContexts.TryGetValue( context.Group, out var glist )) {
                changed = glist.Remove( context );
                if (changed && glist.Count == 0)
                    _groupedContexts.Remove( context.Group );
            }
            if (_sceneContexts.TryGetValue( sceneHandle, out var slist )) {
                changed |= slist.Remove( context );
                if (changed && slist.Count == 0) 
                    _sceneContexts.Remove( sceneHandle );
            }

            if (changed) UpdateAllRootContextsAndTopmostScene();
        }

        /// <summary>
        /// Updates the internal lists based on the scene change. Will also update all the root contexts. <para/>
        /// <b> It's an expensive call! </b>
        /// </summary>
        internal static void UpdateContextScene(BContext context, SceneHandle previousScene) {
            var sceneHandle = new SceneHandle( context.gameObject.scene );
            if (sceneHandle.Value == previousScene.Value) return;

#if B_DEBUG
            Debug.Log( $"Context {context.name} changed scene handle from {previousScene.Value} to {sceneHandle.Value}" );
#endif

            // add
            if (!_sceneContexts.TryGetValue( sceneHandle, out var list ))
                _sceneContexts.Add( sceneHandle, list = new( 8 ) );
            list.Add( context );
            // remove
            list = _sceneContexts[previousScene];
            list.Remove( context );
            if (list.Count == 0) _sceneContexts.Remove( previousScene );

            UpdateAllRootContextsAndTopmostScene();
        }

#if B_DEBUG
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        static string CreateStringListOfAllContexts() =>
            $"[{string.Join( ", ", _sceneContexts.SelectMany( s => s.Value ).Select( c => c ? $"{c.name}({c.gameObject.scene.name})" : "null" ) )}]";
#endif

        /// <summary>
        /// Updates <see cref="_sceneContexts"/>'s roots, <see cref="_groupedContexts"/>'s roots and
        /// <see cref="_topMostScene"/>.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static void UpdateAllRootContextsAndTopmostScene() {
            if (_sceneContexts.Count == 0) {
                _topMostScene = default;
                return;
            }
            // will be needed 
            var stack = new Stack<Transform>( 32 );
            
            // scene roots
            int rootOrder;
            foreach (var contexts in _sceneContexts.Values) {
#if B_DEBUG
                bool changed = false;
#endif
                rootOrder = CalculateHierarchyOrder( contexts.GetRoot().transform, stack );
                for (int i = 0; i < contexts.Count; i++) {
                    var order = CalculateHierarchyOrder( contexts[i].transform, stack );
                    if (order < rootOrder) {
                        rootOrder = order;
                        contexts.RootIndex = i;
#if B_DEBUG
                        changed = true;
#endif
                    }
                }
                
#if B_DEBUG
                if (changed)
                    Debug.Log( $"Root of scene '{contexts[0].gameObject.scene.name}' changed: {contexts.GetRoot().name}({contexts.GetRoot().gameObject.scene.name})" );
#endif
            }
            
            // group roots (and topmost scene at the same time)
            Dictionary<Scene, int> sceneOrder = new( SceneManager.sceneCount );
            bool foundTopmostScene = false;
            for (int i = 0; i < SceneManager.sceneCount; i++) {
                sceneOrder[SceneManager.GetSceneAt( i )] = i;
                
                // resolving topmost scene right here
                var scene = SceneManager.GetSceneAt( i );
                var sceneHandle = new SceneHandle( scene );
                if (!foundTopmostScene && _sceneContexts.ContainsKey( sceneHandle )) {
#if B_DEBUG
                    if (!_topMostScene.Equals( sceneHandle )) 
                        Debug.Log( $"Topmost scene changed: {scene.name}" );
#endif
                    _topMostScene = sceneHandle;
                    foundTopmostScene = true;
                }
            }
            
            foreach (var contexts in _groupedContexts.Values) {
                const int SCENE_BENEFIT = 1_000_000;
                rootOrder = CalculateHierarchyOrder( contexts.GetRoot().transform, stack ) * sceneOrder[contexts.GetRoot().gameObject.scene] * SCENE_BENEFIT;
#if B_DEBUG
                bool changed = false;   
#endif
                for (int i = 0; i < contexts.Count; i++) {
                    var order = CalculateHierarchyOrder( contexts[i].transform, stack ) * sceneOrder[contexts[i].gameObject.scene] * SCENE_BENEFIT;
                    if (order < rootOrder) {
                        rootOrder = order;
                        contexts.RootIndex = i;
#if B_DEBUG
                        changed = true;
#endif
                    }
                }
#if B_DEBUG
                if (changed)
                    Debug.Log( $"Root of group '{contexts[0].Group}' changed: {contexts.GetRoot().name}({contexts.GetRoot().gameObject.scene.name})" );
#endif
            }
        }

        /// <summary>
        /// checks whether or not the <see cref="context"/> is compatible with the given <see cref="groupNumber"/>
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        static bool isCorrectGroup(BContext context, ushort groupNumber) => groupNumber == 0 || groupNumber == context.Group;
        
        /// <summary>
        /// Returns the index of which the transform will show up in hierarchy if everything is expanded. <para/>
        /// the <see cref="stack"/> has to be empty but initialized.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        static int CalculateHierarchyOrder(Transform transform, Stack<Transform> stack) {
            do {
                stack.Push( transform );
                transform = transform.parent;
            } while (transform is not null);

            int order = 0;
            while (stack.Count > 0) 
                order += stack.Pop().GetSiblingIndex() * 100;

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
        
        /// <inheritdoc cref="TryGetDependency{T}(UnityEngine.Transform,out T,ushort)"/>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool TryGetDependency<T>(this Component component, out T result, ushort groupNumber = 0) => 
            TryGetDependency( component.transform, out result, groupNumber );
        
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

#endregion

#endregion
    }


    internal readonly struct SceneHandle : IEquatable<SceneHandle> {
        public readonly int Value;
        public SceneHandle(Scene scene) => Value = scene.handle;
        
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public override int GetHashCode() => Value;
        
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public bool Equals(SceneHandle other) => Value == other.Value;
    }


    struct TransformTypeTuple : IEqualityComparer<TransformTypeTuple> {
        public int transformHash;
        public Type type;
        
        public TransformTypeTuple(Transform transform, Type type) {
            transformHash = transform.GetHashCode();
            this.type = type;
        }

        public bool Equals(TransformTypeTuple x, TransformTypeTuple y) => x.transformHash == y.transformHash && ReferenceEquals( x.type, y.type );
        public int GetHashCode(TransformTypeTuple obj) {
            var hash = new HashCode();
            hash.Add( obj.transformHash.GetHashCode() );
            hash.Add( obj.type.GetHashCode() );
            return hash.ToHashCode();
        }

        public override string ToString() => $"({transformHash}, {type})";
    }
}