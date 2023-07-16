using System;
using System.Linq;
using System.Reflection;
using Binject;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BinjectEditor {
    [CustomEditor( typeof(BContext) )]
    internal class BContextEditor : Editor {

        ReorderableList _list;
        BContext _context;
        SerializedProperty dependenciesProp;

        void OnEnable() {
            _context = (BContext)target;
            dependenciesProp = serializedObject.FindProperty( nameof(BContext.dependencies) );
            
            _list = new ReorderableList( serializedObject, dependenciesProp, 
                draggable: true, 
                displayHeader: false, 
                displayAddButton: AllDependencyTypes.Length > 0, 
                displayRemoveButton: AllDependencyTypes.Length > 0 );

            _list.elementHeightCallback += index => {
                var prop = dependenciesProp.GetArrayElementAtIndex( index );
                float h = EditorGUIUtility.singleLineHeight;
                if (prop.isExpanded)
                    h = EditorGUIUtility.standardVerticalSpacing * 2 +
                        EditorGUI.GetPropertyHeight( dependenciesProp.GetArrayElementAtIndex( index ) );
                return h;
            };
            _list.drawElementCallback += (rect, index, active, focused) => {
                var prop = dependenciesProp.GetArrayElementAtIndex( index );
                // check if has custom editor
                rect.height = EditorGUIUtility.singleLineHeight;
                prop.isExpanded = EditorGUI.BeginFoldoutHeaderGroup( 
                    position: rect, 
                    foldout: prop.isExpanded,
                    content: prop.managedReferenceValue.GetType().ToString(),
                    menuAction: rect => {
                        var menu = new GenericMenu();
                        menu.AddItem( new GUIContent( "Remove" ), false, () => {
                            dependenciesProp.DeleteArrayElementAtIndex( index );
                            serializedObject.ApplyModifiedProperties();
                        } );
                        menu.DropDown( rect );
                    }
                );
                EditorGUI.EndFoldoutHeaderGroup();
                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                if (prop.isExpanded) {
                    prop.Next( true );
                    EditorGUI.PropertyField( rect, prop, true );
                }
            };
            
            _list.onAddDropdownCallback += (_, _) => {
                var menu = new GenericMenu();
                for (int i = 0; i < AllDependencyTypes.Length; i++) {
                    int index = i;
                    bool typeAlreadyIncluded = _context.dependencies.Any( d => d.GetType() == AllDependencyTypes[index] );
                    if (typeAlreadyIncluded) {
                        menu.AddDisabledItem( AllDependencyTypeNames[i], false );
                    }
                    else {
                        menu.AddItem( AllDependencyTypeNames[i], false, () => {
                            var dependency = Activator.CreateInstance( AllDependencyTypes[index] ) as IBDependency;
                            dependenciesProp.InsertArrayElementAtIndex( dependenciesProp.arraySize );
                            dependenciesProp.GetArrayElementAtIndex( dependenciesProp.arraySize - 1 )
                                .managedReferenceValue = dependency;
                            serializedObject.ApplyModifiedProperties();
                        } );
                    }
                }
                menu.ShowAsContext();
            };
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            _list.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        public static Type[] AllDependencyTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany( assembly => assembly.GetTypes()
                .Where( type => type.GetInterfaces().Contains( typeof(IBDependency) )
                                && type.IsValueType
                                && type.GetCustomAttribute( typeof(SerializableAttribute) ) != null
                ) ).ToArray();
        
        public static GUIContent[] AllDependencyTypeNames = AllDependencyTypes.Select( type => new GUIContent( type.FullName.Replace( '.', '/' ) ) ).ToArray();
    }
}