using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Timers;
using System.ComponentModel;


namespace FolderMove

{
    public partial class FolderMoveWindow : Form
    {
        
        /// <summary>
        /// This tells when to run and when to pause. It runs at 6pm, pauses at 7am
        /// </summary>
        private TimeSpan RunTime = new TimeSpan(18, 0, 0);
        private TimeSpan PauseTime = new TimeSpan(7, 0, 0);
        /// <summary>
        /// Initializing and calling global vars.
        /// I also set the progress bar to not enabled so that if you hit exit it will not recongize the progess bar as enabled
        /// So it will work without any fuss.
        /// </summary>
        public FolderMoveWindow()
        {
            InitializeComponent();
            string SourcePath = @SrcPath.Text;
            string DestinationPath = @DestPath.Text;
        }

        public CancellationTokenSource _cts = null;
 
        public PauseTokenSource _pts = null;

        private static object locker = new object();

        /// <summary>
        /// Getting the time in milliseconds to
        /// Create a timespan.
        /// </summary>
        /// <param name="targetTime"></param>
        /// <returns></returns>
        private double MilliSecondsToNextTargetTime(TimeSpan targetTime)
        {
            DateTime dt = DateTime.Today.Add(targetTime);
            if (DateTime.Now > dt)
            {
                dt = dt.AddSeconds(45);
            }
            return dt.Subtract(DateTime.Now).TotalMilliseconds;
        }

