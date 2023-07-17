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
    [DisallowMultipleComponent]
    [AddComponentMenu( "Binject/Binject Context" )]
    [DefaultExecutionOrder( -10 )]
    public sealed class BContext : MonoBehaviour {
        
        [Tooltip( "List of injectable non Unity Object data as dependency." )]
        [SerializeReference] internal List<IBDependency> dataDependencies = new( 8 );
        
        [Tooltip( "List of injectable Unity Objects as dependency." )]
        [SerializeField] internal List<UnityEngine.Object> objectDependencies = new( 8 );
        
        readonly HashSet<Type> _dependencyTypes = new( 16 );

        bool _added;

        void Awake() {
            AddAllDependencyTypes( true );
            BinjectManager.AddContext( this );
            _added = true;
        }

#if UNITY_EDITOR
        void OnValidate() {
            if (Application.isPlaying) return;
            StringBuilder sb = new( 128 );
            for (int i = 0; i < dataDependencies.Count; i++)
                if (dataDependencies[i] == null) {
                    sb.AppendLine( $"    - Data at {i}: was null" );
                    dataDependencies.RemoveAt( i-- );
                }

            // delete duplicates
            for (int i = 0; i < dataDependencies.Count - 1; i++)
            for (int j = i + 1; j < dataDependencies.Count; j++)
                if (dataDependencies[i].GetType() == dataDependencies[j].GetType()) {
                    sb.AppendLine( $"    - Data at {j}: duplicate of {i}" );
                    dataDependencies.RemoveAt( j-- );
                }
            for (int i = 0; i < objectDependencies.Count - 1; i++)
            for (int j = i + 1; j < objectDependencies.Count; j++)
                if (objectDependencies[i].GetType() == objectDependencies[j].GetType()) {
                    sb.AppendLine( $"    - Object at {j}: duplicate of {i}" );
                    objectDependencies.RemoveAt( j-- );
                }

            if (sb.Length > 0) 
                Debug.LogWarning( $"Binject Context of {name} removed some dependencies:\n{sb}" );
        }

#endif

        void OnEnable() {
            if (!_added) BinjectManager.AddContext( this );
            _added = true;
        }

        void OnDisable() {
            if (_added) BinjectManager.RemoveContext( this );
            _added = false;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        void AddAllDependencyTypes(bool clear) {
            if (clear) _dependencyTypes.Clear();
            for (int i = 0; i < dataDependencies.Count; i++)
                _dependencyTypes.Add( dataDependencies[i].GetType() );
            for (int i = 0; i < objectDependencies.Count; i++) 
                _dependencyTypes.Add( objectDependencies[i].GetType() );
        }

        /// <summary>
        /// Binds a dependency to this context. If one with the same type already exists, the new one will override
        /// the old one.
        /// </summary>
        public void Bind(IBDependency dependency) {
            if (_dependencyTypes.Add( dependency.GetType() )) {
                // new type
                if (dependency is UnityEngine.Object obj)
                    objectDependencies.Add( obj );
                else
                    dataDependencies.Add( dependency );
            }
            else {
                // override previous of same type
                if (dependency is UnityEngine.Object obj) {
                    for (int i = 0; i < objectDependencies.Count; i++) {
                        if (objectDependencies[i].GetType() == dependency.GetType()) {
                            objectDependencies[i] = obj;
                            break;
                        }
                    }
                } else {
                    for (int i = 0; i < dataDependencies.Count; i++) {
                        if (dataDependencies[i].GetType() == dependency.GetType()) {
                            dataDependencies[i] = dependency;
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
                    for (int i = 0; i < objectDependencies.Count; i++) {
                        if (objectDependencies[i].GetType() == typeof(T)) {
                            objectDependencies.RemoveAt( i );
                            return;
                        }
                    }
                } else {
                    for (int i = 0; i < dataDependencies.Count; i++) {
                        if (dataDependencies[i].GetType() == typeof(T)) {
                            dataDependencies.RemoveAt( i );
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
                    for (int i = 0; i < objectDependencies.Count; i++)
                        if (objectDependencies[i].GetType() == typeof(T))
                            return (T)(IBDependency)objectDependencies[i];
                } else {
                    for (int i = 0; i < dataDependencies.Count; i++)
                        if (dataDependencies[i].GetType() == typeof(T))
                            return (T)dataDependencies[i];
                }
            }
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
                for (int i = 0; i < objectDependencies.Count; i++)
                    if (objectDependencies[i].GetType() == typeof(T))
                        return (T)(IBDependency)objectDependencies[i];
            } else {
                for (int i = 0; i < dataDependencies.Count; i++)
                    if (dataDependencies[i].GetType() == typeof(T))
                        return (T)dataDependencies[i];
            }

            return default;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        static bool IsUnityObjectType(Type type) => type.IsSubclassOf( typeof(UnityEngine.Object) );
    }
}