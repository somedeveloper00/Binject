using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Binject;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace BinjectEditor {
    [CustomEditor( typeof(BContext) )]
    internal class BContextEditor : Editor {
        SerializedProperty classDependenciesProp;
        SerializedProperty structDependenciesProp;
        SerializedProperty objectDependenciesProp;
        SerializedProperty groupProp;
        ReorderableList _classList;
        ReorderableList _structList;
        ReorderableList _objectList;
        bool _advancedFoldout;
        
        static GUIStyle _headerStyle;
        static GUIContent _objectListHeaderGuiContent;
        static GUIContent _classListHeaderGuiContent;
        static GUIContent _structListHeaderGuiContent;

        static TypeSelectDropDown _classTypeDropDown = new( new AdvancedDropdownState(),
            filter: type =>
                true
                && !type.IsValueType                                                                        // avoid boxing/unboxing
                && !type.IsSubclassOf( typeof(UnityEngine.Object) )                                         // Unity Objects won't SerializeReference
                && !type.IsAbstract && !type.ContainsGenericParameters &&                                   // exclude useless serializing classes
                !type.IsSubclassOf( typeof(Attribute) )
                && type.IsSerializable                                                                      // only serializable
                && type.Assembly.GetName().Name != "mscorlib"                                               // mscorlib is too low-level to inject
                && type.GetCustomAttribute( typeof(CompilerGeneratedAttribute) ) == null &&                 // no compiler-generated classes
                !type.FullName.Contains( '<' ) && !type.FullName.Contains( '>' ) &&
                !type.FullName.Contains( '+' )
                && !type.IsSubclassOf( typeof(Exception) )                                                  // no need to inject exceptions
                && !type.GetInterfaces().Any( inter => inter.IsSubclassOf( typeof(IDisposable) ) )          // no need to inject disposables
                && type.GetConstructors().Any( c => c.GetParameters().Length == 0 )                         // construct easily 
        );

        static TypeSelectDropDown _structTypeDropDown = new( new AdvancedDropdownState(),
            filter: type =>
                true
                && type.IsValueType && !type.IsEnum                                                         // only structs
                && !type.ContainsGenericParameters                                                          // exclude useless serializing types
                && type.IsSerializable                                                                      // only serializable
                && type.Assembly.GetName().Name != "mscorlib"                                               // mscorlib is too low-level to inject
                && type.GetCustomAttribute( typeof(CompilerGeneratedAttribute) ) == null &&                 // no compiler-generated types
                !type.FullName.Contains( '<' ) && !type.FullName.Contains( '>' ) &&
                !type.FullName.Contains( '+' )
                && !type.GetInterfaces().Any( inter => inter.IsSubclassOf( typeof(IDisposable) ) )          // no need to inject disposables
        );


        void SetupObjectList() {
            _objectList = new ReorderableList( serializedObject, objectDependenciesProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: !Application.isPlaying,
                displayRemoveButton: !Application.isPlaying );

            _objectList.headerHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            _objectList.drawHeaderCallback += rect => {
                _objectListHeaderGuiContent ??= new GUIContent(objectDependenciesProp.displayName, objectDependenciesProp.tooltip);
                GUI.Label( rect, _objectListHeaderGuiContent, _headerStyle );
            };

            _objectList.elementHeightCallback += index => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            _objectList.drawElementCallback += (rect, index, _, _) => {
                rect.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField( rect, objectDependenciesProp.GetArrayElementAtIndex( index ), GUIContent.none, true );
            };

            _objectList.onAddCallback += _ => {
                objectDependenciesProp.arraySize++;
                objectDependenciesProp.GetArrayElementAtIndex( objectDependenciesProp.arraySize - 1 )
                    .objectReferenceValue = null;
            };
        }

        void SetupClassList() {
            _classList = new ReorderableList( serializedObject, classDependenciesProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: !Application.isPlaying,
                displayRemoveButton: !Application.isPlaying );

            _classList.headerHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            _classList.drawHeaderCallback += rect => {
                _classListHeaderGuiContent ??= new GUIContent(classDependenciesProp.displayName, classDependenciesProp.tooltip);
                GUI.Label( rect, _classListHeaderGuiContent, _headerStyle );
            };

            _classList.elementHeightCallback += index => {
                var prop = classDependenciesProp.GetArrayElementAtIndex( index );
                var headerHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                if (!prop.isExpanded) return headerHeight;

                var hasCustomEditor = HasCustomEditor( prop );
                if (hasCustomEditor) 
                    return headerHeight + EditorGUI.GetPropertyHeight( prop, true );
 
                // else
                float bodyHeight = 0;
                int d = prop.depth;
                prop.Next( true );
                do {
                    if (prop.depth <= d) break;
                    bodyHeight += EditorGUI.GetPropertyHeight( prop, true ) + EditorGUIUtility.standardVerticalSpacing;
                } while (prop.Next( false ));

                return headerHeight + bodyHeight;
            };
            _classList.drawElementCallback += (rect, index, active, focused) => {
                var prop = classDependenciesProp.GetArrayElementAtIndex( index );
                // check if has custom editor
                rect.height = EditorGUIUtility.singleLineHeight;

                prop.isExpanded = EditorGUI.BeginFoldoutHeaderGroup(
                    position: rect,
                    foldout: prop.isExpanded,
                    content: prop.managedReferenceValue.GetType().ToString(),
                    menuAction: rect => {
                        var menu = new GenericMenu();
                        menu.AddItem( new GUIContent( "Remove" ), false, () => {
                            classDependenciesProp.DeleteArrayElementAtIndex( index );
                            serializedObject.ApplyModifiedProperties();
                        } );
                        menu.DropDown( rect );
                    }
                );
                EditorGUI.EndFoldoutHeaderGroup();

                EditorGUI.indentLevel++;

                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                if (prop.isExpanded) {
                    bool hasCustomEditor = HasCustomEditor( prop );
                    if (hasCustomEditor) {
                        EditorGUI.PropertyField( rect, prop, true );
                    } else {
                        int d = prop.depth;
                        prop.Next( true );
                        do {
                            if (prop.depth <= d) break;
                            EditorGUI.PropertyField( rect, prop, true );
                            rect.y += EditorGUI.GetPropertyHeight( prop, true ) +
                                      EditorGUIUtility.standardVerticalSpacing;
                        } while (prop.Next( false ));
                    }
                }
                EditorGUI.indentLevel--;
            };

            _classList.onAddDropdownCallback += (_, _) => {
                _classTypeDropDown.Show( new Rect( Event.current.mousePosition, Vector2.zero ),
                    onSelect: type => {
                        if (type == null) return;
                        var dependency = Activator.CreateInstance( type );
                        if (dependency == null) return;
                        classDependenciesProp.arraySize++;
                        classDependenciesProp.GetArrayElementAtIndex( classDependenciesProp.arraySize - 1 )
                            .managedReferenceValue = dependency;
                        serializedObject.ApplyModifiedProperties();
                    } );
            };

        }

        void SetupStructList() {
            _structList = new ReorderableList( serializedObject, structDependenciesProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: !Application.isPlaying,
                displayRemoveButton: !Application.isPlaying );

            _structList.headerHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            _structList.drawHeaderCallback += rect => {
                _structListHeaderGuiContent ??= new GUIContent(structDependenciesProp.displayName, structDependenciesProp.tooltip);
                GUI.Label( rect, _structListHeaderGuiContent, _headerStyle );
            };

            _structList.elementHeightCallback += index => {
                var prop = structDependenciesProp.GetArrayElementAtIndex( index );
                var headerHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                if (!prop.isExpanded) return headerHeight;

                var hasCustomEditor = HasCustomEditor( prop );
                if (hasCustomEditor) 
                    return headerHeight + EditorGUI.GetPropertyHeight( prop, true );
 
                // else
                float bodyHeight = 0;
                int d = prop.depth;
                prop.Next( true );
                prop.Next( true );
                do {
                    if (prop.depth <= d) break;
                    bodyHeight += EditorGUI.GetPropertyHeight( prop, true ) + EditorGUIUtility.standardVerticalSpacing;
                } while (prop.Next( false ));

                return headerHeight + bodyHeight;
            };
            
            _structList.drawElementCallback += (rect, index, active, focused) => {
                var prop = structDependenciesProp.GetArrayElementAtIndex( index );
                // check if has custom editor
                rect.height = EditorGUIUtility.singleLineHeight;

                prop.isExpanded = EditorGUI.BeginFoldoutHeaderGroup(
                    position: rect,
                    foldout: prop.isExpanded,
                    content: ((StructHolder)prop.managedReferenceValue).GetValueType().ToString(),
                    menuAction: rect => {
                        var menu = new GenericMenu();
                        menu.AddItem( new GUIContent( "Remove" ), false, () => {
                            structDependenciesProp.DeleteArrayElementAtIndex( index );
                            serializedObject.ApplyModifiedProperties();
                        } );
                        menu.DropDown( rect );
                    }
                );
                EditorGUI.EndFoldoutHeaderGroup();
                
                using (new EditorGUI.DisabledScope( Application.isPlaying )) {
                    EditorGUI.indentLevel++;
                    rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    if (prop.isExpanded) {
                        bool hasCustomEditor = HasCustomEditor( prop );
                        if (hasCustomEditor) {
                            EditorGUI.PropertyField( rect, prop, true );
                        } else {
                            int d = prop.depth;
                            prop.Next( true );
                            prop.Next( true );
                            do {
                                if (prop.depth <= d) break;
                                EditorGUI.PropertyField( rect, prop, true );
                                rect.y += EditorGUI.GetPropertyHeight( prop, true ) +
                                          EditorGUIUtility.standardVerticalSpacing;
                            } while (prop.Next( false ));
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            };

            _structList.onAddDropdownCallback += (_, _) => {
                _structTypeDropDown.Show( new Rect( Event.current.mousePosition, Vector2.zero ),
                    onSelect: type => {
                        if (type == null) return;
                        var dependency = Activator.CreateInstance( type );
                        structDependenciesProp.arraySize++;
                        structDependenciesProp.GetArrayElementAtIndex( structDependenciesProp.arraySize - 1 )
                            .managedReferenceValue = new BoxedStructHolder( dependency );
                        serializedObject.ApplyModifiedProperties();
                    } );
            };

        }

        static bool HasCustomEditor(SerializedProperty prop) => prop.managedReferenceValue is IBHasCustomDrawer;

        public override void OnInspectorGUI() {
            if ( classDependenciesProp == null ) { 
                objectDependenciesProp = serializedObject.FindProperty( nameof(BContext.UnityObjectDependencies) );
                classDependenciesProp = serializedObject.FindProperty( nameof(BContext.ClassDependencies) );
                structDependenciesProp = serializedObject.FindProperty( nameof(BContext.StructDependencies_Serializaded) );
                groupProp = serializedObject.FindProperty( nameof(BContext.Group) );
                SetupObjectList();
                SetupClassList();
                SetupStructList();
            }

            InitStylesIfNotAlready();
            serializedObject.Update();
            
            _objectList.DoLayoutList();
            _classList.DoLayoutList();
            DrawAdvanced();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawAdvanced() {
            _advancedFoldout = EditorGUILayout.BeginFoldoutHeaderGroup( _advancedFoldout, "Advanced" );
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (_advancedFoldout) {
                // group prop
                using (new EditorGUILayout.HorizontalScope()) {
                    var isGrouped = groupProp.intValue != 0;
                    using (var check = new EditorGUI.ChangeCheckScope()) {
                        using (new LabelWidth( 50 ))
                            isGrouped = EditorGUILayout.Toggle( new GUIContent( "Group", groupProp.tooltip ), isGrouped,
                                GUILayout.Width( 80 ) );
                        if (check.changed) groupProp.intValue = isGrouped ? 1 : 0;
                    }

                    if (isGrouped) {
                        using (new LabelWidth( 100 ))
                            EditorGUILayout.PropertyField( groupProp );
                    }
                }
                _structList.DoLayoutList();
            }

        }

        static void InitStylesIfNotAlready() {
            if (_headerStyle == null) {
                _headerStyle = new GUIStyle( EditorStyles.label );
                _headerStyle.alignment = TextAnchor.MiddleCenter;
            }
        }
        
        class LabelWidth : IDisposable {
            readonly float w;
            public LabelWidth(float width) {
                w = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = width;
            }
            public void Dispose() => EditorGUIUtility.labelWidth = w;
        }


        class ForceGuiEnable : IDisposable {
            readonly bool enabled;
            public ForceGuiEnable() {
                enabled = GUI.enabled;
                GUI.enabled = true;
            }
            public void Dispose() => GUI.enabled = enabled;
        }
    }
}