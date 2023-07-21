using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Binject {
    internal interface ValueHolder {
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public Type GetValueType();
        
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public object BoxAndGetValue();
        
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void BoxAndSetValue(object value);
    }

    [Serializable]
    internal class BoxedValueHolder : ValueHolder {
        [SerializeReference] public object Value;

        public BoxedValueHolder(object value) => Value = value;
        public Type GetValueType() => Value.GetType();
        public object BoxAndGetValue() => Value;
        public void BoxAndSetValue(object value) => Value = value;
    }

    internal interface ITest<T>{ }


    [Serializable]
    internal struct RealValueHolder<T> : ValueHolder {
        public T Value;
        public RealValueHolder(T value) => Value = value;
        public Type GetValueType() => typeof(T);
        public object BoxAndGetValue() => Value;
        public void BoxAndSetValue(object value) => Value = (T)value;
    }
}
