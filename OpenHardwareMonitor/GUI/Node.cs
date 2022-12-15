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
        private Node _parent;
        private readonly NodeCollection _nodes;

        private string _text;
        private Image _image;
        private bool _visible;
        private bool _expanded;

        private TreeModel RootTreeModel()
        {
            Node node = this;
            while (node != null)
            {
                if (node.Model != null)
                    return node.Model;
                node = node._parent;
            }
            return null;
        }

        public Node()
            : this(string.Empty) { }

        public Node(string text)
        {
            _text = text;
            _nodes = new NodeCollection(this);
            _visible = true;
            _expanded = true;
        }

        public TreeModel Model { get; set; }

        public Node Parent
        {
            get => _parent;
            set
            {
                if (value != _parent)
                {
                    _parent?._nodes.Remove(this);
                    value?._nodes.Add(this);
                }
            }
        }

        public Collection<Node> Nodes => _nodes;

        public virtual string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                }
            }
        }

        public Image Image
        {
            get => _image;
            set
            {
                if (_image != value)
                {
                    _image = value;
                }
            }
        }

        public virtual bool IsExpanded
        {
            get => _expanded;
            set
            {
                if (value != _expanded)
                {
                    _expanded = value;
                }
            }
        }

        public virtual bool IsVisible
        {
            get => _visible;
            set
            {
                if (value != _visible)
                {
                    _visible = value;
                    TreeModel model = RootTreeModel();
                    if (model != null && _parent != null)
                    {
                        int index = 0;
                        for (int i = 0; i < _parent._nodes.Count; i++)
                        {
                            Node node = _parent._nodes[i];
                            if (node == this)
                                break;
                            if (node.IsVisible || model.ForceVisible)
                                index++;
                        }
                        if (model.ForceVisible)
                        {
                            model.OnNodeChanged(_parent, index, this);
                        }
                        else
                        {
                            if (value)
                                model.OnNodeInserted(_parent, index, this);
                            else
                                model.OnNodeRemoved(_parent, index, this);
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
            private readonly Node _owner;

            public NodeCollection(Node owner)
            {
                _owner = owner;
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

                if (item._parent != _owner)
                {
                    item._parent?._nodes.Remove(item);
                    item._parent = _owner;
                    base.InsertItem(index, item);

                    TreeModel model = _owner.RootTreeModel();
                    model?.OnStructureChanged(_owner);
                    _owner.NodeAdded?.Invoke(this, new NodeEventArgs(item));
                }
            }

            protected override void RemoveItem(int index)
            {
                Node item = this[index];
                item._parent = null;
                base.RemoveItem(index);

                TreeModel model = _owner.RootTreeModel();
                model?.OnStructureChanged(_owner);
                _owner.NodeRemoved?.Invoke(this, new NodeEventArgs(item));
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
