﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseManager.Helper;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Core;
using DatabaseManager.Model;
using DatabaseInterpreter.Utility;

namespace DatabaseManager.Controls
{
    public partial class UC_TableColumns : UserControl
    {
        private Rectangle dragBoxFromMouseDown;
        private int rowIndexFromMouseDown;
        private int rowIndexOfItemUnderMouseToDrop;
        private bool defaultNullable = false;
        private IEnumerable<DataTypeSpecification> dataTypeSpecifications;
        public DatabaseType DatabaseType { get; set; }
        public List<UserDefinedType> UserDefinedTypes { get; set; }

        public GeneateChangeScriptsHandler OnGenerateChangeScripts;

        public UC_TableColumns()
        {
            InitializeComponent();
        }

        private void UC_TableColumns_Load(object sender, EventArgs e)
        {
            this.InitColumnsGrid();
        }

        public void InitControls()
        {
            this.LoadDataTypes();

            if (this.DatabaseType == DatabaseType.Oracle)
            {
                this.colIdentity.Visible = false;
                this.colDataType.Width = 200;
            }
        }

        private void InitColumnsGrid()
        {
            foreach (DataGridViewRow row in this.dgvColumns.Rows)
            {
                if (row.Tag == null)
                {
                    if (row.IsNewRow)
                    {
                        row.Cells[this.colNullable.Name].Value = this.defaultNullable;
                    }

                    row.Tag = new TableColumnDesingerInfo() { IsNullable = this.defaultNullable };
                }

                string columnName = row.Cells[nameof(colColumnName)].Value?.ToString();

                if (string.IsNullOrEmpty(columnName))
                {
                    DataGridViewHelper.SetRowColumnsReadOnly(this.dgvColumns, row, true, this.colColumnName);
                }
            }
        }

        public void LoadColumns(Table table, IEnumerable<TableColumnDesingerInfo> columns)
        {
            this.dgvColumns.Rows.Clear();

            foreach (TableColumnDesingerInfo column in columns)
            {
                int rowIndex = this.dgvColumns.Rows.Add();

                DataGridViewRow row = this.dgvColumns.Rows[rowIndex];

                row.Cells[this.colColumnName.Name].Value = column.Name;
                row.Cells[this.colDataType.Name].Value = column.DataType;
                row.Cells[this.colLength.Name].Value = column.Length;
                row.Cells[this.colNullable.Name].Value = column.IsNullable;
                row.Cells[this.colIdentity.Name].Value = column.IsIdentity;
                row.Cells[this.colPrimary.Name].Value = column.IsPrimary;
                row.Cells[this.colDefaultValue.Name].Value = ValueHelper.GetTrimedDefaultValue(column.DefaultValue);
                row.Cells[this.colComment.Name].Value = column.Comment;

                row.Tag = column;

                TableColumnExtraPropertyInfo extraPropertyInfo = new TableColumnExtraPropertyInfo();

                if (column.IsComputed)
                {
                    extraPropertyInfo.Expression = column.ComputeExp;
                }

                if (column.IsIdentity && table.IdentitySeed.HasValue)
                {
                    extraPropertyInfo.Seed = table.IdentitySeed.Value;
                    extraPropertyInfo.Increment = table.IdentityIncrement.Value;
                }

                this.SetColumnCellsReadonly(row);
            }

            this.AutoSizeColumns();
            this.dgvColumns.ClearSelection();
        }

        public void OnSaved()
        {
            for (int i = 0; i < this.dgvColumns.RowCount; i++)
            {
                DataGridViewRow row = this.dgvColumns.Rows[i];

                TableColumnDesingerInfo columnDesingerInfo = row.Tag as TableColumnDesingerInfo;

                if (columnDesingerInfo != null && !string.IsNullOrEmpty(columnDesingerInfo.Name))
                {
                    columnDesingerInfo.OldName = columnDesingerInfo.Name;
                }
            }
        }

