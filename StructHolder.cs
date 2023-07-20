using System;

namespace Binject {
    internal abstract class StructHolder {
        public abstract Type GetValueType();
    }
    internal sealed class StructHolder<T> : StructHolder where T : struct {
        public T Value;
        public StructHolder(T value) => Value = value;
        public override Type GetValueType() => typeof(T);
    }
 
}
