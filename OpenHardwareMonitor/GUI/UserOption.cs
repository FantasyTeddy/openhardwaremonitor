/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Windows.Forms;
using OpenHardwareMonitor.Utilities;

namespace OpenHardwareMonitor.GUI
{
    public class UserOption
    {
        private readonly string _name;
        private bool _value;
        private readonly MenuItem _menuItem;
        private event EventHandler changed;
        private readonly PersistentSettings _settings;

        public UserOption(string name, bool value,
          MenuItem menuItem, PersistentSettings settings)
        {

            _settings = settings;
            _name = name;
            if (name != null)
                _value = settings.GetValue(name, value);
            else
                _value = value;
            _menuItem = menuItem;
            _menuItem.Checked = _value;
            _menuItem.Click += new EventHandler(menuItem_Click);
        }

        private void menuItem_Click(object sender, EventArgs e)
        {
            Value = !Value;
        }

        public bool Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    if (_name != null)
                        _settings.SetValue(_name, value);
                    _menuItem.Checked = value;
                    changed?.Invoke(this, null);
                }
            }
        }

        public event EventHandler Changed
        {
            add
            {
                changed += value;
                changed?.Invoke(this, null);
            }
            remove => changed -= value;
        }
    }
}
