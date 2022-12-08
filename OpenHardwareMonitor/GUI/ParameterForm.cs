﻿/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>

*/


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using OpenHardwareMonitor.Hardware;

namespace OpenHardwareMonitor.GUI
{
    public partial class ParameterForm : Form
    {

        private IReadOnlyList<IParameter> parameters;
        private BindingList<ParameterRow> parameterRows;

        public ParameterForm()
        {
            InitializeComponent();
        }

        public IReadOnlyList<IParameter> Parameters
        {
            get => parameters;
            set
            {
                parameters = value;
                parameterRows = new BindingList<ParameterRow>();
                foreach (IParameter parameter in parameters)
                    parameterRows.Add(new ParameterRow(parameter));
                bindingSource.DataSource = parameterRows;
            }
        }

        private class ParameterRow : INotifyPropertyChanged
        {
            public IParameter parameter;
            private float value;
            public bool isDefault;

            public event PropertyChangedEventHandler PropertyChanged;

            private void NotifyPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            public ParameterRow(IParameter parameter)
            {
                this.parameter = parameter;
                value = parameter.Value;
                isDefault = parameter.IsDefault;
            }

            public string Name => parameter.Name;

            public float Value
            {
                get => value;
                set
                {
                    isDefault = false;
                    this.value = value;
                    NotifyPropertyChanged(nameof(Default));
                    NotifyPropertyChanged(nameof(Value));
                }
            }

            public bool Default
            {
                get => isDefault;
                set
                {
                    isDefault = value;
                    if (value)
                        this.value = parameter.DefaultValue;
                    NotifyPropertyChanged(nameof(Default));
                    NotifyPropertyChanged(nameof(Value));
                }
            }
        }

        private void dataGridView_RowEnter(object sender,
          DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < parameters.Count)
                descriptionLabel.Text = parameters[e.RowIndex].Description;
            else
                descriptionLabel.Text = string.Empty;
        }

        private void dataGridView_CellValidating(object sender,
          DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex == 2 &&
              !float.TryParse(e.FormattedValue.ToString(), out float value))
            {
                dataGridView.Rows[e.RowIndex].Cells[0].ErrorText =
                  "Invalid value";
                e.Cancel = true;
            }
        }

        private void dataGridView_CellEndEdit(object sender,
          DataGridViewCellEventArgs e)
        {
            dataGridView.Rows[e.RowIndex].Cells[0].ErrorText = string.Empty;
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            foreach (ParameterRow row in parameterRows)
            {
                if (row.Default)
                {
                    row.parameter.IsDefault = true;
                }
                else
                {
                    row.parameter.Value = row.Value;
                }
            }
        }

        private void dataGridView_CurrentCellDirtyStateChanged(object sender,
          EventArgs e)
        {
            if (dataGridView.CurrentCell is DataGridViewCheckBoxCell ||
              dataGridView.CurrentCell is DataGridViewComboBoxCell)
            {
                dataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }
    }
}
