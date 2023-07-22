using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Binject {
    
    /// <summary>
    /// Types implementing this will be responsible for saving a <see cref="ValueType"/> data inside and provide
    /// external access to it. This interface makes sure a <see cref="System.Collections.Generic.List{T}"/> can use any
    /// structs.
    /// </summary>
    internal interface IValueHolder {
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public Type GetValueType();
        
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public object BoxAndGetValue();
        
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void BoxAndSetValue(object value);
    }

    /// <summary>
    /// Any data stored in this will be boxed, but it's useful for showing the data inside in Unity Editor.
    /// </summary>
    [Serializable]
    internal class BoxedValueHolder : IValueHolder {
        [SerializeReference] public object Value;

        public BoxedValueHolder(object value) => Value = value;
        public Type GetValueType() => Value.GetType();
        public object BoxAndGetValue() => Value;
        public void BoxAndSetValue(object value) => Value = value;
    }

    /// <summary>
    /// This will store real data and provides direct access to it without boxing.
    /// </summary>
    [Serializable]
    internal class RealValueHolder<T> : IValueHolder {
        public T Value;
        public RealValueHolder(T value) => Value = value;
        public Type GetValueType() => typeof(T);
        public object BoxAndGetValue() => Value;
        public void BoxAndSetValue(object value) => Value = (T)value;
    }
}
