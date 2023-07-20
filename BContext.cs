using System;
using System.Collections.Generic;
using System.Linq;
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
        [SerializeReference] internal List<object> DataDependencies = new( 8 );
         
        [Tooltip( "List of injectable Unity Objects as dependency." )]
        [SerializeField] internal List<UnityEngine.Object> ObjectDependencies = new( 8 );

        readonly List<StructHolder> _structDependencies = new( 8 );
        
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
                    sb.AppendLine( $"    - Data at [{j}]: duplicate of [{i}]" );
                    DataDependencies.RemoveAt( j-- );
                }
            for (int i = 0; i < ObjectDependencies.Count - 1; i++)
            for (int j = i + 1; j < ObjectDependencies.Count; j++)
                if (ObjectDependencies[i].GetType() == ObjectDependencies[j].GetType()) {
                    sb.AppendLine( $"    - Object at [{j}]: duplicate of [{i}]" );
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
        /// (for reference types only)
        /// </summary>
        public void Bind<T>(T dependency) where T : class {
            if (_dependencyTypes.Add( dependency.GetType() )) {
                // new type
                if (IsUnityObjectType( dependency.GetType() ))
                    ObjectDependencies.Add( dependency as UnityEngine.Object );
                else
                    DataDependencies.Add( dependency );
            } else {
                // override previous of same type
                if (IsUnityObjectType( dependency.GetType() )) {
                    for (int i = 0; i < ObjectDependencies.Count; i++) {
                        if (ObjectDependencies[i].GetType() == dependency.GetType()) {
                            ObjectDependencies[i] = dependency as UnityEngine.Object;
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
        /// Binds a dependency to this context. If one with the same type already exists, the new one will override
        /// the old one.
        /// (for value types only)
        /// </summary>
        public void BindStruct<T>(T dependency) where T : struct {
            if (_dependencyTypes.Add( dependency.GetType() )) {
                // new type
                _structDependencies.Add( new StructHolder<T>( dependency ) );
            } else {
                // override previous of same type
                for (int i = 0; i < _structDependencies.Count; i++) {
                    if (_structDependencies[i] is StructHolder<T> sd) {
                        sd.Value = dependency;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Unbinds a dependency from this context.
        /// </summary>
        void Unbind<T>() {
            if (_dependencyTypes.Remove( typeof(T) )) {
                if (typeof(T).IsValueType) {
                    for (int i = 0; i < _structDependencies.Count; i++) {
                        if (_structDependencies[i].GetValueType() == typeof(T)) {
                            _structDependencies.RemoveAt( i );
                            return;
                        }
                    }
                }
                if (IsUnityObjectType( typeof(T) )) {
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
        /// Checks if this context has a dependency of type <see cref="T"/>
        /// </summary>
        public bool HasDependency<T>() => _dependencyTypes.Contains( typeof(T) );

        /// <summary>
        /// Returns the dependency of type <see cref="T"/> if it exists, otherwise returns <c>default</c>.
        /// (for reference types only)
        /// </summary>
        public T GetDependency<T>() where T : class {
            if (HasDependency<T>())
                if (GetDependency_ReferenceType( out T result ))
                    return result;

            Debug.LogWarning( $"No dependency of type {typeof(T).FullName} found. returning default/null." );
            return default;
        }

        /// <summary>
        /// Returns the dependency of type <see cref="T"/> if it exists, otherwise returns <c>default</c>.
        /// (for value types only)
        /// </summary>
        public T GetDependencyStruct<T>() where T : struct {
            if (HasDependency<T>())
                if (GetDependency_ValueType<T>( out var result ))
                    return result;

            Debug.LogWarning( $"No dependency of type {typeof(T).FullName} found. returning default/null." );
            return default;
        }

        /// <summary>
        /// Without checking if it exists, returns the dependency of type <see cref="T"/>. If not found, returns default.
        /// Slightly faster than <see cref="GetDependency{T}"/> if you already know that the dependency exists, but
        /// using <see cref="HasDependency{T}"/> and this method together is slightly slower than a single
        /// <see cref="GetDependency{T}"/> call. 
        /// (for reference types only)
        /// </summary>
        public T GetDependencyNoCheck<T>() where T : class {
            if (GetDependency_ReferenceType<T>( out var result ))
                return result;
            Debug.LogWarning( $"No dependency of type {typeof(T).FullName} found. returning default/null." );
            return default;
        }

        /// <summary>
        /// Without checking if it exists, returns the dependency of type <see cref="T"/>. If not found, returns default.
        /// Slightly faster than <see cref="GetDependency{T}"/> if you already know that the dependency exists, but
        /// using <see cref="HasDependency{T}"/> and this method together is slightly slower than a single
        /// <see cref="GetDependency{T}"/> call. 
        /// (for value types only)
        /// </summary>
        public T GetDependencyStructNoCheck<T>() where T : struct {
            if (GetDependency_ValueType<T>( out var result ))
                return result;

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
            for (int i = 0; i < _structDependencies.Count; i++)
                _dependencyTypes.Add( _structDependencies[i].GetValueType() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        bool GetDependency_ReferenceType<T>(out T result) where T : class {
            if (IsUnityObjectType( typeof(T) )) {
                for (int i = 0; i < ObjectDependencies.Count; i++)
                    if (ObjectDependencies[i] is T obj) {
                        result = obj;
                        return true;
                    }
            } else {
                for (int i = 0; i < DataDependencies.Count; i++)
                    if (DataDependencies[i] is T dat) {
                        result = dat;
                        return true;
                    }
            }

            result = default;
            return false;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        bool GetDependency_ValueType<T>(out T result) where T : struct {
            for (int i = 0; i < _structDependencies.Count; i++)
                if (_structDependencies[i] is StructHolder<T> sd) {
                    result = sd.Value;
                    return true;
                }
            result = default;
            return false;
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        static bool IsUnityObjectType(Type type) => type.IsSubclassOf( typeof(UnityEngine.Object) );
    }
}