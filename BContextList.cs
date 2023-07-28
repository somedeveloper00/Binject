using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Binject {
    /// <summary>
    /// A list specifically designed for <see cref="BinjectManager"/>. it holds a <see cref="RootIndex"/> and sorts
    /// contexts by their use count.
    /// </summary>
    public class BContextList : List<BContext> {
        public int RootIndex;
        List<int> _points;
        HashSet<Transform> _transforms;

        public BContextList(int capacity) : base( capacity ) => Init();

        void Init() {
            _points = new( Capacity );
            _transforms = new( Capacity, new ObjectReferenceEquality<Transform>() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public BContext GetRoot() => this[RootIndex];

        public new void Add(BContext context) {
            base.Add( context );
            _points.Add( 0 );
            _transforms.Add( context.transform );
        }

        public new bool Remove(BContext context) {
            _points.RemoveAt( IndexOf( context ) );
            _transforms.Remove( context.transform );
            return base.Remove( context );
        }

        public bool ContainsTransform(Transform transform) => _transforms.Contains( transform );

        public void AddPoint(int index) {
            if (index == 0) return;
            _points[index]++;
            while (index > 0 && _points[index] > _points[index - 1]) {
                (this[index], this[index - 1]) = (this[index - 1], this[index]);
                (_points[index], _points[index - 1]) = (_points[index - 1], _points[index]);
                index--;
            }
        }
    }


    class ObjectReferenceEquality<T> : IEqualityComparer<T> {
        public bool Equals(T x, T y) => ReferenceEquals( x, y );
        public int GetHashCode(T obj) => obj.GetHashCode();
    }
}