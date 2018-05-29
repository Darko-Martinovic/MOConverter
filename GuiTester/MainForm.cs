using System;
using System.Data;
using System.Windows.Forms;
using System.Threading;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Converter.Extension;
using Converter.Interface;
using Converter.Options;
using System.Text;
using System.IO;
using System.Diagnostics;
using Converter.Inputs;
using Converter.DataAccess;
using Converter.Enums;

namespace GuiTester
{
    public partial class MainForm : Form, Converter.Interface.ILog
    {
        public MainForm()
        {
            InitializeComponent();
        }



        /// <summary>
        /// Load the main form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            cmbAuth.SelectedIndex = 0;
            //load the configuration
            _cnf = new Converter.Configuration.Configuration();
            _cnf.LoadConfig();
            txtServer.Focus();
        }


        /// <summary>
        /// button click handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnConvertToMO_Click(object sender, EventArgs e)
        {
            if (txtServer.Text.Equals(string.Empty))
            {
                txtServer.Focus();
                MessageBox.Show(@"Please enter the valid server name",
                                 @"Error",
                                 MessageBoxButtons.OK,
                                 MessageBoxIcon.Error);
                return;
            }
            if (cmbAuth.SelectedIndex == -1)
            {
                cmbAuth.Focus();
                MessageBox.Show(@"Please choose authentication type!",
                                @"Error", 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Error);
                return;
            }
            if (cmbDatabase.SelectedIndex == -1)
            {
                cmbDatabase.Focus();
                MessageBox.Show(@"Please choose database",
                                @"Error", 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Error);
                return;
            }
            if (chkNewDatabase.Checked == false && cmbDatabase.Text.Equals(cmbDestination.Text))
            {
                MessageBox.Show(@"Source and destination are the same. Not allowed!",
                                @"Error", 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Error);
                return;

            }

            //Create inputs
            _i = new Inputs
            {
                ServerName = txtServer.Text,
                DatabaseName = cmbDatabase.Text,
                InMemoryDataBaseName = cmbDestination.Text,
                UserName = txtUserName.Text,
                Password = txtPassword.Text,
                IsWindows = cmbAuth.SelectedIndex == 0,
                CreateNew = chkNewDatabase.Checked
            };
            if (_i.CreateNew)
                _i.InMemoryDataBaseName = $"{_i.DatabaseName}_InMem";
            //create options
            _o = new Options {CopyData = chkCopyData.Checked};
            if (rbHash.Checked)
                _o.UseHashIndexes = Options.IndexDecision.Hash;
            else if (rbRange.Checked)
                _o.UseHashIndexes = Options.IndexDecision.Range;
            else
                _o.UseHashIndexes = Options.IndexDecision.ExtendedPropery;

            //o.DropOnDestination = chkDropOnDestination.Checked;
            _o.SchemaContains = txtSchema.Text.Trim();
            _o.TableContains = txtTableContains.Text.Trim();

            Server server = null;
            try
            {
                ServerConnection cnn = new ServerConnection(_i.ServerName);
                cnn.Connect();
                server = new Server(cnn);

            }
            catch (Exception ex)
            {
                MessageBox.Show($@"I'm unable to connect to the server {_i.ServerName}  {ex.Message}",
                                @"Error",
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Error);
                return;
            }
            bool isSysAdmin = ((int)DataAccess.ExecuteScalar(DataAccess.GetConnectionString(
                    txtServer.Text,
                    "master",
                    cmbAuth.SelectedIndex == 0 ,
                    txtUserName.Text, txtPassword.Text), $@" SELECT IS_SRVROLEMEMBER ('sysadmin') ") == 1) ;

            if ( isSysAdmin== false)
            {
                MessageBox.Show(@"You should connect as a member of sysadmin fixed server role",
                                @"Error", 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Error);
                return;
            }



            if (new Version(server.VersionString) < new Version(CServerVersion))
            {
                MessageBox.Show(@"The server has to be SQL2016 SP2 or higher",
                                @"Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                return;
            }

            if (server.Databases[_i.DatabaseName] == null)
            {
                MessageBox.Show(@"Choose the database!",
                                @"Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                cmbDatabase.SelectedItem = null;
                return;

            }

            if (server.Databases[_i.DatabaseName].HasMemoryOptimizedObjects)
            {
                MessageBox.Show(@"The source database contains Memory Optimized FileGroup. It is not allowed!",
                                @"Error", 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Error);
                return;
            }
            var error = "";


            if (_i.CreateNew)
            {
                if (MessageBox.Show($@"You choose to create a new database {_i.DatabaseName}_InMem. Are you sure?",
                                    @"Question", 
                                    MessageBoxButtons.YesNo, 
                                    MessageBoxIcon.Question) == DialogResult.No)
                {
                    return;
                }
                if (Converter.Utility.CreateDatabase.Create(server, _i.DatabaseName + "_InMem", ref error, _cnf.FileGroupName, _cnf.FileName, _cnf.MoPath) == false)
                {
                    MessageBox.Show($@"An error occurs while creating the database! {Environment.NewLine} { error}",
                                    @"Error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                if (MessageBox.Show($@"You choose to convert the database {_i.DatabaseName.ToUpper()} to In-Mem  {
                                _i.InMemoryDataBaseName.ToUpper()
                            }.
                                                        Are you sure?",
                                    @"Question", 
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question) == DialogResult.No)
                {
                    return;
                }
                if (Converter.Utility.CreateDatabase.Create(server, _i.InMemoryDataBaseName, ref error,
                        _cnf.FileGroupName, _cnf.FileName, _cnf.MoPath) == false)
                {
                    MessageBox.Show(@"An error occurs while creating the database!",
                                    @"Error",
                                    MessageBoxButtons.OK, 
                                    MessageBoxIcon.Error);
                    return;
                }


            }


            ProgressBar1.Visible = true;
            ProgressBar1.Minimum = 1;
            ProgressBar1.Maximum = server.Databases[_i.DatabaseName].Tables.Count;
            ProgressBar1.Step = 1;
            ProgressBar1.Show();

            grpConnection.Enabled = false;
            grpOptions.Enabled = false;
            btnConvertToMO.Enabled = false;
            btnCancel.Enabled = true;
         


            _t1 = DateTime.Now;
            _mainObr = new Thread(StartConversion);
            Timer1.Enabled = true;
            Timer1.Interval = 500;
            _mainObr.Start();
            


        }

        #region " Fields & constants "

        private bool _isError;
        private Thread _mainObr; 
        private Inputs _i;
        private Options _o ;

        //
        // https://support.microsoft.com/en-us/help/3177312/sql-server-2016-build-versions
        // SQL Server 2016 SP2
        private const string CServerVersion = "13.0.5026.0";

        //
        // New features available with SQL Server 2017
        // 
        private const string CNewFeaturesVersion = "14.0.1000.169";


        private Converter.Configuration.Configuration _cnf = null;
        private DateTime _t1;
        private DateTime _t2;
        private bool _success = false;
        #endregion

        #region " Start the conversion "

        /// <summary>
        /// The conversion process starts here
        /// </summary>
        private void StartConversion()
        {
            var cnn = new ServerConnection(_i.ServerName);
            cnn.Connect();
            var server = new Server(cnn);

            var db = server.Databases[_i.DatabaseName];
            // Connect to the In-Memory Database
            var cnnInMem = new ServerConnection(_i.ServerName);
            cnnInMem.Connect();
            var serverInMem = new Server(cnnInMem);
            var dbInMemory = serverInMem.Databases[_i.InMemoryDataBaseName];

            // new features available starting with SQL Server 2017
            var enumFeatures = SqlServerMoFeatures.SqlServer2016;
            if (new Version(server.VersionString) >= new Version(CNewFeaturesVersion))
            {
                enumFeatures = SqlServerMoFeatures.SqlServer2017;
            }
            _success = db.SwitchToMo(
                                    dbInMemory, 
                                    (ILog)this, 
                                    _cnf, 
                                    _o,
                                    enumFeatures);

      
            cnnInMem.Disconnect();
            cnn.Disconnect();
            cnn = null;
            cnnInMem = null;
            server = null;
            db = null;
            serverInMem = null;
            dbInMemory = null;

            //for (int i = 0; i <= GC.MaxGeneration; i++)
            //{
            //    GC.Collect(i, GCCollectionMode.Forced, true, true);
            //    GC.WaitForPendingFinalizers();
            //}



        }
        #endregion

        #region " Callback "

        private delegate void SetTextCallback(string text);

        private delegate void SetProgressBarValueCallBack(int text);

        public void SetTextCode(string text)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (txtCode.InvokeRequired)
            {
                var d = new SetTextCallback(SetTextCode);
                Invoke(d, text);
                d = null;
            }
            else
            {
                txtCode.Text = text;
            }
        }
        public void SetTextDescription(string text)
        {
            if (txtDescription.InvokeRequired)
            {
                var d = new SetTextCallback(SetTextDescription);
                Invoke(d, text);
                d = null;
            }
            else
            {
                txtDescription.Text = text;
            }
        }


        public void SetLabelText(string text)
        {
            if (lblOveral.InvokeRequired)
            {
                var d = new SetTextCallback(SetLabelText);
                Invoke(d, text);
                d = null;
            }
            else
            {
                lblOveral.Text = text;
            }
        }

        private void SetProgresBarValue(int text)
        {
            if (ProgressBar1.InvokeRequired)
            {
                var d = new SetProgressBarValueCallBack(SetProgresBarValue);
                Invoke(d, text);
                d = null;
            }
            else
            {
                if (text > ProgressBar1.Maximum)
                    text = ProgressBar1.Maximum;
                ProgressBar1.Value = text;
            }
        }

        private void SetProgressBarMaxValue(int text)
        {
            if (ProgressBar1.InvokeRequired)
            {
                var d = new SetProgressBarValueCallBack(SetProgressBarMaxValue);
                Invoke(d, text);
                d = null;
            }
            else
            {
                ProgressBar1.Maximum = text;
            }
        }
        #endregion

        #region " The interface( ILog )implementation "

       
        void ILog.SetValue(int text)
        {
            SetProgresBarValue(text);
        }

        void ILog.SetMaxValue(int text)
        {
            SetProgressBarMaxValue(text);
        }

        void ILog.SetOverall(string text)
        {
            SetLabelText(text);
        }




        void ILog.Log(string text, string txt)
        {

            SetTextCode(text);
            SetTextDescription(txt);

        }
        private StringBuilder _sb;
        void ILog.LogWarErr(string text, string txt)
        {
            if (_sb == null)
            {
                _sb = new StringBuilder();
                _sb.Append(
                    $"****Summary report - converting  {_i.DatabaseName} to IN-MEM OLTP {_i.InMemoryDataBaseName} on server {_i.ServerName}\r\n");
                _sb.Append("\r\n");
                _sb.Append("****List of warnings and errors");
                _sb.Append("\r\n");
                _sb.Append("\r\n");
            }
                

            _sb.Append($"{text} {txt}\r\n");
            _sb.Append(Environment.NewLine);
        }

        int ILog.CurrentItem { get; set; }

        int ILog.Counter { get; set; }

        #endregion

        #region " Cancel the conversation process "

        private bool _isAborted = false;
        private void BtnCancelClick(object sender, EventArgs e)
        {
            if (MessageBox.Show(@"You choose to stop conversation process.
                                     Are you sure?", 
                                @"Question", 
                                MessageBoxButtons.YesNo, 
                                MessageBoxIcon.Question) == DialogResult.No)
            {
                return;
            }

            if (_mainObr != null)
            {
                try
                {
                    _mainObr.Abort();
                    while (_mainObr.ThreadState != System.Threading.ThreadState.Aborted)
                    {
                        Application.DoEvents();
                        _mainObr.Abort();
                    }

                }
                catch (Exception )
                {
                    if(Debugger.IsAttached)
                        Debugger.Break();
                }
            }
            _isAborted = true;
            Timer1.Enabled = false;
            lblOveral.Text = string.Empty;
            txtCode.Text = string.Empty;
            txtDescription.Text = string.Empty;

            btnCancel.Enabled = false;
            ProgressBar1.Visible = false;
            btnConvertToMO.Enabled = true;
            grpConnection.Enabled = true;
            grpOptions.Enabled = true;
            _sb = null;

        }

        #endregion

        #region " The timer controls the working thread "
        private void Timer1_Tick_1(object sender, EventArgs e)
        {
            if (_mainObr == null || _mainObr.IsAlive)
            {
                return;
            }
            try
            {
                Timer1.Enabled = false;
                _mainObr.Join();
                _mainObr = null;

            }
            catch (Exception)
            {
                if(Debugger.IsAttached)
                    Debugger.Break();
            }
            _t2 = DateTime.Now;

            ProgressBar1.Visible = false;
            btnCancel.Enabled = false;
            btnConvertToMO.Enabled = true;
            grpOptions.Enabled = true;
            grpConnection.Enabled = true;
            if (_success)
            {
                TimeSpan ts = _t2 - _t1;
                MessageBox.Show($@"Switching to in-memory OLTP finished successfully. Elapsed time {ts:dd\.hh\:mm\:ss}", 
                                @"Info", 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Information);
            }
            SetLabelText("");
            if (_isAborted == false)
            {
                var fileName = $"{_i.DatabaseName}{DateTime.Now:yyyy_mm_dd_HH_mm_ss}.txt";
                if (File.Exists(fileName))
                    File.Delete(fileName);
                File.WriteAllText(fileName, _sb.ToString());
                // start notepad and disply the configuration
                Process.Start(fileName);
            }
            else
            {
                _isAborted = false;
            }

            SetLabelText("");
            _cnf.LoadConfig();
            _sb = null;


        }
        #endregion
        
        #region " Inputs manipulations "

        private void chkNewDatabase_CheckStateChanged(object sender, EventArgs e)
        {
            if (chkNewDatabase.Checked)
            {
                cmbDestination.Enabled = false;
                cmbDestination.Text = string.Empty;
            }
            else
            {
                cmbDestination.Enabled = true;
            }


        }

        private void cmbDestination_MouseClick(object sender, MouseEventArgs e)
        {
            if (cmbDestination.Enabled == false)
                return;
            if (txtServer.Text.Trim().Equals(string.Empty))
            {
                txtServer.Focus();
                MessageBox.Show(@"Please enter the valid server name",
                                 @"Error",
                                 MessageBoxButtons.OK,
                                 MessageBoxIcon.Error);
                return;

            }

            BindDataBases(cmbDestination);
        }


        private void cmbAuth_SelectedValueChanged(object sender, EventArgs e)
        {
            if (cmbAuth.SelectedItem != null)
            {
                if (_isError)
                    _isError = false;

                if (cmbAuth.SelectedIndex == 0)
                {
                    txtUserName.Text = "";
                    txtPassword.Text = "";
                    txtUserName.Enabled = false;
                    txtPassword.Enabled = false;
                }
                else if (cmbAuth.SelectedIndex == 1)
                {
                    txtUserName.Enabled = true;
                    txtPassword.Enabled = true;
                    txtUserName.Select();
                }
            }
        }

        private void cmbDatabase_MouseClick(object sender, MouseEventArgs e)
        {
            if (txtServer.Text.Trim().Equals(string.Empty))
            {
                txtServer.Focus();
                MessageBox.Show(@"Please enter the valid server name",
                                 @"Error",
                                 MessageBoxButtons.OK,
                                 MessageBoxIcon.Error);
                return;

            }
            if (cmbAuth.SelectedIndex != 0 && (txtUserName.Text.Trim().Equals(string.Empty) ||
                                               txtPassword.Text.Trim().Equals(string.Empty)))
            {
                MessageBox.Show(@"Please enter userName and password",
                                @"Error", 
                                 MessageBoxButtons.OK, 
                                 MessageBoxIcon.Error);
                if (_isError)
                    _isError = false;
                return;
            }
            if (_isError == false)
            {
                BindDataBases(cmbDatabase);
            }
        }

        private void BindDataBases(ComboBox cmb)
        {
            cmb.Items.Clear();
            var ds = DataAccess.GetDataSet(
                DataAccess.GetConnectionString(
                    txtServer.Text,
                    "master",
                    cmbAuth.SelectedIndex == 0 ,
                    txtUserName.Text, txtPassword.Text), @"SELECT name 
                                                                FROM sys.databases
                                                                WHERE state = 0 
                                                                    AND is_read_only = 0 
                                                                ORDER BY name", null, out var error);
            if (error.Equals(string.Empty) == false)
            {
                _isError = true;
                MessageBox.Show($@"Error binding database information : {error}",
                                @"Error", 
                                 MessageBoxButtons.OK, 
                                 MessageBoxIcon.Error);
                ds = null;
            }
            else
            {
                foreach (DataRow r in ds.Tables[0].Rows)
                    cmb.Items.Add(r["Name"].ToString());
                ds = null;

            }

        }

        #endregion

    }
}
