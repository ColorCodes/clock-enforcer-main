namespace ClockEnforcer
{
    partial class OvertimeRequestForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label lblNote;
        private System.Windows.Forms.TextBox txtNote;
        private System.Windows.Forms.Button btnSendRequest;
        private System.Windows.Forms.NumericUpDown numOvertimeHours;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            lblNote = new Label();
            txtNote = new TextBox();
            btnSendRequest = new Button();
            numOvertimeHours = new NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)numOvertimeHours).BeginInit();
            SuspendLayout();
            // 
            // lblNote
            // 
            lblNote.AutoSize = true;
            lblNote.Location = new Point(12, 15);
            lblNote.Name = "lblNote";
            lblNote.Size = new Size(430, 15);
            lblNote.TabIndex = 0;
            lblNote.Text = "Overtime Note: (Make sure you provide hours and times for requested overtime)";
            // 
            // txtNote
            // 
            txtNote.Location = new Point(15, 104);
            txtNote.Multiline = true;
            txtNote.Name = "txtNote";
            txtNote.Size = new Size(647, 100);
            txtNote.TabIndex = 1;
            txtNote.TextChanged += txtNote_TextChanged;
            // 
            // btnSendRequest
            // 
            btnSendRequest.Location = new Point(15, 210);
            btnSendRequest.Name = "btnSendRequest";
            btnSendRequest.Size = new Size(119, 38);
            btnSendRequest.TabIndex = 3;
            btnSendRequest.Text = "Send Request";
            btnSendRequest.UseVisualStyleBackColor = true;
            btnSendRequest.Click += btnSendRequest_Click;
            // 
            // numOvertimeHours
            // 
            numOvertimeHours.DecimalPlaces = 1;
            numOvertimeHours.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numOvertimeHours.Location = new Point(15, 61);
            numOvertimeHours.Maximum = new decimal(new int[] { 24, 0, 0, 0 });
            numOvertimeHours.Name = "numOvertimeHours";
            numOvertimeHours.Size = new Size(120, 23);
            numOvertimeHours.TabIndex = 2;
            // 
            // OvertimeRequestForm
            // 
            ClientSize = new Size(696, 262);
            Controls.Add(btnSendRequest);
            Controls.Add(numOvertimeHours);
            Controls.Add(txtNote);
            Controls.Add(lblNote);
            Name = "OvertimeRequestForm";
            Text = "Overtime Request";
            Load += OvertimeRequestForm_Load;
            ((System.ComponentModel.ISupportInitialize)numOvertimeHours).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
