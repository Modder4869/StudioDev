using System.Configuration;
using System.Windows.Forms;

namespace AssetStudio.GUI
{
    partial class AssetBrowser
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            tableLayoutPanel2 = new TableLayoutPanel();
            loadAssetMap = new Button();
            clear = new Button();
            loadSelected = new Button();
            exportSelected = new Button();
            assetDataGridView = new DataGridView();
            tableLayoutPanel1 = new TableLayoutPanel();
            tableLayoutPanel3 = new TableLayoutPanel();
            sourceTextBox = new TextBox();
            pathTextBox = new TextBox();
            nameTextBox = new TextBox();
            containerTextBox = new TextBox();
            typeTextBox = new TextBox();
            tableLayoutPanel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)assetDataGridView).BeginInit();
            tableLayoutPanel1.SuspendLayout();
            tableLayoutPanel3.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanel2.ColumnCount = 4;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle());
            tableLayoutPanel2.Controls.Add(loadAssetMap, 0, 0);
            tableLayoutPanel2.Controls.Add(clear, 1, 0);
            tableLayoutPanel2.Controls.Add(loadSelected, 2, 0);
            tableLayoutPanel2.Controls.Add(exportSelected, 3, 0);
            tableLayoutPanel2.Location = new System.Drawing.Point(3, 4);
            tableLayoutPanel2.Margin = new Padding(3, 4, 3, 4);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 1;
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel2.Size = new System.Drawing.Size(593, 39);
            tableLayoutPanel2.TabIndex = 3;
            // 
            // loadAssetMap
            // 
            loadAssetMap.Dock = DockStyle.Fill;
            loadAssetMap.Location = new System.Drawing.Point(3, 4);
            loadAssetMap.Margin = new Padding(3, 4, 3, 4);
            loadAssetMap.Name = "loadAssetMap";
            loadAssetMap.Size = new System.Drawing.Size(132, 31);
            loadAssetMap.TabIndex = 0;
            loadAssetMap.Text = "Load AssetMap";
            loadAssetMap.UseVisualStyleBackColor = true;
            loadAssetMap.Click += loadAssetMap_Click;
            // 
            // clear
            // 
            clear.Dock = DockStyle.Fill;
            clear.Location = new System.Drawing.Point(141, 4);
            clear.Margin = new Padding(3, 4, 3, 4);
            clear.Name = "clear";
            clear.Size = new System.Drawing.Size(63, 31);
            clear.TabIndex = 1;
            clear.Text = "Clear";
            clear.UseVisualStyleBackColor = true;
            clear.Click += clear_Click;
            // 
            // loadSelected
            // 
            loadSelected.Dock = DockStyle.Fill;
            loadSelected.Location = new System.Drawing.Point(210, 4);
            loadSelected.Margin = new Padding(3, 4, 3, 4);
            loadSelected.Name = "loadSelected";
            loadSelected.Size = new System.Drawing.Size(132, 31);
            loadSelected.TabIndex = 2;
            loadSelected.Text = "Load Selected";
            loadSelected.UseVisualStyleBackColor = true;
            loadSelected.Click += loadSelected_Click;
            // 
            // exportSelected
            // 
            exportSelected.Dock = DockStyle.Fill;
            exportSelected.Location = new System.Drawing.Point(348, 4);
            exportSelected.Margin = new Padding(3, 4, 3, 4);
            exportSelected.Name = "exportSelected";
            exportSelected.Size = new System.Drawing.Size(242, 31);
            exportSelected.TabIndex = 3;
            exportSelected.Text = "Export Selected";
            exportSelected.UseVisualStyleBackColor = true;
            exportSelected.Click += exportSelected_Click;
            // 
            // assetDataGridView
            // 
            assetDataGridView.AllowUserToAddRows = false;
            assetDataGridView.AllowUserToDeleteRows = false;
            assetDataGridView.AllowUserToResizeRows = false;
            assetDataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            assetDataGridView.Dock = DockStyle.Fill;
            assetDataGridView.Location = new System.Drawing.Point(3, 98);
            assetDataGridView.Margin = new Padding(3, 4, 3, 4);
            assetDataGridView.Name = "assetDataGridView";
            assetDataGridView.ReadOnly = true;
            assetDataGridView.RowTemplate.Height = 25;
            assetDataGridView.Size = new System.Drawing.Size(593, 351);
            assetDataGridView.TabIndex = 2;
            assetDataGridView.VirtualMode = true;
            assetDataGridView.CellValueNeeded += AssetDataGridView_CellValueNeeded;
            assetDataGridView.ColumnHeaderMouseClick += AssetListView_ColumnHeaderMouseClick;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(assetDataGridView, 0, 2);
            tableLayoutPanel1.Controls.Add(tableLayoutPanel2, 0, 0);
            tableLayoutPanel1.Controls.Add(tableLayoutPanel3, 0, 1);
            tableLayoutPanel1.Location = new System.Drawing.Point(14, 16);
            tableLayoutPanel1.Margin = new Padding(3, 4, 3, 4);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 47F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 47F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle());
            tableLayoutPanel1.Size = new System.Drawing.Size(599, 444);
            tableLayoutPanel1.TabIndex = 3;
            // 
            // tableLayoutPanel3
            // 
            tableLayoutPanel3.ColumnCount = 5;
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanel3.Controls.Add(sourceTextBox, 0, 0);
            tableLayoutPanel3.Controls.Add(pathTextBox, 0, 0);
            tableLayoutPanel3.Controls.Add(nameTextBox, 0, 0);
            tableLayoutPanel3.Controls.Add(containerTextBox, 0, 0);
            tableLayoutPanel3.Controls.Add(typeTextBox, 4, 0);
            tableLayoutPanel3.Dock = DockStyle.Fill;
            tableLayoutPanel3.Location = new System.Drawing.Point(3, 51);
            tableLayoutPanel3.Margin = new Padding(3, 4, 3, 4);
            tableLayoutPanel3.Name = "tableLayoutPanel3";
            tableLayoutPanel3.RowCount = 1;
            tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel3.Size = new System.Drawing.Size(593, 39);
            tableLayoutPanel3.TabIndex = 4;
            // 
            // sourceTextBox
            // 
            sourceTextBox.Dock = DockStyle.Fill;
            sourceTextBox.Location = new System.Drawing.Point(239, 4);
            sourceTextBox.Margin = new Padding(3, 4, 3, 4);
            sourceTextBox.Name = "sourceTextBox";
            sourceTextBox.PlaceholderText = "Source";
            sourceTextBox.Size = new System.Drawing.Size(112, 27);
            sourceTextBox.TabIndex = 6;
            sourceTextBox.KeyPress += SourceTextBox_KeyPress;
            // 
            // pathTextBox
            // 
            pathTextBox.Dock = DockStyle.Fill;
            pathTextBox.Location = new System.Drawing.Point(357, 4);
            pathTextBox.Margin = new Padding(3, 4, 3, 4);
            pathTextBox.Name = "pathTextBox";
            pathTextBox.PlaceholderText = "PathID";
            pathTextBox.Size = new System.Drawing.Size(112, 27);
            pathTextBox.TabIndex = 7;
            pathTextBox.KeyPress += PathTextBox_KeyPress;
            // 
            // nameTextBox
            // 
            nameTextBox.Dock = DockStyle.Fill;
            nameTextBox.Location = new System.Drawing.Point(3, 4);
            nameTextBox.Margin = new Padding(3, 4, 3, 4);
            nameTextBox.Name = "nameTextBox";
            nameTextBox.PlaceholderText = "Name";
            nameTextBox.Size = new System.Drawing.Size(112, 27);
            nameTextBox.TabIndex = 4;
            nameTextBox.KeyPress += NameTextBox_KeyPress;
            // 
            // containerTextBox
            // 
            containerTextBox.Dock = DockStyle.Fill;
            containerTextBox.Location = new System.Drawing.Point(121, 4);
            containerTextBox.Margin = new Padding(3, 4, 3, 4);
            containerTextBox.Name = "containerTextBox";
            containerTextBox.PlaceholderText = "Container";
            containerTextBox.Size = new System.Drawing.Size(112, 27);
            containerTextBox.TabIndex = 5;
            containerTextBox.KeyPress += ContainerTextBox_KeyPress;
            // 
            // typeTextBox
            // 
            typeTextBox.Dock = DockStyle.Fill;
            typeTextBox.Location = new System.Drawing.Point(475, 4);
            typeTextBox.Margin = new Padding(3, 4, 3, 4);
            typeTextBox.Name = "typeTextBox";
            typeTextBox.PlaceholderText = "Type";
            typeTextBox.Size = new System.Drawing.Size(115, 27);
            typeTextBox.TabIndex = 8;
            typeTextBox.KeyPress += TypeTextBox_KeyPress;
            // 
            // AssetBrowser
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(626, 476);
            Controls.Add(tableLayoutPanel1);
            Margin = new Padding(3, 4, 3, 4);
            Name = "AssetBrowser";
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Asset Browser";
            FormClosing += AssetBrowser_FormClosing;
            tableLayoutPanel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)assetDataGridView).EndInit();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel3.ResumeLayout(false);
            tableLayoutPanel3.PerformLayout();
            ResumeLayout(false);
        }



        #endregion

        private TableLayoutPanel tableLayoutPanel2;
        private Button loadAssetMap;
        private Button clear;
        private Button loadSelected;
        private Button exportSelected;
        private DataGridView assetDataGridView;
        private TableLayoutPanel tableLayoutPanel1;
        private TableLayoutPanel tableLayoutPanel3;
        private TextBox sourceTextBox;
        private TextBox pathTextBox;
        private TextBox nameTextBox;
        private TextBox containerTextBox;
        private TextBox typeTextBox;
    }
}