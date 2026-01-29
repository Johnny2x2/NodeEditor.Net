namespace SharedCommon
{
    partial class ControlNodeEditor
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            NodeEditor.DrawInfo drawInfo1 = new NodeEditor.DrawInfo();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.panel = new System.Windows.Forms.Panel();
            this.nodesControl = new NodeEditor.NodeControl();
            this.propertyGrid = new System.Windows.Forms.PropertyGrid();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panel.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.panel);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.propertyGrid);
            this.splitContainer1.Size = new System.Drawing.Size(933, 746);
            this.splitContainer1.SplitterDistance = 567;
            this.splitContainer1.SplitterWidth = 6;
            this.splitContainer1.TabIndex = 0;
            // 
            // panel
            // 
            this.panel.AutoScroll = true;
            this.panel.Controls.Add(this.nodesControl);
            this.panel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel.Location = new System.Drawing.Point(0, 0);
            this.panel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.panel.Name = "panel";
            this.panel.Size = new System.Drawing.Size(567, 746);
            this.panel.TabIndex = 0;
            // 
            // nodesControl
            // 
            this.nodesControl.AutoScroll = true;
            this.nodesControl.BackgroundImage = global::SampleCommon.Properties.Resources.grid;
            this.nodesControl.CustomDrawInfo = drawInfo1;
            this.nodesControl.Location = new System.Drawing.Point(0, 0);
            this.nodesControl.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.nodesControl.Name = "nodesControl";
            this.nodesControl.PreferFastRendering = false;
            this.nodesControl.Size = new System.Drawing.Size(7500, 7692);
            this.nodesControl.TabIndex = 0;
            this.nodesControl.Zoom = 1F;
            // 
            // propertyGrid
            // 
            this.propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGrid.Location = new System.Drawing.Point(0, 0);
            this.propertyGrid.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.propertyGrid.Name = "propertyGrid";
            this.propertyGrid.Size = new System.Drawing.Size(360, 746);
            this.propertyGrid.TabIndex = 1;
            // 
            // ControlNodeEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer1);
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "ControlNodeEditor";
            this.Size = new System.Drawing.Size(933, 746);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.panel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Panel panel;
        public NodeEditor.NodeControl nodesControl;
        public System.Windows.Forms.PropertyGrid propertyGrid;
    }
}
