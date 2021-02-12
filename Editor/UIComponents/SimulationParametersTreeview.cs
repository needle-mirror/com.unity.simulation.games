using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace Unity.Simulation.Games.Editor.UIComponents
{
    /// <summary>
    /// Class that can be used to make a GameSim parameters treeview element
    /// </summary>
    ///
    public class GameSimParametersTreeElement
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Key { get; set; }
        public string Type { get; set; }
        public string DefaultValue { get; set; }
        public string Values { get; set; }

        public GameSimParametersTreeElement Parent { get; private set; }
        private List<GameSimParametersTreeElement> _children = new List<GameSimParametersTreeElement>();
        public List<GameSimParametersTreeElement> Children { get { return _children; } }

        /// < summary > 
        /// Add a child 
        /// </ summary > 
        public void AddChild(GameSimParametersTreeElement child)
        {
            if (child.Parent != null)
            {
                child.Parent.RemoveChild(child);
            }
            Children.Add(child);
            child.Parent = this;
        }

        /// < summary > 
        /// Remove child 
        /// </ summary > 
        public void RemoveChild(GameSimParametersTreeElement child)
        {
            if (Children.Contains(child))
            {
                Children.Remove(child);
                child.Parent = null;
            }
        }
    }

    /// <summary>
    /// Class that can be used to make a GameSim parameters treeview
    /// </summary>
    public class GameSimParametersTreeview : TreeView
    {
        public Dictionary<string, Tuple<string, string>> parameterDict = new Dictionary<string, Tuple<string, string>>();

        class GameSimParametersTreeviewItem : TreeViewItem
        {
            public GameSimParametersTreeElement Data { get; set; }
        }

        private GameSimParametersTreeElement[] _baseElements;

        // Pass MultiColumnHeader at initialization 
        public GameSimParametersTreeview(TreeViewState treeViewState, MultiColumnHeader multiColumnHeader) : base(treeViewState, multiColumnHeader) {
        }

        public void Setup(GameSimParametersTreeElement[] baseElements)
        {
            _baseElements = baseElements;
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };

            var rows = GetRows() ?? new List<TreeViewItem>();
            rows.Clear();

            foreach (var baseElement in _baseElements)
            {
                var baseItem = CreateTreeViewItem(baseElement);
                root.AddChild(baseItem);
                rows.Add(baseItem);
                if (baseElement.Children.Count >= 1)
                {
                    if (IsExpanded(baseItem.id))
                    {
                        AddChildrenRecursive(baseElement, baseItem, rows);
                    }
                    else
                    {
                        baseItem.children = CreateChildListForCollapsedParent();
                    }
                }
            }

            SetupParentsAndChildrenFromDepths(root, rows);

            return root;
        }

        private void AddChildrenRecursive(GameSimParametersTreeElement element, TreeViewItem item, IList<TreeViewItem> rows)
        {
            foreach (var childElement in element.Children)
            {
                var childItem = CreateTreeViewItem(childElement);
                item.AddChild(childItem);
                rows.Add(childItem);
                if (childElement.Children.Count >= 1)
                {
                    childItem.children = CreateChildListForCollapsedParent();
                }
            }
        }

        private GameSimParametersTreeviewItem CreateTreeViewItem(GameSimParametersTreeElement model)
        {
            return new GameSimParametersTreeviewItem { id = model.Id, displayName = model.Name, Data = model };
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            GUIStyle style = GUI.skin.label;
            var item = (GameSimParametersTreeviewItem)args.item;

            // Process for each displayed column
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {

                // Rect in the current cell
                var cellRect = args.GetCellRect(i);
                var columnIndex = args.GetColumn(i);

                // Utility method to center the cell Rect up and down (you don't need to use it if you don't need it) 
                CenterRectUsingSingleLineHeight(ref cellRect);
                
                if (columnIndex == 0)
                {
                    GUI.Label(cellRect, item.Data.Key);
                }
                else if (columnIndex == 1)
                {
                    GUI.Label(cellRect, item.Data.Type);
                }
                else if (columnIndex == 2)
                {
                    GUI.Label(cellRect, item.Data.DefaultValue);
                    
                }
                else if (columnIndex == 3)
                {
                    item.Data.Values = GUI.TextField(cellRect, item.Data.Values);
                }

                if (!parameterDict.ContainsKey(item.Data.Key))
                {
                    parameterDict[item.Data.Key] = new Tuple<string, string>(item.Data.Type, item.Data.Values);
                }
                else
                {
                    string v1 = parameterDict[item.Data.Key].Item1;
                    string v2 = parameterDict[item.Data.Key].Item2;

                    if (v1 != item.Data.Type || v2 != item.Data.Values)
                    {
                        parameterDict[item.Data.Key] = new Tuple<string, string>(item.Data.Type, item.Data.Values);
                    }
                }
            }
        }
    }
}
