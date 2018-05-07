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
            cnf = new Converter.Configuration.Configuration();
            cnf.LoadConfig();
            txtServer.Focus();
            isLoaded = true;
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (txtServer.Text.Equals(string.Empty))
            {
                txtServer.Focus();
                MessageBox.Show("Please enter the valid server name",
                                 "Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }
            if (cmbAuth.SelectedIndex == -1)
            {
                cmbAuth.Focus();
                MessageBox.Show("Please choose authentication type!",
                                "Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }
            if (cmbDatabase.SelectedIndex == -1)
            {
                cmbDatabase.Focus();
                MessageBox.Show("Please choose database",
                                "Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }
            if (chkNewDatabase.IsChecked == false && cmbDatabase.Text.Equals(cmbDestination.Text))
            {
                MessageBox.Show("Source and destination are the same. Not allowed!",
                                "Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;

            }

            //Create inputs
            i = new Inputs();
            i.serverName = txtServer.Text;
            i.databaseName = cmbDatabase.Text;
            i.inMemoryDataBaseName = cmbDestination.Text;
            i.userName = txtUserName.Text;
            i.password = txtPassword.Text;
            i.isWindows = cmbAuth.SelectedIndex == 0 ? true : false;
            i.createNew = chkNewDatabase.IsChecked == true;
            if (i.createNew)
                i.inMemoryDataBaseName = i.databaseName + "_InMem";
            //create options
            o = new Options();
            o.CopyData = chkCopyData.IsChecked == true;


            if (cmbIndexOptions.SelectedIndex == 0)
                o.UseHashIndexes = Options.IndexDecision.Hash;
            else if (cmbIndexOptions.SelectedIndex == 1)
                o.UseHashIndexes = Options.IndexDecision.Range;
            else
                o.UseHashIndexes = Options.IndexDecision.ExtendedPropery;

            //o.DropOnDestination = chkDropOnDestination.Checked;
            o.SchemaContains = txtSchema.Text.Trim();
            o.TableContains = txtTable.Text.Trim();

            Server server = null;
            try
            {
                ServerConnection cnn = new ServerConnection(i.serverName);
                cnn.Connect();
                server = new Server(cnn);

            }
            catch (Exception ex)
            {
                MessageBox.Show("I'm unable to connect to the server " + "" + i.serverName + "" + "\r\n" + ex.Message,
                                "Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }
            bool isSysAdmin = ((int)DataAccess.ExecuteScalar(DataAccess.GetConnectionString(
                    txtServer.Text,
                    "master",
                    cmbAuth.SelectedIndex == 0 ? true : false,
                    txtUserName.Text, txtPassword.Text), " SELECT IS_SRVROLEMEMBER ('sysadmin') ") == 1) ? true : false;

            if (isSysAdmin == false)
            {
                MessageBox.Show("You should connect as a member of sysadmin fixed server role",
                                "Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }



            if (new Version(server.VersionString) < new Version(C_SERVER_VERSION))
            {
                MessageBox.Show("The server has to be SQL2016 SP2 or higher",
                                "Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }

            if (server.Databases[i.databaseName].HasMemoryOptimizedObjects)
            {
                MessageBox.Show("The source database contains Memory Optimized FileGroup. It is not allowed!",
                                "Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;
            }
            string error = "";


            if (i.createNew)
            {
                if (MessageBox.Show("You choose to create a new database \"" + i.databaseName + "_InMem." + "\"\r\n" + " Are you sure?",
                                    "Question",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }
                if (Converter.Utility.CreateDatabase.Create(server, i.databaseName + "_InMem", ref error, cnf.FileGroupName, cnf.FileName, cnf.MoPath) == false)
                {
                    MessageBox.Show("An error occurs while creating the database!" + Environment.NewLine + error,
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                if (MessageBox.Show("You choose to convert the database \"" + i.databaseName.ToUpper() + "\"" + " to In-Mem \"" + i.inMemoryDataBaseName.ToUpper() + "\"\r\n" + "Are you sure?",
                                    "Question",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }
                if (Converter.Utility.CreateDatabase.Create(server, i.inMemoryDataBaseName, ref error, cnf.FileGroupName, cnf.FileName, cnf.MoPath) == false)
                {
                    MessageBox.Show("An error occurs while creating the database!",
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }


            }


            ProgressBar1.Visibility = Visibility.Visible;
            ProgressBar1.Minimum = 1;
            ProgressBar1.Maximum = server.Databases[i.databaseName].Tables.Count;

            SetupRows(false);
            btnConvertToMO.IsEnabled = false;
            btnCancel.IsEnabled = true;



            t1 = DateTime.Now;

            mainObr = new Thread(StartConversion);
            mainObr.Start();


            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();

            

        }



        #region " Fields & constants "
        private bool isError = false;
        //private Task t = null;
        //private CancellationTokenSource ts = null;


        private bool isLoaded = false;
        private Inputs i = null;
        private Options o = null;
        DispatcherTimer dispatcherTimer = null;
        private Thread mainObr = null;

        //
        // https://support.microsoft.com/en-us/help/3177312/sql-server-2016-build-versions
        // SQL Server 2016 SP2 
        private const string C_SERVER_VERSION = "13.0.5026.0";


        //
        // New features available with SQL Server 2017
        // 
        private const string C_NEW_FEATURES_VERSION = "14.0.1000.169";


        private Converter.Configuration.Configuration cnf = null;
        private DateTime t1;
        private DateTime t2;
        private bool success = false;
        #endregion

        #region " Start the conversion "

        /// <summary>
        /// The conversion process starts here
        /// </summary>
        private void StartConversion()
        {

            ServerConnection cnn = new ServerConnection(i.serverName);
            cnn.Connect();
            Server server = new Server(cnn);

            Database db = server.Databases[i.databaseName];
            // Connect to the In-Memory Database
            ServerConnection cnnInMem = new ServerConnection(i.serverName);
            cnnInMem.Connect();
            Server serverInMem = new Server(cnnInMem);
            Database dbInMemory = serverInMem.Databases[i.inMemoryDataBaseName];

            // new features available starting with SQL Server 2017
            SQLServerMoFeatures enumFeatures = SQLServerMoFeatures.SQLServer2016;
            if (new Version(server.VersionString) >= new Version(C_NEW_FEATURES_VERSION))
            {
                enumFeatures = SQLServerMoFeatures.SQLServer2017;
            }
            success = db.SwichToMo(
                                    dbInMemory,
                                    (ILog)this,
                                    cnf,
                                    o,
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


        public void SetTextCode(string text)
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
        public void SetTextDescription(string text)
        {
            if (Dispatcher.CheckAccess())
            {
                txtDescription.Text = text;
            }
            else
            {
                this.Dispatcher.Invoke(() =>
                {
                    txtDescription.Text = text;
                });

            }
        }


        public void SetLabelText(string text)
        {
            if (Dispatcher.CheckAccess())
            {
                lblOveral.Text = text;

            }
            else
            {
                this.Dispatcher.Invoke(() =>
                {
                    lblOveral.Text = text;
                });

            }
        }

        void SetProgresBarValue(int text)
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

        void SetProgressBarMaxValue(int text)
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




        void ILog.Log(string text, string txtDescription)
        {

            SetTextCode(text);
            SetTextDescription(txtDescription);

        }
        private StringBuilder sb = null;
        void ILog.LogWarErr(string text, string txtDescription)
        {
            if (sb == null)
            {
                sb = new StringBuilder();
                sb.Append("****Summary report - converting  " + i.databaseName + " to IN-MEM OLTP " + i.inMemoryDataBaseName + " on server " + i.serverName + "\r\n");
                sb.Append("\r\n");
                sb.Append("****List of warnings and errors");
                sb.Append("\r\n");
                sb.Append("\r\n");
            }


            sb.Append(text + " " + txtDescription + "\r\n");
            sb.Append(Environment.NewLine);
        }

        int mCurrentItem;
        int ILog.CurrentItem
        {
            get
            {
                return mCurrentItem;
            }
            set
            {
                mCurrentItem = value;
            }
        }
        int mCounter;
        int ILog.Counter
        {
            get
            {
                return mCounter;
            }
            set
            {
                mCounter = value;
            }
        }

        #endregion

        #region " Cancel the conversation process "
        private bool isAborted = false;
        private void btnCancel_Click_1(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("You choose to stop conversation process.\r\n Are you sure?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            if (mainObr != null)
            {
                try
                {
                    mainObr.Abort();
                    while (mainObr.ThreadState != System.Threading.ThreadState.Aborted)
                    {
                        mainObr.Abort();
                    }

                }
                catch (Exception ex)
                {

                }
            }
            isAborted = true;
            SetupRows(true);
            btnCancel.IsEnabled = false;
            btnConvertToMO.IsEnabled = true;
            lblOveral.Text = string.Empty;
            txtCode.Text = string.Empty;
            txtDescription.Text = string.Empty;
            sb = null;
            

        }




        #endregion

        #region " The timer controls the working thread "
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (mainObr == null || mainObr.IsAlive)
            {
                return;
            }
            try
            {
                dispatcherTimer.IsEnabled = false;
                mainObr.Join();
                mainObr = null;

            }
            catch (Exception ex)
            {
            }
            t2 = DateTime.Now;

            ProgressBar1.Visibility = Visibility.Hidden;
            if (success)
            {
                TimeSpan ts = t2 - t1;
                MessageBox.Show("Switching to in-memory OLTP finished successfully. Elapsed time " + ts.ToString(@"dd\.hh\:mm\:ss"), "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            if (isAborted == false)
            {
                string fileName = i.databaseName + DateTime.Now.ToString("yyyy_mm_dd_HH_mm_ss") + ".txt";
                if (File.Exists(fileName))
                    File.Delete(fileName);
                File.WriteAllText(fileName, sb.ToString());
                // start notepad and disply the configuration
                Process.Start(fileName);
            }
            else
            {
                isAborted = false;
            }

            cnf.LoadConfig();
            SetupRows(true);
            btnCancel.IsEnabled = false;
            btnConvertToMO.IsEnabled = true;
            sb = null;

        }
        #endregion

        #region " Inputs manipulations "

        private void ComboBox_DropDownOpened(object sender, System.EventArgs e)
        {
            if (txtServer.Text.Trim().Equals(string.Empty))
            {
                txtServer.Focus();
                MessageBox.Show("Please enter the valid server name",
                                 "Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;

            }
            if (cmbAuth.SelectedIndex != 0 && (txtUserName.Text.Trim().Equals(string.Empty) || txtPassword.Text.Trim().Equals(string.Empty)))
            {

                MessageBox.Show("Please enter userName and password", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                if (isError)
                    isError = false;
                return;
            }
            if (isError == false)
            {
                BindDataBases(cmbDatabase);
            }
        }
        private void BindDataBases(ComboBox cmb)
        {
            string error = "";
            cmb.Items.Clear();
            DataSet ds = DataAccess.GetDataSet(
                DataAccess.GetConnectionString(
                    txtServer.Text,
                    "master",
                    cmbAuth.SelectedIndex == 0 ? true : false,
                    txtUserName.Text, txtPassword.Text), @"SELECT name 
                                                                FROM sys.databases
                                                                WHERE state = 0 
                                                                    AND is_read_only = 0 
                                                                ORDER BY name", null, out error);
            if (error.Equals(string.Empty) == false)
            {
                isError = true;
                MessageBox.Show("Error binding database information : " + error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (isLoaded == false)
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
            if (isLoaded == false)
                return;

            if (cmbDestination != null)
            {
                cmbDestination.IsEnabled = true;
            }


        }

        private void Destination_DropDownOpened(object sender, System.EventArgs e)
        {
            if (isLoaded == false)
                return;
            if (cmbDestination.IsEnabled == false)
                return;
            if (txtServer.Text.Trim().Equals(string.Empty))
            {
                txtServer.Focus();
                MessageBox.Show("Please enter the valid server name",
                                 "Error",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                return;

            }

            BindDataBases(cmbDestination);
        }


        private void cmbAuth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoaded == false)
                return;
            if (cmbAuth.SelectedItem != null)
            {
                if (isError)
                    isError = false;

                if (cmbAuth.SelectedIndex == 0)
                {
                    txtUserName.Text = "";
                    txtPassword.Text = "";
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