        /// <summary>
        /// This is the timespan millisecond creation for pausing at 7am
        /// </summary>
        /// <param name="pauseTime"></param>
        /// <returns></returns>
        private double MilliSecondsToNextPauseTime(TimeSpan pauseTime)
        {
            DateTime dt = DateTime.Today.Add(pauseTime);
            if (DateTime.Now > dt)
            {
                dt = dt.AddSeconds(15);
            }
            return (dt).Subtract(DateTime.Now).TotalMilliseconds;
        }
        /// <summary>
        /// Check box 1 tells the program to run the program after 6pm and to pause at 7am
        /// Check box 2 tells the program to do a move. 
        /// There is only one check for check box 2 here, because I then do a check inside
        /// The method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal async void startBtn_Click(object sender, EventArgs e)
        {
            
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _pts = new PauseTokenSource();
            var pausetoken = _pts.Token;

            timer1.Tick += new EventHandler(Timer1_Tick);
            timer2.Tick += new EventHandler(Timer2_Tick);


            ///This will check the time. If after 6pm it will start now
            ///If before 6 it starts the timer loop (where it starts timer 2
            ///and then waits for it hit 7am, before pausing, going to timer 1 to 6).
            if (checkBox1.Checked)
            {
                /// Because of how I am doing the timings, I want a pause function to be set to paused whenever the After 6
                /// option is selected. This will prevent it from running until timer1 ends.
                _pts.IsPaused =!_pts.IsPaused;
                DateTime sixAfter = DateTime.Today.Add(RunTime);
                if (DateTime.Now < sixAfter)
                {
                    listBox1.Items.Add("This will start after 6pm, and then stop at 7am. It will loop until complete");
                    PrepareControlForStart();
                    timer1.Interval = (int)MilliSecondsToNextTargetTime(RunTime);
                    timer1.Start();
                }
                /// If the user ends up starting after 6, this will just start the job right away and then start
                /// timer2. Since timer 2 is 7am, unless this starts past midnight, it won't throw any errors.
                /// TODO: Add code so that after midnight there is no errors. For now there is a very tiny chance this will happen.
                if (DateTime.Now > sixAfter)
                {
                    PauseBtn_Click();
                    PrepareControlForStart();
                    listBox1.Items.Add("**********File Copy has Started!*****");
                    await RunCopyorMoveAfterSeven(_pts.Token, _cts.Token);
                    timer2.Interval = (int)MilliSecondsToNextPauseTime(PauseTime);
                    timer2.Start();
                }
                
            }

            ///This is only indication of a move. Everything else is a copy
            if (!checkBox1.Checked && (checkBox2.Checked))
            {
                listBox1.Items.Add("**********File Move has Started!*****");
                listBox1.Items.Add("This will delete the source path. If you did not intend that please hit Stop Copy");
                PrepareControlForStart();
                RunCopyorMove();
            }
            /// If no option selected it runs now, and does a copy
            if (!checkBox1.Checked && (!checkBox2.Checked))
            {
                listBox1.Items.Add("**********File Copy has Started!*****");
                PrepareControlForStart();
                RunCopyorMove();

            }
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            timer1.Stop();

            PauseBtn_Click();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            RunCopyorMoveAfterSeven(_pts.Token, _cts.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            timer2.Interval = (int)MilliSecondsToNextPauseTime(PauseTime);
            timer2.Start();
        }

        private void Timer2_Tick(object sender, EventArgs e)
        {
            timer2.Stop();

            StopBtn_Click(sender, e);

            timer1.Interval = (int)MilliSecondsToNextTargetTime(RunTime);
            timer1.Start();
        }

        internal void PrepareControlForStart()
        {

            this.Invoke((MethodInvoker)delegate
            {
                StartBtn.Enabled = false;
                StopBtn.Enabled = true;
            });


        }
        internal void PrepareControlsForCancel()
        {
            if (_cts != null)
                _cts.Cancel();
            this.Invoke((MethodInvoker)delegate
            {
                StartBtn.Enabled = true;
                StopBtn.Enabled = false;
            });
        }


        private void StopBtn_Click(object sender, EventArgs e)
        {
            PrepareControlsForCancel();

            _cts.Cancel();

            listBox1.Items.Add("**********File Copy has Stopped!*****");

        }



        private void ExitBtn_Click(object sender, EventArgs e)
        {
            if (_cts != null)
                MessageBox.Show("File Copy is running is running. Cancel the copy first then close the application!");
            else
                this.Close();
        }

        public void PauseBtn_Click()
        {
            if (_pts != null)   
            _pts.IsPaused = !_pts.IsPaused;

        }
        public async void RunCopyorMove()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            string StartDirectory = @SrcPath.Text;
            string EndDirectory = @DestPath.Text;
            if (checkBox2.Checked)
            {
                try
                {
                    listBox1.Items.Add("This will delete the source path. If you did not intend that please hit Stop Copy");
                    var moveTask = Task.Run(async() =>
                    {
                        foreach (string dirPath in Directory.GetDirectories(StartDirectory, "*", SearchOption.AllDirectories))
                        {
                            Directory.CreateDirectory(dirPath.Replace(StartDirectory, EndDirectory));

                            foreach (string filename in Directory.EnumerateFiles(dirPath))
                            {
                                using (FileStream SourceStream = File.Open(filename, FileMode.Open))
                                {
                                    using (FileStream DestinationStream = File.Create(filename.Replace(@SrcPath.Text, @DestPath.Text)))
                                    {

                                        await SourceStream.CopyToAsync(DestinationStream, 81920, token);
                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            listBox1.Items.Add("Finished Moving  " + SourceStream.Name);
                                        });
                                        token.ThrowIfCancellationRequested();

                                    }

                                }
                            }
                        }
                        foreach (string filename in Directory.EnumerateFiles(@SrcPath.Text))
                        {
                            using (FileStream SourceStream = File.Open(filename, FileMode.Open))
                            {
                                using (FileStream DestinationStream = File.Create(EndDirectory + filename.Substring(filename.LastIndexOf('\\'))))
                                {

                                    await SourceStream.CopyToAsync(DestinationStream, 81920, token);
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        listBox1.Items.Add("Finsihed Moving  " + SourceStream.Name);
                                    });
                                    token.ThrowIfCancellationRequested();

                                }
                            }
                        }
                    });
                    await moveTask;
                    if (System.IO.Directory.Exists(SrcPath.Text))
                    {
                        try
                        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            Task.Factory.StartNew(() => System.IO.Directory.Delete(SrcPath.Text, true), token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        }

                        catch (System.IO.IOException e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    listBox1.Items.Add("**********File Move has Completed!*****");
                    PrepareControlsForCancel();
                }
                catch (OperationCanceledException)
                {
                    listBox1.Items.Add("Cancelled.");
                    PrepareControlsForCancel();
                }
            }
            if (!checkBox2.Checked)
            {
                try
                {
                    var t = Task.Run(async() =>
                    {
                        foreach (string dirPath in Directory.GetDirectories(StartDirectory, "*", SearchOption.AllDirectories))
                        {
                            Directory.CreateDirectory(dirPath.Replace(StartDirectory, EndDirectory));

                            foreach (string filename in Directory.EnumerateFiles(dirPath))
                            {
                                using (FileStream SourceStream = File.Open(filename, FileMode.Open))
                                {
                                    using (FileStream DestinationStream = File.Create(filename.Replace(@SrcPath.Text, @DestPath.Text)))
                                    {

                                        await SourceStream.CopyToAsync(DestinationStream, 81920, token);
                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            listBox1.Items.Add("Finished Copying  " + DestinationStream.Name);
                                        });
                                        token.ThrowIfCancellationRequested();

                                    }

                                }
                            }
                        }
                        foreach (string filename in Directory.EnumerateFiles(@SrcPath.Text))
                        {
                            using (FileStream SourceStream = File.Open(filename, FileMode.Open))
                            {
                                using (FileStream DestinationStream = File.Create(EndDirectory + filename.Substring(filename.LastIndexOf('\\'))))
                                {

                                    await SourceStream.CopyToAsync(DestinationStream, 81920, token);
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        listBox1.Items.Add("Finished Copying  " + SourceStream.Name);
                                    });
                                    token.ThrowIfCancellationRequested();

                                }
                            }
                        }
                    });
                    await t;
                    listBox1.Items.Add("**********File Copy has completed!*****");
                    PrepareControlsForCancel();
                       
                }
                catch (OperationCanceledException)
                {
                    listBox1.Items.Add("Cancelled.");
                    PrepareControlsForCancel();
                }
            }
        }
        public async Task RunCopyorMoveAfterSeven(PauseToken pausetoken, CancellationToken cancelToken)
        {

            string StartDirectory = @SrcPath.Text;
            string EndDirectory = @DestPath.Text;

            if (checkBox2.Checked)
            {
                try
                {
                    listBox1.Items.Add("This will delete the source path. If you did not intend that please hit Stop Copy");
                    var moveTask = Task.Run(async () =>
                    {
                        foreach (string dirPath in Directory.GetDirectories(StartDirectory, "*", SearchOption.AllDirectories))
                        {
                            Directory.CreateDirectory(dirPath.Replace(StartDirectory, EndDirectory));

                            foreach (string filename in Directory.EnumerateFiles(dirPath))
                            {
                                using (FileStream SourceStream = File.Open(filename, FileMode.Open))
                                {
                                    using (FileStream DestinationStream = File.Create(filename.Replace(@SrcPath.Text, @DestPath.Text)))
                                    {

                                        await pausetoken.WaitWhilePausedAsync();
                                        await SourceStream.CopyToAsync(DestinationStream, 81920, cancelToken);
                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            listBox1.Items.Add("Finished Moving  " + SourceStream.Name);
                                        });
                                        cancelToken.ThrowIfCancellationRequested();

                                    }

                                }
                            }
                        }
                        foreach (string filename in Directory.EnumerateFiles(@SrcPath.Text))
                        {
                            using (FileStream SourceStream = File.Open(filename, FileMode.Open))
                            {
                                using (FileStream DestinationStream = File.Create(EndDirectory + filename.Substring(filename.LastIndexOf('\\'))))
                                {

                                    await pausetoken.WaitWhilePausedAsync();
                                    await SourceStream.CopyToAsync(DestinationStream, 81920, cancelToken);
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        listBox1.Items.Add("Finished Moving  " + SourceStream.Name);
                                    });
                                    cancelToken.ThrowIfCancellationRequested();

                                }
                            }
                        }
                    });
                    await moveTask;

                    if (System.IO.Directory.Exists(SrcPath.Text))
                    {
                        try
                        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            Task.Factory.StartNew(() => System.IO.Directory.Delete(SrcPath.Text, true), cancelToken);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        }

                        catch (System.IO.IOException e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    listBox1.Items.Add("**********File Move has Completed!*****");
                    PrepareControlsForCancel();
                       

                    }
                    catch (OperationCanceledException)
                    {
                        listBox1.Items.Add("Cancelled.");
                        PrepareControlsForCancel();
                    }
            }
            if (!checkBox2.Checked)
            {
                try
                {
                    
                    var moveTask = Task.Run(async () =>
                    {
                        foreach (string dirPath in Directory.GetDirectories(StartDirectory, "*", SearchOption.AllDirectories))
                        {
                            Directory.CreateDirectory(dirPath.Replace(StartDirectory, EndDirectory));

                            foreach (string filename in Directory.EnumerateFiles(dirPath))
                            {
                                using (FileStream SourceStream = File.Open(filename, FileMode.Open))
                                {
                                    using (FileStream DestinationStream = File.Create(filename.Replace(@SrcPath.Text, @DestPath.Text)))
                                    {

                                        await pausetoken.WaitWhilePausedAsync();
                                        await SourceStream.CopyToAsync(DestinationStream, 81920, cancelToken);
                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            listBox1.Items.Add("Finished Copying  " + SourceStream.Name);
                                        });
                                        cancelToken.ThrowIfCancellationRequested();
                                    }

                                }
                            }
                        }
                        foreach (string filename in Directory.EnumerateFiles(@SrcPath.Text))
                        {
                            using (FileStream SourceStream = File.Open(filename, FileMode.Open))
                            {
                                using (FileStream DestinationStream = File.Create(EndDirectory + filename.Substring(filename.LastIndexOf('\\'))))
                                {

                                    await pausetoken.WaitWhilePausedAsync();
                                    await SourceStream.CopyToAsync(DestinationStream, 81920, cancelToken);
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        listBox1.Items.Add("Finished Copying  " + SourceStream.Name);
                                    });
                                    cancelToken.ThrowIfCancellationRequested();
                                }
                            }
                        }
                    });
                    await moveTask;
                    listBox1.Items.Add("**********File Move has Completed!*****");
                    PrepareControlsForCancel();
                }
                    
                catch (OperationCanceledException)
                {
                    listBox1.Items.Add("Cancelled.");
                    PrepareControlsForCancel();
                }

            }
        }
    }
}   
      