        private void LoadDataTypes()
        {
            dataTypeSpecifications = DataTypeManager.GetDataTypeSpecifications(this.DatabaseType);

            List<DatabaseObject> dbObjects = new List<DatabaseObject>();

            dbObjects.AddRange(dataTypeSpecifications.Select(item => new DatabaseObject { Name = item.Name }));

            if (this.UserDefinedTypes != null)
            {
                dbObjects.AddRange(this.UserDefinedTypes);
            }

            this.colDataType.DataSource = dbObjects;
            this.colDataType.DisplayMember = "Name";
            this.colDataType.ValueMember = "Name";
            this.colDataType.AutoComplete = true;
        }

        private UserDefinedType GetUserDefinedType(string dataType)
        {
            return this.UserDefinedTypes?.FirstOrDefault(item => item.Name == dataType);
        }

        public void EndEdit()
        {
            this.dgvColumns.EndEdit();
            this.dgvColumns.CurrentCell = null;
        }

        public List<TableColumnDesingerInfo> GetColumns()
        {
            List<TableColumnDesingerInfo> columnDesingerInfos = new List<TableColumnDesingerInfo>();

            int order = 1;
            foreach (DataGridViewRow row in this.dgvColumns.Rows)
            {
                TableColumnDesingerInfo col = new TableColumnDesingerInfo() { Order = order };

                string colName = row.Cells[this.colColumnName.Name].Value?.ToString();

                if (!string.IsNullOrEmpty(colName))
                {
                    TableColumnDesingerInfo tag = row.Tag as TableColumnDesingerInfo;

                    string dataType = DataGridViewHelper.GetCellStringValue(row, this.colDataType.Name);

                    col.OldName = tag?.OldName;
                    col.Name = colName;
                    col.DataType = dataType;
                    col.Length = DataGridViewHelper.GetCellStringValue(row, this.colLength.Name);
                    col.IsNullable = DataGridViewHelper.GetCellBoolValue(row, this.colNullable.Name);
                    col.IsPrimary = DataGridViewHelper.GetCellBoolValue(row, this.colPrimary.Name);
                    col.IsIdentity = DataGridViewHelper.GetCellBoolValue(row, this.colIdentity.Name);
                    col.DefaultValue = DataGridViewHelper.GetCellStringValue(row, this.colDefaultValue.Name);
                    col.Comment = DataGridViewHelper.GetCellStringValue(row, this.colComment.Name);
                    col.ExtraPropertyInfo = tag?.ExtraPropertyInfo;

                    UserDefinedType userDefinedType = this.GetUserDefinedType(dataType);

                    if (userDefinedType != null)
                    {
                        col.IsUserDefined = true;
                        col.TypeOwner = userDefinedType.Owner;
                    }

                    row.Tag = col;

                    columnDesingerInfos.Add(col);

                    order++;
                }
            }

            return columnDesingerInfos;
        }

        private void UC_TableColumns_SizeChanged(object sender, EventArgs e)
        {
            this.AutoSizeColumns();
        }

        private void AutoSizeColumns()
        {
            DataGridViewHelper.AutoSizeLastColumn(this.dgvColumns);
        }

        private void dgvColumns_UserAddedRow(object sender, DataGridViewRowEventArgs e)
        {
            e.Row.Cells[this.colNullable.Name].Value = this.defaultNullable;
            e.Row.Tag = new TableColumnDesingerInfo() { IsNullable = defaultNullable };

            DataGridViewHelper.SetRowColumnsReadOnly(this.dgvColumns, e.Row, true, this.colColumnName);
        }

        private void dgvColumns_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                DataGridViewRow row = this.dgvColumns.Rows[e.RowIndex];
                DataGridViewCell cell = row.Cells[e.ColumnIndex];

