using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace Binject {
    /// <summary>
    /// A container for dependencies. You can use contexts to group dependencies.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder( -10 )]
    [AddComponentMenu( "Binject/Binject Context" )]
    public sealed class BContext : MonoBehaviour {

        [Tooltip( "used for when you want to use a specific context but don't have access to it directly; you can find " +
                  "it using it's Group number." )]
        [SerializeField] internal ushort Group;
        
        [Tooltip( "List of injectable non Unity Object data as dependency." )]
        [SerializeReference] internal List<IBDependency> DataDependencies = new( 8 );
         
        [Tooltip( "List of injectable Unity Objects as dependency." )]
        [SerializeField] internal List<UnityEngine.Object> ObjectDependencies = new( 8 );
        
        readonly HashSet<Type> _dependencyTypes = new( 16 );

        [NonSerialized] bool _initialized; 

#if UNITY_EDITOR
        void OnValidate() {
            if (Application.isPlaying) return;
            
            // fix broken lists
            StringBuilder sb = new( 128 );
            for (int i = 0; i < DataDependencies.Count; i++)
                if (DataDependencies[i] == null) {
                    sb.AppendLine( $"    - Data at {i}: was null" );
                    DataDependencies.RemoveAt( i-- );
                }

            // delete duplicates
            for (int i = 0; i < DataDependencies.Count - 1; i++)
            for (int j = i + 1; j < DataDependencies.Count; j++)
                if (DataDependencies[i].GetType() == DataDependencies[j].GetType()) {
                    sb.AppendLine( $"    - Data at {j}: duplicate of {i}" );
                    DataDependencies.RemoveAt( j-- );
                }
            for (int i = 0; i < ObjectDependencies.Count - 1; i++)
            for (int j = i + 1; j < ObjectDependencies.Count; j++)
                if (ObjectDependencies[i].GetType() == ObjectDependencies[j].GetType()) {
                    sb.AppendLine( $"    - Object at {j}: duplicate of {i}" );
                    ObjectDependencies.RemoveAt( j-- );
                }

            if (sb.Length > 0) 
                Debug.LogWarning( $"Binject Context of {name} removed some dependencies:\n{sb}" );
            
            // support in-editor injection
            if (!_initialized) { 
                SyncAllDependencyTypes( true );
                BinjectManager.AddContext( this );
                _initialized = true;
            }
        }

#endif

        void Awake() {
            if (!_initialized) {
                SyncAllDependencyTypes( true );
                BinjectManager.AddContext( this );
                _initialized = true;
            }
        }

        void OnEnable() {
            if (!_initialized) BinjectManager.AddContext( this );
            _initialized = true;
        }

        void OnDisable() {
            if (_initialized) BinjectManager.RemoveContext( this );
            _initialized = false;
        }

        void OnDestroy() {
            if (_initialized) BinjectManager.RemoveContext( this );
        }


        /// <summary>
        /// Binds a dependency to this context. If one with the same type already exists, the new one will override
        /// the old one.
        /// </summary>
        public void Bind(IBDependency dependency) {
            if (_dependencyTypes.Add( dependency.GetType() )) {
                // new type
                if (dependency is UnityEngine.Object obj)
                    ObjectDependencies.Add( obj );
                else
                    DataDependencies.Add( dependency );
            }
            else {
                // override previous of same type
                if (dependency is UnityEngine.Object obj) {
                    for (int i = 0; i < ObjectDependencies.Count; i++) {
                        if (ObjectDependencies[i].GetType() == dependency.GetType()) {
                            ObjectDependencies[i] = obj;
                            break;
                        }
                    }
                } else {
                    for (int i = 0; i < DataDependencies.Count; i++) {
                        if (DataDependencies[i].GetType() == dependency.GetType()) {
                            DataDependencies[i] = dependency;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Binds a dependency from this context.
        /// </summary>
        public void Unbind<T>() where T : IBDependency {
            if (_dependencyTypes.Remove( typeof(T) )) {
                if (IsUnityObjectType( typeof(T))) {
                    for (int i = 0; i < ObjectDependencies.Count; i++) {
                        if (ObjectDependencies[i].GetType() == typeof(T)) {
                            ObjectDependencies.RemoveAt( i );
                            return;
                        }
                    }
                } else {
                    for (int i = 0; i < DataDependencies.Count; i++) {
                        if (DataDependencies[i].GetType() == typeof(T)) {
                            DataDependencies.RemoveAt( i );
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if this context has a dependency of type <see cref="T"/>.
        /// </summary>
        public bool HasDependency<T>() where T : IBDependency => _dependencyTypes.Contains( typeof(T) );

        /// <summary>
        /// Returns the dependency of type <see cref="T"/> if it exists, otherwise returns default.
        /// </summary>
        public T GetDependency<T>() where T : IBDependency {
            if (HasDependency<T>()) {
                if (IsUnityObjectType( typeof(T))) {
                    for (int i = 0; i < ObjectDependencies.Count; i++)
                        if (ObjectDependencies[i].GetType() == typeof(T))
                            return (T)(IBDependency)ObjectDependencies[i];
                } else {
                    for (int i = 0; i < DataDependencies.Count; i++)
                        if (DataDependencies[i].GetType() == typeof(T))
                            return (T)DataDependencies[i];
                }
            }

            Debug.LogWarning( $"No dependency of type {typeof(T).FullName} found. returning default/null." );
            return default;
        }

        /// <summary>
        /// Without checking if it exists, returns the dependency of type <see cref="T"/>. If not found, returns default.
        /// Slightly faster than <see cref="GetDependency{T}"/> if you already know that the dependency exists, but
        /// using <see cref="HasDependency{T}"/> and this method together is slightly slower than a single
        /// <see cref="GetDependency{T}"/> call.
        /// </summary>
        public T GetDependencyNoCheck<T>() where T : IBDependency {
            if (IsUnityObjectType( typeof(T))) {
                for (int i = 0; i < ObjectDependencies.Count; i++)
                    if (ObjectDependencies[i].GetType() == typeof(T))
                        return (T)(IBDependency)ObjectDependencies[i];
            } else {
                for (int i = 0; i < DataDependencies.Count; i++)
                    if (DataDependencies[i].GetType() == typeof(T))
                        return (T)DataDependencies[i];
            }

            Debug.LogWarning( $"No dependency of type {typeof(T).FullName} found. returning default/null." );
            return default;
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal void SyncAllDependencyTypes(bool clear) {
            if (clear) _dependencyTypes.Clear();
            for (int i = 0; i < DataDependencies.Count; i++)
                _dependencyTypes.Add( DataDependencies[i].GetType() );
            for (int i = 0; i < ObjectDependencies.Count; i++) 
                _dependencyTypes.Add( ObjectDependencies[i].GetType() );
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        static bool IsUnityObjectType(Type type) => type.IsSubclassOf( typeof(UnityEngine.Object) );
    }
}