using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


internal sealed class TypeSelectDropDown : AdvancedDropdown {

    Action<Type> _onSelect;
    Func<Type, bool> _filter;
    AddingItem root;
    string longestName;

    class AddingItem {
        public string name;
        public List<AddingItem> children;
        public AddingItem(string name) => this.name = name;
    }

    public TypeSelectDropDown(AdvancedDropdownState state, Func<Type, bool> filter) : base( state ) {
        _filter = filter;

        root = new AddingItem( "Assemblies" ) { children = new() };
        longestName = string.Empty;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            AddingItem assItem = null;
            foreach (var type in assembly.GetTypes().Where( _filter )) {
                AddingItem item = new( type.FullName );
                assItem ??= new( assembly.GetName().Name ) { children = new() };
                assItem.children.Add( item );
                if (type.FullName.Length > longestName.Length)
                    longestName = type.FullName;
            }

            if (assItem != null) root.children.Add( assItem );
        }
    }

    public void Show(Rect position, Action<Type> onSelect) {
        minimumSize = new Vector2( EditorStyles.label.CalcSize( new GUIContent( longestName ) ).x, minimumSize.y );
        base.Show( position );
        _onSelect = onSelect;
    }
    
    protected override AdvancedDropdownItem BuildRoot() {
        var rootItem = new AdvancedDropdownItem( root.name );
        AddChildrenRecursively( rootItem, root );
        return rootItem;
    }

    void AddChildrenRecursively(AdvancedDropdownItem parentItem, AddingItem parent) {
        for (int i = 0; i < parent.children?.Count; i++) {
            var childItem = new AdvancedDropdownItem( parent.children[i].name );
            parentItem.AddChild( childItem );
            if (childItem.children != null)
                AddChildrenRecursively( childItem, parent.children[i] );
        }
    }

    protected override void ItemSelected(AdvancedDropdownItem item) {
        base.ItemSelected( item );
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany( a => a.GetTypes() )
            .FirstOrDefault( t => t.FullName == item.name );
        _onSelect?.Invoke( type );
    }
}