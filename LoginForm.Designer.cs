namespace ClockEnforcer
{
    partial class LoginForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.Button btnRequestOvertime;  // New Request Overtime button
        private System.Windows.Forms.TextBox statusTextBox; // New message box

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            txtUsername = new TextBox();
            txtPassword = new TextBox();
            lblUsername = new Label();
            lblPassword = new Label();
            btnLogin = new Button();
            btnRequestOvertime = new Button();
            statusTextBox = new TextBox();
            SuspendLayout();
            // 
            // txtUsername
            // 
            txtUsername.Location = new Point(200, 58);
            txtUsername.Margin = new Padding(5, 6, 5, 6);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(331, 31);
            txtUsername.TabIndex = 0;
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(200, 135);
            txtPassword.Margin = new Padding(5, 6, 5, 6);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(331, 31);
            txtPassword.TabIndex = 1;
            txtPassword.UseSystemPasswordChar = true;
            // 
            // lblUsername
            // 
            lblUsername.AutoSize = true;
            lblUsername.Location = new Point(83, 58);
            lblUsername.Margin = new Padding(5, 0, 5, 0);
            lblUsername.Name = "lblUsername";
            lblUsername.Size = new Size(95, 25);
            lblUsername.TabIndex = 2;
            lblUsername.Text = "Username:";
            // 
            // lblPassword
            // 
            lblPassword.AutoSize = true;
            lblPassword.Location = new Point(83, 135);
            lblPassword.Margin = new Padding(5, 0, 5, 0);
            lblPassword.Name = "lblPassword";
            lblPassword.Size = new Size(91, 25);
            lblPassword.TabIndex = 3;
            lblPassword.Text = "Password:";
            // 
            // btnLogin
            // 
            btnLogin.Location = new Point(200, 212);
            btnLogin.Margin = new Padding(5, 6, 5, 6);
            btnLogin.Name = "btnLogin";
            btnLogin.Size = new Size(125, 64);
            btnLogin.TabIndex = 4;
            btnLogin.Text = "Login";
            btnLogin.UseVisualStyleBackColor = true;
            btnLogin.Click += btnLogin_Click;
            // 
            // btnRequestOvertime
            // 
            btnRequestOvertime.Location = new Point(335, 212);
            btnRequestOvertime.Margin = new Padding(5, 6, 5, 6);
            btnRequestOvertime.Name = "btnRequestOvertime";
            btnRequestOvertime.Size = new Size(125, 64);
            btnRequestOvertime.TabIndex = 5;
            btnRequestOvertime.Text = "Request Overtime";
            btnRequestOvertime.UseVisualStyleBackColor = true;
            btnRequestOvertime.Click += btnRequestOvertime_Click;
            // 
            // statusTextBox
            // 
            statusTextBox.Location = new Point(83, 288);
            statusTextBox.Margin = new Padding(5, 6, 5, 6);
            statusTextBox.Multiline = true;
            statusTextBox.Name = "statusTextBox";
            statusTextBox.ReadOnly = true;
            statusTextBox.Size = new Size(497, 112);
            statusTextBox.TabIndex = 7;
            // 
            // LoginForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(667, 481);
            Controls.Add(statusTextBox);
            Controls.Add(btnRequestOvertime);
            Controls.Add(btnLogin);
            Controls.Add(lblPassword);
            Controls.Add(lblUsername);
            Controls.Add(txtPassword);
            Controls.Add(txtUsername);
            Margin = new Padding(5, 6, 5, 6);
            Name = "LoginForm";
            Text = "Clock Enforcer";
            ResumeLayout(false);
            PerformLayout();
        }  // End of InitializeComponent

        // Additional event handler for the Request Overtime button
        private void btnRequestOvertime_Click(object sender, EventArgs e)
        {
            OvertimeRequestForm overtimeForm = new OvertimeRequestForm();
            overtimeForm.ShowDialog();
        }
        #endregion
    }
}