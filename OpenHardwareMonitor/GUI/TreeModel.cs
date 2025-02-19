﻿/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Aga.Controls.Tree;

namespace OpenHardwareMonitor.GUI
{
    public class TreeModel : ITreeModel
    {

        private readonly Node _root;
        private bool _forceVisible;

        public TreeModel()
        {
            _root = new Node
            {
                Model = this
            };
        }

        public TreePath GetPath(Node node)
        {
            if (node == _root)
            {
                return TreePath.Empty;
            }
            else
            {
                Stack<object> stack = new Stack<object>();
                while (node != _root)
                {
                    stack.Push(node);
                    node = node.Parent;
                }
                return new TreePath(stack.ToArray());
            }
        }

        public Collection<Node> Nodes => _root.Nodes;

        private Node GetNode(TreePath treePath)
        {
            Node parent = _root;
            foreach (object obj in treePath.FullPath)
            {
                if (!(obj is Node node) || node.Parent != parent)
                    return null;
                parent = node;
            }
            return parent;
        }

        public IEnumerable GetChildren(TreePath treePath)
        {
            Node node = GetNode(treePath);
            if (node != null)
            {
                foreach (Node n in node.Nodes)
                {
                    if (_forceVisible || n.IsVisible)
                        yield return n;
                }
            }
            else
            {
                yield break;
            }
        }

        public bool IsLeaf(TreePath treePath)
        {
            return false;
        }

        public bool ForceVisible
        {
            get => _forceVisible;
            set
            {
                if (value != _forceVisible)
                {
                    _forceVisible = value;
                    OnStructureChanged(_root);
                }
            }
        }

#pragma warning disable 67
        public event EventHandler<TreeModelEventArgs> NodesChanged;
        public event EventHandler<TreePathEventArgs> StructureChanged;
        public event EventHandler<TreeModelEventArgs> NodesInserted;
        public event EventHandler<TreeModelEventArgs> NodesRemoved;
#pragma warning restore 67

        public void OnNodeChanged(Node parent, int index, Node node)
        {
            if (NodesChanged != null && parent != null)
            {
                TreePath path = GetPath(parent);
                if (path != null)
                {
                    NodesChanged(this, new TreeModelEventArgs(
                      path, new int[] { index }, new object[] { node }));
                }
            }
        }

        public void OnStructureChanged(Node node)
        {
            StructureChanged?.Invoke(this,
  new TreeModelEventArgs(GetPath(node), Array.Empty<object>()));
        }

        public void OnNodeInserted(Node parent, int index, Node node)
        {
            if (NodesInserted != null)
            {
                TreeModelEventArgs args = new TreeModelEventArgs(GetPath(parent),
                  new int[] { index }, new object[] { node });
                NodesInserted(this, args);
            }

        }

        public void OnNodeRemoved(Node parent, int index, Node node)
        {
            if (NodesRemoved != null)
            {
                TreeModelEventArgs args = new TreeModelEventArgs(GetPath(parent),
                  new int[] { index }, new object[] { node });
                NodesRemoved(this, args);
            }
        }

    }
}
