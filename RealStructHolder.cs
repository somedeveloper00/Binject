using System;
using System.Runtime.CompilerServices;

namespace Binject {
    internal abstract class StructHolder {
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public abstract Type GetValueType();
        
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public abstract object BoxAndGetValue();
        
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public abstract void BoxAndSetValue(object value);
    }


    internal sealed class BoxedStructHolder : StructHolder {
        public object Value;

        public BoxedStructHolder(object value) => Value = value;
        public override Type GetValueType() => Value.GetType();
        public override object BoxAndGetValue() => Value;
        public override void BoxAndSetValue(object value) => Value = value;
    }
    
    internal sealed class RealStructHolder<T> : StructHolder where T : struct {
        public T Value;
        public RealStructHolder(T value) => Value = value;
        public override Type GetValueType() => typeof(T);
        public override object BoxAndGetValue() => Value;
        public override void BoxAndSetValue(object value) => Value = (T)value;
    }
 
}