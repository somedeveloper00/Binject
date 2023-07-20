using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

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

        [FormerlySerializedAs( "ObjectDependencies" )]
        [Tooltip( "List of injectable Unity Objects as dependency." )]
        [SerializeField] internal List<UnityEngine.Object> UnityObjectDependencies = new( 8 );

        [Tooltip( "List of injectable non Unity Object classes as dependency." )]
        [SerializeReference] internal List<object> ClassDependencies = new( 8 );

        [FormerlySerializedAs( "StructDependencies" )]
        [Tooltip( "List of injectable value types (struct) as dependency.\n" +
                  "Elements added from inspector will be boxed." )]
        [SerializeReference] internal List<BoxedStructHolder> StructDependencies_Serializaded = new( 8 );
        
        
        [NonSerialized] readonly List<StructHolder> _structDependencies = new( 8 );
        [NonSerialized] readonly HashSet<Type> _dependencyTypes = new( 16 );
        [NonSerialized] bool _initialized;

#if UNITY_EDITOR
        void OnValidate() {
            if (Application.isPlaying) return;

            // fix broken lists
            StringBuilder sb = new( 128 );
            for (int i = 0; i < UnityObjectDependencies.Count; i++)
                if (UnityObjectDependencies[i] == null) {
                    sb.AppendLine( $"    - Unity Object at {i}: was null" );
                    UnityObjectDependencies.RemoveAt( i-- );
                }
            for (int i = 0; i < ClassDependencies.Count; i++)
                if (ClassDependencies[i] == null) {
                    sb.AppendLine( $"    - class at {i}: was null" );
                    ClassDependencies.RemoveAt( i-- );
                }
            for (int i = 0; i < _structDependencies.Count; i++)
                if (_structDependencies[i].BoxAndGetValue() == null) {
                    sb.AppendLine( $"    - struct at {i}: was null" );
                    _structDependencies.RemoveAt( i-- );
                }

            // delete duplicates
            for (int i = 0; i < UnityObjectDependencies.Count - 1; i++)
            for (int j = i + 1; j < UnityObjectDependencies.Count; j++)
                if (UnityObjectDependencies[i].GetType() == UnityObjectDependencies[j].GetType()) {
                    sb.AppendLine( $"    - Unity Object at [{j}]: duplicate of [{i}]" );
                    UnityObjectDependencies.RemoveAt( j-- );
                }
            for (int i = 0; i < ClassDependencies.Count - 1; i++)
            for (int j = i + 1; j < ClassDependencies.Count; j++)
                if (ClassDependencies[i].GetType() == ClassDependencies[j].GetType()) {
                    sb.AppendLine( $"    - class at [{j}]: duplicate of [{i}]" );
                    ClassDependencies.RemoveAt( j-- );
                }
            for (int i = 0; i < _structDependencies.Count - 1; i++)
            for (int j = i + 1; j < _structDependencies.Count; j++)
                if (_structDependencies[i].GetValueType() == _structDependencies[j].GetValueType()) {
                    sb.AppendLine( $"    - struct at [{j}]: duplicate of [{i}]" );
                    _structDependencies.RemoveAt( j-- );
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
            _structDependencies.AddRange( StructDependencies_Serializaded );
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
        public void Bind<T>(T dependency) {
            if (_dependencyTypes.Add( dependency.GetType() )) {
                // new type
                if (typeof(T).IsValueType) {
                    _structDependencies.Add( new BoxedStructHolder( dependency ) );
#if UNITY_EDITOR
                    StructDependencies_Serializaded.Add( new BoxedStructHolder( dependency ) );
#endif
                }
                else if (IsUnityObjectType( dependency.GetType() ))
                    UnityObjectDependencies.Add( dependency as UnityEngine.Object );
                else
                    ClassDependencies.Add( dependency );
            } else {
                // override previous of same type
                if (typeof(T).IsValueType) {
                    for (int i = 0; i < _structDependencies.Count; i++) {
                        if (_structDependencies[i].GetValueType() == dependency.GetType()) {
                            _structDependencies[i].BoxAndSetValue( dependency );
                            break;
                        }
                    }
                }
                else if (IsUnityObjectType( dependency.GetType() )) {
                    for (int i = 0; i < UnityObjectDependencies.Count; i++) {
                        if (UnityObjectDependencies[i].GetType() == dependency.GetType()) {
                            UnityObjectDependencies[i] = dependency as UnityEngine.Object;
                            break;
                        }
                    }
                } else {
                    for (int i = 0; i < ClassDependencies.Count; i++) {
                        if (ClassDependencies[i].GetType() == dependency.GetType()) {
                            ClassDependencies[i] = dependency;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Binds a dependency to this context. If one with the same type already exists, the new one will override
        /// the old one.<para/>
        /// Use this instead of <see cref="Bind{T}"/> for `struct`s to avoid boxing and get better
        /// performance.
        /// </summary>
        public void BindStruct<T>(T dependency) where T : struct {
            if (_dependencyTypes.Add( typeof(T) )) {
                // new type
                _structDependencies.Add( new RealStructHolder<T>( dependency ) );
#if UNITY_EDITOR
                StructDependencies_Serializaded.Add( new BoxedStructHolder( dependency ) );
#endif
            } else {
                // override previous of same type
                for (int i = 0; i < _structDependencies.Count; i++) {
                    if (_structDependencies[i] is RealStructHolder<T> sd) {
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
#if UNITY_EDITOR
                            StructDependencies_Serializaded.RemoveAt( i );
#endif
                            return;
                        }
                    }
                }
                if (IsUnityObjectType( typeof(T) )) {
                    for (int i = 0; i < UnityObjectDependencies.Count; i++) {
                        if (UnityObjectDependencies[i].GetType() == typeof(T)) {
                            UnityObjectDependencies.RemoveAt( i );
                            return;
                        }
                    }
                } else {
                    for (int i = 0; i < ClassDependencies.Count; i++) {
                        if (ClassDependencies[i].GetType() == typeof(T)) {
                            ClassDependencies.RemoveAt( i );
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
        /// </summary>
        public T GetDependency<T>() {
            if (HasDependency<T>())
                if (GetDependency_Any( out T result ))
                    return result;
            Debug.LogWarning( $"No dependency of type {typeof(T).FullName} found. returning default/null." );
            return default;
        }

        /// <summary>
        /// Returns the dependency of type <see cref="T"/> if it exists, otherwise returns <c>default</c>.<para/>
        /// Use this instead of <see cref="GetDependency{T}"/> for `struct`s to avoid boxing and get better performance.
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
        /// </summary>
        public T GetDependencyNoCheck<T>() {
            if (GetDependency_Any<T>( out var result ))
                return result;
            Debug.LogWarning( $"No dependency of type {typeof(T).FullName} found. returning default/null." );
            return default;
        }

        /// <summary>
        /// Without checking if it exists, returns the dependency of type <see cref="T"/>. If not found, returns default.
        /// Slightly faster than <see cref="GetDependency{T}"/> if you already know that the dependency exists, but
        /// using <see cref="HasDependency{T}"/> and this method together is slightly slower than a single
        /// <see cref="GetDependency{T}"/> call. <para/>
        /// Use this instead of <see cref="GetDependency{T}"/> for `struct`s to avoid boxing and get better performance.
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
            for (int i = 0; i < ClassDependencies.Count; i++)
                _dependencyTypes.Add( ClassDependencies[i].GetType() );
            for (int i = 0; i < UnityObjectDependencies.Count; i++) 
                _dependencyTypes.Add( UnityObjectDependencies[i].GetType() );
            for (int i = 0; i < _structDependencies.Count; i++)
                _dependencyTypes.Add( _structDependencies[i].GetValueType() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        bool GetDependency_Any<T>(out T result) {
            if (typeof(T).IsValueType) {
                for (int i = 0; i < _structDependencies.Count; i++)
                    if (_structDependencies[i].GetValueType() == typeof(T)) {
                        result = (T)_structDependencies[i].BoxAndGetValue();
                        return true;
                    }
                
            }
            if (IsUnityObjectType( typeof(T) )) {
                for (int i = 0; i < UnityObjectDependencies.Count; i++)
                    if (UnityObjectDependencies[i] is T obj) {
                        result = obj;
                        return true;
                    }
            } else {
                for (int i = 0; i < ClassDependencies.Count; i++)
                    if (ClassDependencies[i] is T dat) {
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
                if (_structDependencies[i] is RealStructHolder<T> sd) {
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