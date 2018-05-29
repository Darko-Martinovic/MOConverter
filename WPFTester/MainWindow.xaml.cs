using System.Data;
using System.Threading;
using System.Windows;
using System;
using Converter.Options;
using System.Windows.Controls;
using Converter.Interface;
using System.Text;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;
using System.Windows.Threading;
using System.IO;
using System.Diagnostics;
using Converter.Extension;
using Converter.Inputs;
using Converter.DataAccess;
using Converter.Enums;
using Converter.Utility;

namespace WPFTester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window,ILog
    {
        public MainWindow()
        {
            InitializeComponent();
        }




        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            cmbAuth.SelectedIndex = 0;
            txtPassword.IsEnabled = false;
            txtUserName.IsEnabled = false;
            cmbDestination.IsEnabled = false;
            chkNewDatabase.IsChecked = true;
            //load the configuration
            _cnf = new Converter.Configuration.Configuration();
            _cnf.LoadConfig();
            txtServer.Focus();
            _isLoaded = true;
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (txtServer.Text.Equals(string.Empty))
            {
                txtServer.Focus();
                MessageBox.Show(@"Please enter the valid server name",
                                 @"Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }
            if (cmbAuth.SelectedIndex == -1)
            {
                cmbAuth.Focus();
                MessageBox.Show(@"Please choose authentication type!",
                                @"Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }
            if (cmbDatabase.SelectedIndex == -1)
            {
                cmbDatabase.Focus();
                MessageBox.Show(@"Please choose database",
                                @"Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }
            if (chkNewDatabase.IsChecked == false && cmbDatabase.Text.Equals(cmbDestination.Text))
            {
                MessageBox.Show(@"Source and destination are the same. Not allowed!",
                                @"Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;

            }

            //Create inputs
            _i = new Inputs
            {
                ServerName = txtServer.Text,
                DatabaseName = cmbDatabase.Text,
                InMemoryDataBaseName = cmbDestination.Text,
                UserName = txtUserName.Text,
                Password = txtPassword.Password,
                IsWindows = cmbAuth.SelectedIndex == 0,
                CreateNew = chkNewDatabase.IsChecked == true
            };
            if (_i.CreateNew)
                _i.InMemoryDataBaseName = $"{_i.DatabaseName}_InMem";
            //create options
            _o = new Options
            {
                CopyData = chkCopyData.IsChecked == true,
                TableContains = txtTable.Text.Trim(),
                SchemaContains = txtSchema.Text.Trim()
            };


            switch (cmbIndexOptions.SelectedIndex)
            {
                case 0:
                    _o.UseHashIndexes = Options.IndexDecision.Hash;
                    break;
                case 1:
                    _o.UseHashIndexes = Options.IndexDecision.Range;
                    break;
                default:
                    _o.UseHashIndexes = Options.IndexDecision.ExtendedPropery;
                    break;
            }

            //_o.DropOnDestination = chkDropOnDestination.Checked;

            Server server;
            try
            {
                var cnn = new ServerConnection(_i.ServerName);
                cnn.Connect();
                server = new Server(cnn);

            }
            catch (Exception ex)
            {
                MessageBox.Show($@"I'm unable to connect to the server {_i.ServerName}
                                {ex.Message}",
                                @"Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }

            if ((((int)DataAccess.ExecuteScalar(DataAccess.GetConnectionString(
                      txtServer.Text,
                      "master",
                      cmbAuth.SelectedIndex == 0,
                      txtUserName.Text, txtPassword.Password), " SELECT IS_SRVROLEMEMBER ('sysadmin') ") == 1)) == false)
            {
                MessageBox.Show(@"You should connect as a member of sysadmin fixed server role",
                                @"Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }



            if (new Version(server.VersionString) < new Version(CServerVersion))
            {
                MessageBox.Show(@"The server has to be SQL2016 SP2 or higher",
                                @"Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }
            if (server.Databases[_i.DatabaseName] == null)
            {
                MessageBox.Show(@"Choose the database!",
                                @"Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                cmbDatabase.SelectedItem = null;
                return;

            }

            if (server.Databases[_i.DatabaseName].HasMemoryOptimizedObjects)
            {
                MessageBox.Show(@"The source database contains Memory Optimized FileGroup. It is not allowed!",
                                @"Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }
            var error = "";


            if (_i.CreateNew)
            {
                if (MessageBox.Show($"You choose to create a new database {_i.DatabaseName}_InMem {Environment.NewLine} Are you sure?",
                                    @"Question",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }
                if (CreateDatabase.Create(server, _i.DatabaseName + "_InMem", ref error, _cnf.FileGroupName, _cnf.FileName, _cnf.MoPath) == false)
                {
                    MessageBox.Show($@"An error occurs while creating the database! {Environment.NewLine} {error}",
                                    @"Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                if (MessageBox.Show($@"You choose to convert the database {_i.DatabaseName.ToUpper()} to In-Mem {_i.InMemoryDataBaseName.ToUpper()} {Environment.NewLine} Are you sure?",
                                    @"Question",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }
                if (CreateDatabase.Create(server, _i.InMemoryDataBaseName, ref error, _cnf.FileGroupName, _cnf.FileName, _cnf.MoPath) == false)
                {
                    MessageBox.Show(@"An error occurs while creating the database!",
                                    @"Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }


            }


            ProgressBar1.Visibility = Visibility.Visible;
            ProgressBar1.Minimum = 1;
            ProgressBar1.Maximum = server.Databases[_i.DatabaseName].Tables.Count;

            SetupRows(false);
            btnConvertToMO.IsEnabled = false;
            btnCancel.IsEnabled = true;



            _t1 = DateTime.Now;

            _mainObr = new Thread(StartConversion);
            _mainObr.Start();


            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Tick += DispatcherTimer_Tick;
            _dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            _dispatcherTimer.Start();

            

        }



        #region " Fields & constants "
        private bool _isError;
        //private Task t = null;
        //private CancellationTokenSource ts = null;


        private bool _isLoaded;
        private Inputs _i ;
        private Options _o ;
        DispatcherTimer _dispatcherTimer;
        private Thread _mainObr;

        //
        // https://support.microsoft.com/en-us/help/3177312/sql-server-2016-build-versions
        // SQL Server 2016 SP2 
        private const string CServerVersion = "13.0.5026.0";


        //
        // New features available with SQL Server 2017
        // 
        private const string CNewFeaturesVersion = "14.0.1000.169";


        private Converter.Configuration.Configuration _cnf;
        private DateTime _t1;
        private DateTime _t2;
        private bool _success;
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
            



        }
        #endregion

        #region " Callback "

        private void SetTextCode(string text)
        {
            if (Dispatcher.CheckAccess())
            {
                txtCode.Text = text;
            }
            else
            {

                this.Dispatcher.Invoke(() =>
                {
                    txtCode.Text = text;
                });

            }
        }

        private void SetTextDescription(string text)
        {
            if (Dispatcher.CheckAccess())
            {
                txtDescription.Text = text;
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    txtDescription.Text = text;
                });

            }
        }


        private void SetLabelText(string text)
        {
            if (Dispatcher.CheckAccess())
            {
                lblOveral.Text = text;

            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    lblOveral.Text = text;
                });

            }
        }

        private void SetProgresBarValue(int text)
        {
            if (Dispatcher.CheckAccess())
            {
                if (text > ProgressBar1.Maximum)
                    text = (int)ProgressBar1.Maximum;
                ProgressBar1.Value = text;

            }
            else
            {

                this.Dispatcher.Invoke(() =>
                {
                    if (text > ProgressBar1.Maximum)
                        text = (int)ProgressBar1.Maximum;
                    ProgressBar1.Value = text;

                });

            }
        }

        private void SetProgressBarMaxValue(int text)
        {
            if (Dispatcher.CheckAccess())
            {
                this.ProgressBar1.Maximum = text;
            }
            else
            {

                this.Dispatcher.Invoke(() =>
                {
                    this.ProgressBar1.Maximum = text;
                });



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
                _sb.Append($"****Summary report - converting  {_i.DatabaseName}  to IN-MEM OLTP {_i.InMemoryDataBaseName}  on server {_i.ServerName} ");
                _sb.Append(Environment.NewLine);
                _sb.Append("****List of warnings and errors");
                _sb.Append(Environment.NewLine);
                _sb.Append(Environment.NewLine);
            }


            _sb.Append($"{text} {txt} {Environment.NewLine}");
            _sb.Append(Environment.NewLine);
        }

        int ILog.CurrentItem { get; set; }

        int ILog.Counter { get; set; }



        #endregion

        #region " Cancel the conversation process "

        private bool _isAborted;
        private void BtnCancel_Click_1(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show($@"You choose to stop conversation process.{Environment.NewLine}Are you sure?",
                                @"Question",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question) == MessageBoxResult.No)
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
                        _mainObr.Abort();
                    }

                }
                catch (Exception)
                {
                    if (Debugger.IsAttached)
                        Debugger.Break();
                }
            }
            _isAborted = true;
            SetupRows(true);
            btnCancel.IsEnabled = false;
            btnConvertToMO.IsEnabled = true;
            lblOveral.Text = string.Empty;
            txtCode.Text = string.Empty;
            txtDescription.Text = string.Empty;
            _sb = null;
            

        }




        #endregion

        #region " The timer controls the working thread "
        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (_mainObr == null || _mainObr.IsAlive)
            {
                return;
            }
            try
            {
                _dispatcherTimer.IsEnabled = false;
                _mainObr.Join();
                _mainObr = null;

            }
            catch (Exception)
            {
                if(Debugger.IsAttached)
                    Debugger.Break();
            }
            _t2 = DateTime.Now;

            ProgressBar1.Visibility = Visibility.Hidden;
            if (_success)
            {
                var ts = _t2 - _t1;
                MessageBox.Show($@"Switching to in-memory OLTP finished successfully. Elapsed time {ts:dd\.hh\:mm\:ss}",
                                @"Info", 
                                MessageBoxButton.OK, 
                               MessageBoxImage.Information);
            }

            if (_isAborted == false)
            {
                var fileName = _i.DatabaseName + DateTime.Now.ToString("yyyy_mm_dd_HH_mm_ss") + ".txt";
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

            _cnf.LoadConfig();
            SetupRows(true);
            btnCancel.IsEnabled = false;
            btnConvertToMO.IsEnabled = true;
            _sb = null;

        }
        #endregion

        #region " Inputs manipulations "

        private void ComboBox_DropDownOpened(object sender, System.EventArgs e)
        {
            if (txtServer.Text.Trim().Equals(string.Empty))
            {
                txtServer.Focus();
                MessageBox.Show(@"Please enter the valid server name",
                                @"Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;

            }
            var text = txtUserName.Text;
            if (cmbAuth.SelectedIndex != 0 && (text.Trim().Equals(string.Empty) || txtPassword.Password.Trim().Equals(string.Empty)))
            {

                MessageBox.Show(@"Please enter userName and password", 
                                @"Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
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
                    cmbAuth.SelectedIndex == 0,
                    userName: txtUserName.Text, password: txtPassword.Password), @"SELECT name 
                                                                FROM sys.databases
                                                                WHERE state = 0 
                                                                    AND is_read_only = 0 
                                                                ORDER BY name", null, out var error);
            if (string.Empty.Equals(error) == false)
            {
                _isError = true;
                MessageBox.Show($"Error binding database information : {error}", 
                                @"Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
                ds = null;
            }
            else
            {
                foreach (DataRow r in ds.Tables[0].Rows)
                    cmb.Items.Add(r["Name"].ToString());
                ds = null;

            }

        }


        private void chkNewDatabase_Checked(object sender, EventArgs e)
        {
            if (_isLoaded == false)
                return;
            if (chkNewDatabase.IsChecked == true)
            {
                if (cmbDestination != null)
                {
                    cmbDestination.IsEnabled = false;
                    cmbDestination.Text = string.Empty;
                }
            }
            else
            {
                cmbDestination.IsEnabled = true;
            }


        }

        private void chkNewDatabase_Unchecked_1(object sender, EventArgs e)
        {
            if (_isLoaded == false)
                return;

            if (cmbDestination != null)
            {
                cmbDestination.IsEnabled = true;
            }


        }

        private void Destination_DropDownOpened(object sender, System.EventArgs e)
        {
            if (_isLoaded == false)
                return;
            if (cmbDestination.IsEnabled == false)
                return;
            if (txtServer.Text.Trim().Equals(string.Empty))
            {
                txtServer.Focus();
                MessageBox.Show(@"Please enter the valid server name",
                                @"Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;

            }

            BindDataBases(cmbDestination);
        }


        private void cmbAuth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoaded == false)
                return;
            if (cmbAuth.SelectedItem == null) return;
            if (_isError)
                _isError = false;

            if (cmbAuth.SelectedIndex == 0)
            {
                txtUserName.Text = "";
                txtPassword.Password = "";
                txtUserName.IsEnabled = false;
                txtPassword.IsEnabled = false;
            }
            else if (cmbAuth.SelectedIndex == 1)
            {
                txtUserName.IsEnabled = true;
                txtPassword.IsEnabled = true;
                txtUserName.Focus();
            }
        }
        private void SetupRows(bool v)
        {
            txtServer.IsEnabled = v;
            txtUserName.IsEnabled = v;
            txtPassword.IsEnabled = v;
            cmbAuth.IsEnabled = v;
            cmbDatabase.IsEnabled = v;
            cmbDestination.IsEnabled = v;
            txtSchema.IsEnabled = v;
            txtTable.IsEnabled = v;
            cmbIndexOptions.IsEnabled = v;
            chkCopyData.IsEnabled = v;
            chkNewDatabase.IsEnabled = v;
        }

        #endregion


    }




}
