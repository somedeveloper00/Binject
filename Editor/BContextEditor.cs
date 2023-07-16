using System;
using System.Linq;
using System.Reflection;
using Binject;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BinjectEditor {
    [CustomEditor( typeof(BContext) )]
    internal class BContextEditor : Editor {

        ReorderableList _list;
        BContext _context;
        SerializedProperty dependenciesProp;
        static GUIStyle _headerStyle;

        void OnEnable() {
            _context = (BContext)target;
            dependenciesProp = serializedObject.FindProperty( nameof(BContext.dependencies) );

            _list = new ReorderableList( serializedObject, dependenciesProp, 
                draggable: true, 
                displayHeader: true, 
                displayAddButton: AllDependencyTypes.Length > 0, 
                displayRemoveButton: AllDependencyTypes.Length > 0 );

            _list.headerHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            _list.drawHeaderCallback += rect => {
                GUI.Label( rect, dependenciesProp.displayName, _headerStyle );
            };

            _list.elementHeightCallback += index => {
                var prop = dependenciesProp.GetArrayElementAtIndex( index );
                var headerHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                if (!prop.isExpanded) return headerHeight;
                
                var hasCustomEditor = HasCustomEditor( prop );
                if (hasCustomEditor) {
                    return headerHeight + EditorGUI.GetPropertyHeight( prop, true );
                }
                // else
                float bodyHeight = 0;
                int d = prop.depth;
                prop.Next( true );
                do {
                    if (prop.depth == d) break;
                    bodyHeight += EditorGUI.GetPropertyHeight( prop, true ) + EditorGUIUtility.standardVerticalSpacing;
                } while (prop.Next( false ));

                return headerHeight + bodyHeight;
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
                    bool hasCustomEditor = HasCustomEditor( prop );
                    if (hasCustomEditor) {
                        EditorGUI.PropertyField( rect, prop, true );
                    } else {
                        int d = prop.depth;
                        prop.Next( true );
                        do {
                            if (prop.depth == d) break;
                            EditorGUI.PropertyField( rect, prop, true );
                            rect.y += EditorGUI.GetPropertyHeight( prop, true ) + EditorGUIUtility.standardVerticalSpacing;
                        } while (prop.Next( false ));
                    }
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

            bool HasCustomEditor(SerializedProperty prop) {
                return prop.managedReferenceValue is IBHasCustomDrawer;
            }
        }

        public override void OnInspectorGUI() {
            InitStylesIfNotAlready();
            serializedObject.Update();
            _list.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        static void InitStylesIfNotAlready() {
            if (_headerStyle == null) {
                _headerStyle = new GUIStyle( EditorStyles.label );
                _headerStyle.alignment = TextAnchor.MiddleCenter;
            }
        }

        public static Type[] AllDependencyTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany( assembly => assembly.GetTypes()
                .Where( type => type.GetInterfaces().Contains( typeof(IBDependency) )
                                && (type.GetCustomAttribute( typeof(SerializableAttribute) ) != null || type.IsSubclassOf( typeof(Object) ))
                ) ).ToArray();
        
        public static GUIContent[] AllDependencyTypeNames = AllDependencyTypes.Select( type => new GUIContent( type.FullName.Replace( '.', '/' ) ) ).ToArray();
    }
}