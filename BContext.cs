using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Binject {
    /// <summary>
    /// A container for dependencies. You can use contexts to group dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu( "Binject/Binject Context" )]
    [DefaultExecutionOrder( -10 )]
    public sealed class BContext : MonoBehaviour {
        [SerializeReference] 
        internal List<IBDependency> dependencies = new( 16 );
        readonly HashSet<Type> _dependencyTypes = new( 16 );

        bool _added;

        void Awake() {
            SyncDependencyTypes();
            BinjectManager.AddContext( this );
            _added = true;
        }

#if UNITY_EDITOR
        void OnValidate() {
            for (int i = 0; i < dependencies.Count; i++)
                if (dependencies[i] == null)
                    dependencies.RemoveAt( i-- );

            // delete duplicates
            for (int i = 0; i < dependencies.Count - 1; i++)
            for (int j = i + 1; j < dependencies.Count; j++)
                if (dependencies[i].GetType() == dependencies[j].GetType())
                    dependencies.RemoveAt( j-- );
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
        void SyncDependencyTypes() {
            for (int i = 0; i < dependencies.Count; i++)
                _dependencyTypes.Add( dependencies[i].GetType() );
        }

        /// <summary>
        /// Binds a dependency to this context. If one with the same type already exists, the new one will override
        /// the old one.
        /// </summary>
        public void Bind(IBDependency dependency) {
            if (_dependencyTypes.Add( dependency.GetType() )) {
                // new type
                dependencies.Add( dependency );
            }
            else {
                // override previous of same type
                for (int i = 0; i < dependencies.Count; i++) {
                    if (dependencies[i].GetType() == dependency.GetType()) {
                        dependencies[i] = dependency;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Binds a dependency from this context.
        /// </summary>
        public void Unbind<T>() where T : IBDependency {
            if (_dependencyTypes.Remove( typeof(T) )) {
                for (int i = 0; i < dependencies.Count; i++) {
                    if (dependencies[i].GetType() == typeof(T)) {
                        dependencies.RemoveAt( i );
                        return;
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
            if (HasDependency<T>())
                for (int i = 0; i < dependencies.Count; i++)
                    if (dependencies[i].GetType() == typeof(T))
                        return (T)dependencies[i];
            return default;
        }

        /// <summary>
        /// Without checking if it exists, returns the dependency of type <see cref="T"/>. If not found, returns default.
        /// Slightly faster than <see cref="GetDependency{T}"/> if you already know that the dependency exists, but
        /// using <see cref="HasDependency{T}"/> and this method together is slightly slower than a single
        /// <see cref="GetDependency{T}"/> call.
        /// </summary>
        public T GetDependencyNoCheck<T>() where T : IBDependency {
            for (int i = 0; i < dependencies.Count; i++)
                if (dependencies[i].GetType() == typeof(T))
                    return (T)dependencies[i];
            return default;
        }
    }
}