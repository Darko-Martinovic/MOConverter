namespace GuiTester
{
    partial class MainForm
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
            this.components = new System.ComponentModel.Container();
            this.grpConnection = new System.Windows.Forms.GroupBox();
            this.chkNewDatabase = new System.Windows.Forms.CheckBox();
            this.cmbDestination = new System.Windows.Forms.ComboBox();
            this.cmbDatabase = new System.Windows.Forms.ComboBox();
            this.cmbAuth = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.txtUserName = new System.Windows.Forms.TextBox();
            this.txtServer = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnConvertToMO = new System.Windows.Forms.Button();
            this.ProgressBar1 = new System.Windows.Forms.ProgressBar();
            this.txtCode = new System.Windows.Forms.TextBox();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.grpOptions = new System.Windows.Forms.GroupBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.rbExtendedProperies = new System.Windows.Forms.RadioButton();
            this.rbRange = new System.Windows.Forms.RadioButton();
            this.rbHash = new System.Windows.Forms.RadioButton();
            this.chkCopyData = new System.Windows.Forms.CheckBox();
            this.label9 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.txtTableContains = new System.Windows.Forms.TextBox();
            this.txtSchema = new System.Windows.Forms.TextBox();
            this.Timer1 = new System.Windows.Forms.Timer(this.components);
            this.label7 = new System.Windows.Forms.Label();
            this.lblOveral = new System.Windows.Forms.Label();
            this.grpConnection.SuspendLayout();
            this.grpOptions.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpConnection
            // 
            this.grpConnection.Controls.Add(this.chkNewDatabase);
            this.grpConnection.Controls.Add(this.cmbDestination);
            this.grpConnection.Controls.Add(this.cmbDatabase);
            this.grpConnection.Controls.Add(this.cmbAuth);
            this.grpConnection.Controls.Add(this.label6);
            this.grpConnection.Controls.Add(this.label5);
            this.grpConnection.Controls.Add(this.label10);
            this.grpConnection.Controls.Add(this.label3);
            this.grpConnection.Controls.Add(this.label2);
            this.grpConnection.Controls.Add(this.label1);
            this.grpConnection.Controls.Add(this.txtPassword);
            this.grpConnection.Controls.Add(this.txtUserName);
            this.grpConnection.Controls.Add(this.txtServer);
            this.grpConnection.Location = new System.Drawing.Point(12, 12);
            this.grpConnection.Name = "grpConnection";
            this.grpConnection.Size = new System.Drawing.Size(282, 315);
            this.grpConnection.TabIndex = 0;
            this.grpConnection.TabStop = false;
            this.grpConnection.Text = "Connection";
            // 
            // chkNewDatabase
            // 
            this.chkNewDatabase.AutoSize = true;
            this.chkNewDatabase.Checked = true;
            this.chkNewDatabase.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkNewDatabase.Location = new System.Drawing.Point(16, 231);
            this.chkNewDatabase.Name = "chkNewDatabase";
            this.chkNewDatabase.Size = new System.Drawing.Size(131, 17);
            this.chkNewDatabase.TabIndex = 54;
            this.chkNewDatabase.Text = "Create New Database";
            this.chkNewDatabase.UseVisualStyleBackColor = true;
            this.chkNewDatabase.CheckStateChanged += new System.EventHandler(this.chkNewDatabase_CheckStateChanged);
            // 
            // cmbDestination
            // 
            this.cmbDestination.BackColor = System.Drawing.SystemColors.Control;
            this.cmbDestination.Enabled = false;
            this.cmbDestination.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.cmbDestination.FormattingEnabled = true;
            this.cmbDestination.Location = new System.Drawing.Point(16, 280);
            this.cmbDestination.Name = "cmbDestination";
            this.cmbDestination.Size = new System.Drawing.Size(249, 21);
            this.cmbDestination.TabIndex = 52;
            this.cmbDestination.MouseClick += new System.Windows.Forms.MouseEventHandler(this.cmbDestination_MouseClick);
            // 
            // cmbDatabase
            // 
            this.cmbDatabase.BackColor = System.Drawing.SystemColors.Control;
            this.cmbDatabase.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.cmbDatabase.FormattingEnabled = true;
            this.cmbDatabase.Location = new System.Drawing.Point(16, 201);
            this.cmbDatabase.Name = "cmbDatabase";
            this.cmbDatabase.Size = new System.Drawing.Size(249, 21);
            this.cmbDatabase.TabIndex = 52;
            this.cmbDatabase.MouseClick += new System.Windows.Forms.MouseEventHandler(this.cmbDatabase_MouseClick);
            // 
            // cmbAuth
            // 
            this.cmbAuth.BackColor = System.Drawing.SystemColors.Control;
            this.cmbAuth.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.cmbAuth.FormattingEnabled = true;
            this.cmbAuth.ItemHeight = 13;
            this.cmbAuth.Items.AddRange(new object[] {
            "Windows authentication",
            "Sql server authentication"});
            this.cmbAuth.Location = new System.Drawing.Point(16, 74);
            this.cmbAuth.Name = "cmbAuth";
            this.cmbAuth.Size = new System.Drawing.Size(249, 21);
            this.cmbAuth.TabIndex = 49;
            this.cmbAuth.SelectedValueChanged += new System.EventHandler(this.cmbAuth_SelectedValueChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(15, 264);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(149, 13);
            this.label6.TabIndex = 43;
            this.label6.Text = "Destination Database Name  :";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(15, 185);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(130, 13);
            this.label5.TabIndex = 43;
            this.label5.Text = "Source Database Name  :";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(15, 143);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(59, 13);
            this.label10.TabIndex = 45;
            this.label10.Text = "Password :";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 101);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(67, 13);
            this.label3.TabIndex = 45;
            this.label3.Text = "User name  :";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 58);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(81, 13);
            this.label2.TabIndex = 46;
            this.label2.Text = "Authentication :";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(15, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(73, 13);
            this.label1.TabIndex = 47;
            this.label1.Text = "Server name :";
            // 
            // txtPassword
            // 
            this.txtPassword.BackColor = System.Drawing.SystemColors.Control;
            this.txtPassword.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.txtPassword.Location = new System.Drawing.Point(16, 159);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.PasswordChar = '*';
            this.txtPassword.Size = new System.Drawing.Size(247, 20);
            this.txtPassword.TabIndex = 51;
            // 
            // txtUserName
            // 
            this.txtUserName.BackColor = System.Drawing.SystemColors.Control;
            this.txtUserName.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.txtUserName.Location = new System.Drawing.Point(16, 117);
            this.txtUserName.Name = "txtUserName";
            this.txtUserName.Size = new System.Drawing.Size(247, 20);
            this.txtUserName.TabIndex = 50;
            // 
            // txtServer
            // 
            this.txtServer.BackColor = System.Drawing.SystemColors.Control;
            this.txtServer.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtServer.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.txtServer.Location = new System.Drawing.Point(16, 32);
            this.txtServer.Name = "txtServer";
            this.txtServer.Size = new System.Drawing.Size(247, 20);
            this.txtServer.TabIndex = 48;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(3, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(120, 13);
            this.label4.TabIndex = 44;
            this.label4.Text = "Indexes for primary keys";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.BackColor = System.Drawing.SystemColors.Control;
            this.btnCancel.Enabled = false;
            this.btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.Blue;
            this.btnCancel.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.btnCancel.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancel.Location = new System.Drawing.Point(310, 415);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(219, 23);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.TabStop = false;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = false;
            this.btnCancel.Click += new System.EventHandler(this.BtnCancelClick);
            // 
            // btnConvertToMO
            // 
            this.btnConvertToMO.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnConvertToMO.BackColor = System.Drawing.SystemColors.Control;
            this.btnConvertToMO.FlatAppearance.BorderColor = System.Drawing.Color.Blue;
            this.btnConvertToMO.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.btnConvertToMO.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btnConvertToMO.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnConvertToMO.Location = new System.Drawing.Point(45, 415);
            this.btnConvertToMO.Name = "btnConvertToMO";
            this.btnConvertToMO.Size = new System.Drawing.Size(219, 23);
            this.btnConvertToMO.TabIndex = 6;
            this.btnConvertToMO.TabStop = false;
            this.btnConvertToMO.Text = "Convert To Memory Optimized";
            this.btnConvertToMO.UseVisualStyleBackColor = false;
            this.btnConvertToMO.Click += new System.EventHandler(this.btnConvertToMO_Click);
            // 
            // ProgressBar1
            // 
            this.ProgressBar1.Location = new System.Drawing.Point(12, 377);
            this.ProgressBar1.Name = "ProgressBar1";
            this.ProgressBar1.Size = new System.Drawing.Size(587, 23);
            this.ProgressBar1.Step = 1;
            this.ProgressBar1.TabIndex = 7;
            this.ProgressBar1.Visible = false;
            // 
            // txtCode
            // 
            this.txtCode.Enabled = false;
            this.txtCode.Location = new System.Drawing.Point(12, 351);
            this.txtCode.Name = "txtCode";
            this.txtCode.Size = new System.Drawing.Size(138, 20);
            this.txtCode.TabIndex = 8;
            // 
            // txtDescription
            // 
            this.txtDescription.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDescription.Enabled = false;
            this.txtDescription.Location = new System.Drawing.Point(156, 351);
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.Size = new System.Drawing.Size(443, 20);
            this.txtDescription.TabIndex = 9;
            // 
            // grpOptions
            // 
            this.grpOptions.Controls.Add(this.panel1);
            this.grpOptions.Controls.Add(this.chkCopyData);
            this.grpOptions.Controls.Add(this.label9);
            this.grpOptions.Controls.Add(this.label8);
            this.grpOptions.Controls.Add(this.txtTableContains);
            this.grpOptions.Controls.Add(this.txtSchema);
            this.grpOptions.Location = new System.Drawing.Point(300, 12);
            this.grpOptions.Name = "grpOptions";
            this.grpOptions.Size = new System.Drawing.Size(299, 315);
            this.grpOptions.TabIndex = 10;
            this.grpOptions.TabStop = false;
            this.grpOptions.Text = "Options";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.rbExtendedProperies);
            this.panel1.Controls.Add(this.rbRange);
            this.panel1.Controls.Add(this.rbHash);
            this.panel1.Controls.Add(this.label4);
            this.panel1.Location = new System.Drawing.Point(10, 58);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(283, 113);
            this.panel1.TabIndex = 55;
            // 
            // rbExtendedProperies
            // 
            this.rbExtendedProperies.AutoSize = true;
            this.rbExtendedProperies.Location = new System.Drawing.Point(14, 93);
            this.rbExtendedProperies.Name = "rbExtendedProperies";
            this.rbExtendedProperies.Size = new System.Drawing.Size(257, 17);
            this.rbExtendedProperies.TabIndex = 0;
            this.rbExtendedProperies.TabStop = true;
            this.rbExtendedProperies.Text = "Use what is written in the table extended property";
            this.rbExtendedProperies.UseVisualStyleBackColor = true;
            // 
            // rbRange
            // 
            this.rbRange.AutoSize = true;
            this.rbRange.Location = new System.Drawing.Point(14, 51);
            this.rbRange.Name = "rbRange";
            this.rbRange.Size = new System.Drawing.Size(124, 17);
            this.rbRange.TabIndex = 0;
            this.rbRange.TabStop = true;
            this.rbRange.Text = "Use RANGE indexes";
            this.rbRange.UseVisualStyleBackColor = true;
            // 
            // rbHash
            // 
            this.rbHash.AutoSize = true;
            this.rbHash.Checked = true;
            this.rbHash.Location = new System.Drawing.Point(14, 16);
            this.rbHash.Name = "rbHash";
            this.rbHash.Size = new System.Drawing.Size(116, 17);
            this.rbHash.TabIndex = 0;
            this.rbHash.TabStop = true;
            this.rbHash.Text = "Use HASH indexes";
            this.rbHash.UseVisualStyleBackColor = true;
            // 
            // chkCopyData
            // 
            this.chkCopyData.AutoSize = true;
            this.chkCopyData.Checked = true;
            this.chkCopyData.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCopyData.Location = new System.Drawing.Point(10, 19);
            this.chkCopyData.Name = "chkCopyData";
            this.chkCopyData.Size = new System.Drawing.Size(287, 17);
            this.chkCopyData.TabIndex = 54;
            this.chkCopyData.Text = "Also copy table data to the new memory optimized table";
            this.chkCopyData.UseVisualStyleBackColor = true;
            this.chkCopyData.CheckStateChanged += new System.EventHandler(this.chkNewDatabase_CheckStateChanged);
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(7, 235);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(83, 13);
            this.label9.TabIndex = 45;
            this.label9.Text = "Table contains :";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(7, 196);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(95, 13);
            this.label8.TabIndex = 45;
            this.label8.Text = "Schema contains :";
            // 
            // txtTableContains
            // 
            this.txtTableContains.BackColor = System.Drawing.SystemColors.Control;
            this.txtTableContains.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.txtTableContains.Location = new System.Drawing.Point(10, 251);
            this.txtTableContains.Name = "txtTableContains";
            this.txtTableContains.Size = new System.Drawing.Size(247, 20);
            this.txtTableContains.TabIndex = 50;
            // 
            // txtSchema
            // 
            this.txtSchema.BackColor = System.Drawing.SystemColors.Control;
            this.txtSchema.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.txtSchema.Location = new System.Drawing.Point(10, 212);
            this.txtSchema.Name = "txtSchema";
            this.txtSchema.Size = new System.Drawing.Size(247, 20);
            this.txtSchema.TabIndex = 50;
            // 
            // Timer1
            // 
            this.Timer1.Tick += new System.EventHandler(this.Timer1_Tick_1);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(12, 330);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(49, 13);
            this.label7.TabIndex = 44;
            this.label7.Text = "Overall : ";
            // 
            // lblOveral
            // 
            this.lblOveral.AutoSize = true;
            this.lblOveral.Location = new System.Drawing.Point(67, 330);
            this.lblOveral.Name = "lblOveral";
            this.lblOveral.Size = new System.Drawing.Size(0, 13);
            this.lblOveral.TabIndex = 44;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(233)))), ((int)(((byte)(216)))));
            this.ClientSize = new System.Drawing.Size(611, 450);
            this.Controls.Add(this.grpOptions);
            this.Controls.Add(this.txtDescription);
            this.Controls.Add(this.txtCode);
            this.Controls.Add(this.ProgressBar1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnConvertToMO);
            this.Controls.Add(this.lblOveral);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.grpConnection);
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Let\'s Convert Together to In-Memory OLTP";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.grpConnection.ResumeLayout(false);
            this.grpConnection.PerformLayout();
            this.grpOptions.ResumeLayout(false);
            this.grpOptions.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox grpConnection;
        private System.Windows.Forms.ComboBox cmbDatabase;
        private System.Windows.Forms.ComboBox cmbAuth;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.TextBox txtUserName;
        private System.Windows.Forms.TextBox txtServer;
        private System.Windows.Forms.CheckBox chkNewDatabase;
        private System.Windows.Forms.ComboBox cmbDestination;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnConvertToMO;
        internal System.Windows.Forms.ProgressBar ProgressBar1;
        internal System.Windows.Forms.TextBox txtCode;
        internal System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.GroupBox grpOptions;
        internal System.Windows.Forms.Timer Timer1;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label lblOveral;
        private System.Windows.Forms.CheckBox chkCopyData;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox txtTableContains;
        private System.Windows.Forms.TextBox txtSchema;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.RadioButton rbExtendedProperies;
        private System.Windows.Forms.RadioButton rbRange;
        private System.Windows.Forms.RadioButton rbHash;
        private System.Windows.Forms.Label label10;
    }
}

