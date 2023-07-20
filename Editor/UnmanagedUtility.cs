using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BinjectEditor {
    
    public static class UnmanagedUtility {
        
        static Dictionary<Type, bool> cachedTypes = new( 64 );

        /// <summary>
        /// Detects whether or not a type is unmanaged
        /// </summary>
        public static bool IsUnManaged(this Type type) {
            bool result;
            if (cachedTypes.TryGetValue( type, out bool value )) return value;
            if (type.IsPrimitive || type.IsPointer || type.IsEnum) 
                result = true;
            else if (type.IsGenericType || !type.IsValueType) 
                result = false;
            else
                result = type
                    .GetFields( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance )
                    .All( x => x.FieldType.IsUnManaged() );
            cachedTypes.Add( type, result );
            return result;
        }
    }
}