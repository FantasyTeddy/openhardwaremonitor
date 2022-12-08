/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/


using System;
using System.Collections.ObjectModel;
using System.Drawing;

namespace OpenHardwareMonitor.GUI
{
    public class Node
    {
        private Node parent;
        private readonly NodeCollection nodes;

        private string text;
        private Image image;
        private bool visible;
        private bool expanded;

        private TreeModel RootTreeModel()
        {
            Node node = this;
            while (node != null)
            {
                if (node.Model != null)
                    return node.Model;
                node = node.parent;
            }
            return null;
        }

        public Node() : this(string.Empty) { }

        public Node(string text)
        {
            this.text = text;
            this.nodes = new NodeCollection(this);
            this.visible = true;
            this.expanded = true;
        }

        public TreeModel Model { get; set; }

        public Node Parent
        {
            get => parent;
            set
            {
                if (value != parent)
                {
                    parent?.nodes.Remove(this);
                    value?.nodes.Add(this);
                }
            }
        }

        public Collection<Node> Nodes => nodes;

        public virtual string Text
        {
            get => text;
            set
            {
                if (text != value)
                {
                    text = value;
                }
            }
        }

        public Image Image
        {
            get => image;
            set
            {
                if (image != value)
                {
                    image = value;
                }
            }
        }

        public virtual bool IsExpanded
        {
            get => expanded;
            set
            {
                if (value != expanded)
                {
                    expanded = value;
                }
            }
        }

        public virtual bool IsVisible
        {
            get => visible;
            set
            {
                if (value != visible)
                {
                    visible = value;
                    TreeModel model = RootTreeModel();
                    if (model != null && parent != null)
                    {
                        int index = 0;
                        for (int i = 0; i < parent.nodes.Count; i++)
                        {
                            Node node = parent.nodes[i];
                            if (node == this)
                                break;
                            if (node.IsVisible || model.ForceVisible)
                                index++;
                        }
                        if (model.ForceVisible)
                        {
                            model.OnNodeChanged(parent, index, this);
                        }
                        else
                        {
                            if (value)
                                model.OnNodeInserted(parent, index, this);
                            else
                                model.OnNodeRemoved(parent, index, this);
                        }
                    }
                    IsVisibleChanged?.Invoke(this, new NodeEventArgs(this));
                }
            }
        }

        public class NodeEventArgs : EventArgs
        {
            public NodeEventArgs(Node node)
            {
                Node = node;
            }

            public Node Node { get; }
        }

        public event EventHandler<NodeEventArgs> IsVisibleChanged;
        public event EventHandler<NodeEventArgs> NodeAdded;
        public event EventHandler<NodeEventArgs> NodeRemoved;

        private class NodeCollection : Collection<Node>
        {
            private readonly Node owner;

            public NodeCollection(Node owner)
            {
                this.owner = owner;
            }

            protected override void ClearItems()
            {
                while (Count != 0)
                    RemoveAt(Count - 1);
            }

            protected override void InsertItem(int index, Node item)
            {
                if (item == null)
                    throw new ArgumentNullException(nameof(item));

                if (item.parent != owner)
                {
                    item.parent?.nodes.Remove(item);
                    item.parent = owner;
                    base.InsertItem(index, item);

                    TreeModel model = owner.RootTreeModel();
                    model?.OnStructureChanged(owner);
                    owner.NodeAdded?.Invoke(this, new NodeEventArgs(item));
                }
            }

            protected override void RemoveItem(int index)
            {
                Node item = this[index];
                item.parent = null;
                base.RemoveItem(index);

                TreeModel model = owner.RootTreeModel();
                model?.OnStructureChanged(owner);
                owner.NodeRemoved?.Invoke(this, new NodeEventArgs(item));
            }

            protected override void SetItem(int index, Node item)
            {
                if (item == null)
                    throw new ArgumentNullException(nameof(item));

                RemoveAt(index);
                InsertItem(index, item);
            }
        }
    }
}
