using System;
using System.Linq;
using System.Reflection;
using Binject;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace BinjectEditor {
    [CustomEditor( typeof(BContext) )]
    internal class BContextEditor : Editor {
        SerializedProperty dataDependenciesProp;
        SerializedProperty objectDependenciesProp;
        SerializedProperty groupProp;
        ReorderableList _dataList;
        ReorderableList _objectList;
        bool _advancedFoldout;
        
        static GUIStyle _headerStyle;
        static GUIContent _objectListHeaderGuiContent;
        static GUIContent _dataListHeaderGuiContent;

        static TypeSelectDropDown _classTypeDropDown = new( new AdvancedDropdownState(),
            filter: type =>
                true
                && !type.IsValueType                                                                        // avoid boxing/unboxing
                && !type.IsSubclassOf( typeof(UnityEngine.Object) )                                         // Unity Objects won't SerializeReference
                && !type.IsAbstract && !type.ContainsGenericParameters &&                                   // exclude useless serializing classes
                !type.IsSubclassOf( typeof(Attribute) )
                && type.IsSerializable                                                                      // only serializable
                && type.Assembly.GetName().Name != "mscorlib"                                               // mscorlib is too low-level to inject
                && !type.FullName.Contains( '<' ) && !type.FullName.Contains( '>' ) &&                      // no compiler-generated types
                !type.FullName.Contains( '+' )
                && !type.IsSubclassOf( typeof(Exception) )                                                  // no need to inject exceptions
                && !type.GetInterfaces().Any( inter => inter.IsSubclassOf( typeof(IDisposable) ) )          // no need to inject disposables
                && type.GetConstructors().Any( c => c.GetParameters().Length == 0 )                         // construct easily 
        );

        
        void SetupDataList() {
            _dataList = new ReorderableList( serializedObject, dataDependenciesProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: !Application.isPlaying,
                displayRemoveButton: !Application.isPlaying );

            _dataList.headerHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            _dataList.drawHeaderCallback += rect => {
                _dataListHeaderGuiContent ??= new GUIContent(dataDependenciesProp.displayName, dataDependenciesProp.tooltip);
                GUI.Label( rect, _dataListHeaderGuiContent, _headerStyle );
            };

            _dataList.elementHeightCallback += index => {
                var prop = dataDependenciesProp.GetArrayElementAtIndex( index );
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
            _dataList.drawElementCallback += (rect, index, active, focused) => {
                var prop = dataDependenciesProp.GetArrayElementAtIndex( index );
                // check if has custom editor
                rect.height = EditorGUIUtility.singleLineHeight;

                prop.isExpanded = EditorGUI.BeginFoldoutHeaderGroup(
                    position: rect,
                    foldout: prop.isExpanded,
                    content: prop.managedReferenceValue.GetType().ToString(),
                    menuAction: rect => {
                        var menu = new GenericMenu();
                        menu.AddItem( new GUIContent( "Remove" ), false, () => {
                            dataDependenciesProp.DeleteArrayElementAtIndex( index );
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
                            if (prop.depth <= d) break;
                            EditorGUI.PropertyField( rect, prop, true );
                            rect.y += EditorGUI.GetPropertyHeight( prop, true ) +
                                      EditorGUIUtility.standardVerticalSpacing;
                        } while (prop.Next( false ));
                    }
                }
            };

            _dataList.onAddDropdownCallback += (_, _) => {
                _classTypeDropDown.Show( new Rect( Event.current.mousePosition, Vector2.zero ),
                    onSelect: type => {
                        if (type == null) return;
                        var dependency = Activator.CreateInstance( type );
                        if (dependency == null) return;
                        dataDependenciesProp.arraySize++;
                        dataDependenciesProp.GetArrayElementAtIndex( dataDependenciesProp.arraySize - 1 )
                            .managedReferenceValue = dependency;
                        serializedObject.ApplyModifiedProperties();
                    } );
            };

        }

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

        static bool HasCustomEditor(SerializedProperty prop) => prop.managedReferenceValue is IBHasCustomDrawer;

        public override void OnInspectorGUI() {
            if ( dataDependenciesProp == null ) { 
                dataDependenciesProp = serializedObject.FindProperty( nameof(BContext.DataDependencies) );
                objectDependenciesProp = serializedObject.FindProperty( nameof(BContext.ObjectDependencies) );
                groupProp = serializedObject.FindProperty( nameof(BContext.Group) );
                SetupDataList();
                SetupObjectList();
            }

            InitStylesIfNotAlready();
            serializedObject.Update();
            _dataList.DoLayoutList();
            _objectList.DoLayoutList();
            DrawAdvanced();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawAdvanced() {
            _advancedFoldout = EditorGUILayout.BeginFoldoutHeaderGroup( _advancedFoldout, "Advanced" );
            EditorGUILayout.EndFoldoutHeaderGroup();
            using (new EditorGUI.DisabledGroupScope( Application.isPlaying )) {
                if (_advancedFoldout) {
                    
                    // group prop
                    using (new EditorGUILayout.HorizontalScope()) {
                        var isGrouped = groupProp.intValue != 0;
                        using (var check = new EditorGUI.ChangeCheckScope()) {
                            using (new LabelWidth( 50 ))
                                isGrouped = EditorGUILayout.Toggle( new GUIContent( "Group", groupProp.tooltip ), isGrouped, GUILayout.Width( 80 ) );
                            if (check.changed) groupProp.intValue = isGrouped ? 1 : 0;
                        }
                        if (isGrouped) {
                            using (new LabelWidth( 100 ))
                                EditorGUILayout.PropertyField( groupProp );
                        }
                    }
                    
                }
            }
        }

        static void InitStylesIfNotAlready() {
            if (_headerStyle == null) {
                _headerStyle = new GUIStyle( EditorStyles.label );
                _headerStyle.alignment = TextAnchor.MiddleCenter;
            }
        }
        
        class LabelWidth : IDisposable {
            float w;
            public LabelWidth(float width) {
                w = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = width;
            }
            public void Dispose() => EditorGUIUtility.labelWidth = w;
        }
    }
}