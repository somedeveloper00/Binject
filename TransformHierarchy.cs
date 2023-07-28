using System.Collections.Generic;
using UnityEngine;

namespace Binject {
    public struct TransformHierarchy {
        List<Transform> _transforms;
        public BContext context;
        int current;

        public TransformHierarchy(Transform transform) {
            _transforms = new( 8 );
            _transforms.Add( transform );
            current = 0;
            context = null;
        }

        public bool TryWalkParent(out Transform parent) {
            current++;
            if (_transforms.Count <= current) {
                parent = _transforms[^1].parent;
                if (parent is null) return false;
                _transforms.Add( parent );
            }
            parent = _transforms[current];
            return true;
        }
        
        public void ResetWalk() => current = 0;
    }
}