using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClockEnforcer
{
    public partial class OvertimeRequestForm : Form
    {
        public OvertimeRequestForm()
        {
            InitializeComponent();
        }

        private void OvertimeRequestForm_Load(object sender, EventArgs e)
        {
            // Optionally initialize controls.
            txtNote.Text = "";
        }

        private async void btnSendRequest_Click(object sender, EventArgs e)
        {
            // Retrieve the AD username, note, and overtime hours.
            string adUsername = Environment.UserName;
            string note = txtNote.Text.Trim();
            decimal hours = numOvertimeHours.Value;

            // Validate inputs
            if (string.IsNullOrEmpty(note))
            {
                MessageBox.Show("Please enter a note for your overtime request.");
                return;
            }

            if (hours == 0 || hours == 9)
            {
                MessageBox.Show("Please enter a valid amount of overtime hours (not 0 or 9).");
                return;
            }

            // Use the overtime request service to send the request.
            OvertimeRequestService overtimeService = new OvertimeRequestService();
            System.Diagnostics.Debug.WriteLine($"adUsername: {adUsername}");
            System.Diagnostics.Debug.WriteLine($"Note: {note}");
            System.Diagnostics.Debug.WriteLine($"Hours: {hours}");

            bool success = await overtimeService.SendOvertimeRequestAsync(adUsername, note, hours);
            if (success)
            {
                MessageBox.Show("Overtime request sent successfully!");
                this.Close();
            }
            else
            {
                MessageBox.Show("Error sending overtime request. Please try again.");
            }
        }

        private void txtNote_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