                if (e.ColumnIndex == this.colColumnName.Index)
                {
                    string columnName = cell.Value?.ToString();

                    DataGridViewHelper.SetRowColumnsReadOnly(this.dgvColumns, row, string.IsNullOrEmpty(columnName), this.colColumnName);
                    this.SetColumnCellsReadonly(row);
                }
                else if (e.ColumnIndex == this.colDataType.Index)
                {
                    this.SetColumnCellsReadonly(row);
                }
                else if (e.ColumnIndex == this.colPrimary.Index)
                {
                    DataGridViewCell primaryCell = row.Cells[this.colPrimary.Name];
                    DataGridViewCell nullableCell = row.Cells[this.colNullable.Name];

                    if (DataGridViewHelper.IsTrueValue(primaryCell.Value) && DataGridViewHelper.IsTrueValue(nullableCell.Value))
                    {
                        nullableCell.Value = false;
                    }
                }
                else if (e.ColumnIndex == this.colIdentity.Index)
                {
                    if (DataGridViewHelper.IsTrueValue(cell.Value))
                    {
                        foreach (DataGridViewRow r in this.dgvColumns.Rows)
                        {
                            if (r.Index >= 0 && r.Index != e.RowIndex)
                            {
                                r.Cells[this.colIdentity.Name].Value = false;
                            }
                        }
                    }

                    this.ShowColumnExtraPropertites();
                }
            }
        }

        private void SetColumnCellsReadonly(DataGridViewRow row)
        {
            DataGridViewCell lengthCell = row.Cells[this.colLength.Name];
            DataGridViewCell primaryCell = row.Cells[this.colPrimary.Name];
            DataGridViewCell identityCell = row.Cells[this.colIdentity.Name];

            string dataType = DataGridViewHelper.GetCellStringValue(row, this.colDataType.Name);

            if (!string.IsNullOrEmpty(dataType))
            {
                UserDefinedType userDefindedType = this.GetUserDefinedType(dataType);

                if (userDefindedType != null)
                {
                    dataType = userDefindedType.Type;
                }

                DataTypeSpecification dataTypeSpec = this.dataTypeSpecifications.FirstOrDefault(item => item.Name == dataType);

                if (dataTypeSpec != null)
                {
                    bool isLengthReadOnly = userDefindedType != null || string.IsNullOrEmpty(dataTypeSpec.Args);
                    bool isPrimaryReadOnly = dataTypeSpec.IndexForbidden;
                    bool isIdentityReadOnly = !dataTypeSpec.AllowIdentity;

                    lengthCell.ReadOnly = isLengthReadOnly;
                    primaryCell.ReadOnly = isPrimaryReadOnly;
                    identityCell.ReadOnly = isIdentityReadOnly;

                    if (isLengthReadOnly)
                    {
                        lengthCell.Value = null;
                    }

                    if (isPrimaryReadOnly)
                    {
                        primaryCell.Value = false;
                    }

                    if (isIdentityReadOnly)
                    {
                        identityCell.Value = false;
                    }
                }
            }
            else
            {
                lengthCell.ReadOnly = true;
                primaryCell.ReadOnly = true;
                identityCell.ReadOnly = true;
            }
        }

        private void dgvColumns_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {

        }

        private void dgvColumns_SelectionChanged(object sender, EventArgs e)
        {
            this.ShowColumnExtraPropertites();
        }

        private void ShowColumnExtraPropertites()
        {
            var row = DataGridViewHelper.GetSelectedRow(this.dgvColumns);

            if (row != null)
            {
                TableColumnDesingerInfo column = row.Tag as TableColumnDesingerInfo;

                if (column == null)
                {
                    column = new TableColumnDesingerInfo();
                    row.Tag = column;
                }

                TableColumnExtraPropertyInfo extralProperty = column?.ExtraPropertyInfo;

                if (extralProperty == null)
                {
                    extralProperty = new TableColumnExtraPropertyInfo();
                    column.ExtraPropertyInfo = extralProperty;
                }

                DataGridViewCell identityCell = row.Cells[this.colIdentity.Name];

                if (!DataGridViewHelper.IsTrueValue(identityCell.Value))
                {
                    this.columnPropertites.HiddenProperties = new string[] { nameof(extralProperty.Seed), nameof(extralProperty.Increment) };
                }
                else
                {
                    this.columnPropertites.HiddenProperties = null;
                }

                this.columnPropertites.SelectedObject = extralProperty;
                this.columnPropertites.Refresh();
            }
        }

        private void dgvColumns_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (e.ColumnIndex == this.colIdentity.Index)
            {
                this.dgvColumns.EndEdit();
            }
        }

        private void dgvColumns_DragDrop(object sender, DragEventArgs e)
        {
            Point clientPoint = this.dgvColumns.PointToClient(new Point(e.X, e.Y));

            rowIndexOfItemUnderMouseToDrop = this.dgvColumns.HitTest(clientPoint.X, clientPoint.Y).RowIndex;

            if (rowIndexOfItemUnderMouseToDrop == -1)
            {
                return;
            }

            if (rowIndexFromMouseDown >= 0 && rowIndexOfItemUnderMouseToDrop < this.dgvColumns.Rows.Count)
            {
                if (this.dgvColumns.Rows[rowIndexOfItemUnderMouseToDrop].IsNewRow)
                {
                    return;
                }
            }

            if (e.Effect == DragDropEffects.Move)
            {
                DataGridViewRow rowToMove = e.Data.GetData(typeof(DataGridViewRow)) as DataGridViewRow;

                if (rowToMove.Index >= 0)
                {
                    this.dgvColumns.Rows.RemoveAt(rowIndexFromMouseDown);
                    this.dgvColumns.Rows.Insert(rowIndexOfItemUnderMouseToDrop, rowToMove);

                    string columnName = DataGridViewHelper.GetCellStringValue(rowToMove, this.colColumnName.Name);

                    DataGridViewHelper.SetRowColumnsReadOnly(this.dgvColumns, rowToMove, string.IsNullOrEmpty(columnName), this.colColumnName);
                    this.SetColumnCellsReadonly(rowToMove);
                }
            }
        }

        private void dgvColumns_MouseMove(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                if (dragBoxFromMouseDown != Rectangle.Empty &&
                !dragBoxFromMouseDown.Contains(e.X, e.Y))
                {
                    DragDropEffects dropEffect = this.dgvColumns.DoDragDrop(
                          this.dgvColumns.Rows[rowIndexFromMouseDown],
                          DragDropEffects.Move);
                }
            }
        }

        private void dgvColumns_MouseDown(object sender, MouseEventArgs e)
        {
            var hit = this.dgvColumns.HitTest(e.X, e.Y);
            rowIndexFromMouseDown = hit.RowIndex;

            if (hit.Type == DataGridViewHitTestType.RowHeader && rowIndexFromMouseDown != -1)
            {
                Size dragSize = SystemInformation.DragSize;

                dragBoxFromMouseDown = new Rectangle(
                          new Point(
                            e.X - (dragSize.Width / 2),
                            e.Y - (dragSize.Height / 2)),
                      dragSize);
            }
            else
            {
                dragBoxFromMouseDown = Rectangle.Empty;
            }
        }

        private void dgvColumns_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void tsmiInsertColumn_Click(object sender, EventArgs e)
        {
            DataGridViewRow row = DataGridViewHelper.GetSelectedRow(this.dgvColumns);

            if (row != null)
            {
                this.dgvColumns.Rows.Insert(row.Index - 1 < 0 ? 0 : row.Index - 1);
            }
        }

        private void tsmiDeleteColumn_Click(object sender, EventArgs e)
        {
            this.DeleteRow();
        }

        private void dgvColumns_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                DataGridViewRow row = DataGridViewHelper.GetSelectedRow(this.dgvColumns);

                if (row != null)
                {
                    bool isEmptyNewRow = row.IsNewRow && DataGridViewHelper.IsEmptyRow(row);

                    this.tsmiDeleteColumn.Enabled = !isEmptyNewRow;
                }
                else
                {
                    this.tsmiDeleteColumn.Enabled = false;
                }

                this.contextMenuStrip1.Show(this.dgvColumns, e.Location);
            }
        }

        private void dgvColumns_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                this.DeleteRow();
            }
        }

        private void DeleteRow()
        {
            DataGridViewRow row = DataGridViewHelper.GetSelectedRow(this.dgvColumns);

            if (row != null && !row.IsNewRow)
            {
                this.dgvColumns.Rows.RemoveAt(row.Index);
            }
        }

        private void dgvColumns_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            this.dgvColumns.EndEdit();
            this.dgvColumns.CurrentCell = null;
            this.dgvColumns.Rows[e.RowIndex].Selected = true;
        }

        private void dgvColumns_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            this.AutoSizeColumns();
            this.dgvColumns.ClearSelection();
        }

        private void tsmiGenerateChangeScripts_Click(object sender, EventArgs e)
        {
            if (this.OnGenerateChangeScripts != null)
            {
                this.OnGenerateChangeScripts();
            }
        }
    }
}
