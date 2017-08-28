using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Timers;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using Ookii.Dialogs;

namespace FolderMove

{
    /// <summary>
    /// WinForm app to copy/move files, including large file set that can be network limiting, with a fire and forget feature that will only
    /// run during quiet periods.
    /// TODO: Add in user folder move (moving from an old computer to new). Implement a progress bar rather than a print out of file finished copy. Possible
    /// find a better display of current files and size that is not so slow.
    /// </summary>
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

            var progress = new Progress<int>(percent =>
            {
                ///label6.Text = percent + "%";
                ///label6.Refresh();
            });

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
                    await RunCopyorMoveAfterSeven(_pts.Token, _cts.Token, progress);
                    timer2.Interval = (int)MilliSecondsToNextPauseTime(PauseTime);
                    timer2.Start();
                }
                
            }

            ///This is only indication of a move. Everything else is a copy, except for if you do checkbox 1, and then the check is in method
            if (!checkBox1.Checked && (checkBox2.Checked))
            {
                listBox1.Items.Add("**********File Move has Started!*****");
                listBox1.Items.Add("This will delete the source path. If you did not intend that please hit Stop Copy");
                PrepareControlForStart();
                RunCopyorMove(progress);
            }
            /// If no option selected it runs now, and does a copy
            if (!checkBox1.Checked && (!checkBox2.Checked))
            {
                listBox1.Items.Add("**********File Copy has Started!*****");
                PrepareControlForStart();
                RunCopyorMove(progress);

            }
        }
        /// <summary>
        /// This will tick the timer and cause the pause to be pressed, starting the copy/move method
        /// It will then start timer 2.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer1_Tick(object sender, EventArgs e)
        {
            timer1.Stop();

            PauseBtn_Click();

            var progress = new Progress<int>(percent =>
            {
                ///label6.Text = percent + "%";
                ///label6.Refresh();
            });

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            ///I am not worried about this not being awaited, because I do actually want the timer2 to start before the execution of the move/copy finishes
            RunCopyorMoveAfterSeven(_pts.Token, _cts.Token, progress);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            timer2.Interval = (int)MilliSecondsToNextPauseTime(PauseTime);
            timer2.Start();
        }
        /// <summary>
        /// When timer2 ticks, it will cancel the operation. This has to happen because the CopytoAsync for whatever reason
        /// was not getting feed any response to the token being envoked, so I just have it cancelled instead.
        /// This then starts the timer loop off again. This timer loop will run contiously until the app closes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        /// <summary>
        /// This Async pause does work really well, just have to have methods that will actually take it as token, like cancel token.
        /// But even without that it does allow you to do your code a little bit cleaner, atlthough from the looks of things
        /// I am not too worried about how clean my code looks...
        /// </summary>
        public void PauseBtn_Click()
        {
            if (_pts != null)   
            _pts.IsPaused = !_pts.IsPaused;

        }
        /// <summary>
        /// This is the base copy/move function that I call when timing is not important. For me this would only run
        /// on our main network, and not at any facility site, but it would be really great for things like large
        /// path restores which will take a long time, and can function in a fire and forget kind of way.
        /// In all honestly the need for something like this is minuscule, since robocopy exists and it is much easier to implement
        /// But this would allow for easier use for end users, and in the end I wanted my app to be able to do both;
        /// Be a timer move/copy and a standard move/copy, so we can call a single app up to do multiple things.
        /// </summary>
        public async void RunCopyorMove(IProgress<int> progress)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            string StartDirectory = @SrcPath.Text;
            string EndDirectory = @DestPath.Text;
            ///Checkbox2 indicates a move, rather than a copy.
            if (checkBox2.Checked)
            {
                try
                {
                    ///This warning is nice to have so the user does not accidentally do a move and not realize until it is too late.
                    ///This could possibly be a message-box if you are really concered.
                    listBox1.Items.Add("This will delete the source path. If you did not intend that please hit Stop Copy");
                    var moveTask = Task.Run(async() =>
                    {
                        ///Get the total number of files, and their size. Do note this will take a really long time.
                        ///Which is why I left it off the After6 run, because those are usually very large moves.
                        ///If you are talking 100s of gigs or even coming close to TB, it would eclipse the timers
                        ///trying to get the info.
                        long fCount = Directory.GetFiles(StartDirectory, "*", SearchOption.AllDirectories).Length;
                        var files = Directory.EnumerateFiles(StartDirectory, "*", SearchOption.AllDirectories);
                        long sum = (from file in files let fileInfo = new FileInfo(file) select fileInfo.Length).Sum();
                        this.Invoke((MethodInvoker)delegate
                        {
                            label5.Text = "Total files to copy " + fCount;
                        });
                        this.Invoke((MethodInvoker)delegate
                        {
                            label4.Text = "Total size to copy " + (sum / 1024f) / 1024f + " MB";
                        });
                        ///Note, this only works for sub directories, and will not copy over root contents, the next foreach loop takes care of that.
                        ///This is allow wrapped in the same task, so it will do the sub first, then the root, and run it both on the same thread.
                        foreach (string dirPath in Directory.GetDirectories(StartDirectory, "*", SearchOption.AllDirectories))
                        {
                            ///I initially did this with a DirectoryInfo and FileInfo, inplace of the style I did now. But this had a weird outcome
                            ///Where a subdir folder would be created, but the files would be copied to root. Doing it this way works
                            Directory.CreateDirectory(dirPath.Replace(StartDirectory, EndDirectory));

                            foreach (string filename in Directory.EnumerateFiles(dirPath))
                            {
                                using (FileStream SourceStream = File.Open(filename, FileMode.Open))
                                {
                                    using (FileStream DestinationStream = File.Create(filename.Replace(@SrcPath.Text, @DestPath.Text)))
                                    {
                                        
                                        ///To be able to have the cancel token work, I had to put in a buffer size.
                                        ///Since I didn't see a point in lowering or increasing the buffer size (increasing *might* be better since
                                        ///we are doing large moves, and it would process files quicker, but at the cost of CPU)
                                        ///I just kept the default value.
                                        await SourceStream.CopyToAsync(DestinationStream, 81920, token);
                                        
                                        ///No matter if I put the source or the destination, it would not display the name of the file being moved, but instead
                                        ///The file that just finished. So I had to put this "Finished Moving"
                                        ///TODO: Possibly put in a progress percentage, hence the progress<T> in button press
                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            listBox1.TopIndex = listBox1.Items.Count - 1;
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
                                        listBox1.TopIndex = listBox1.Items.Count - 1;
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
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                    PrepareControlsForCancel();
                }
                catch (OperationCanceledException)
                {
                    listBox1.Items.Add("Cancelled.");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                    PrepareControlsForCancel();
                }
            }
            if (!checkBox2.Checked)
            {
                try
                {
                    var t = Task.Run(async() =>
                    {

                        long fCount = Directory.GetFiles(StartDirectory, "*", SearchOption.AllDirectories).Length;
                        var files = Directory.EnumerateFiles(StartDirectory, "*", SearchOption.AllDirectories);
                        long sum = (from file in files let fileInfo = new FileInfo(file) select fileInfo.Length).Sum();
                        this.Invoke((MethodInvoker)delegate
                        {
                            label5.Text = "Total files to copy " + fCount;
                        });
                        this.Invoke((MethodInvoker)delegate
                        {
                            label4.Text = "Total size to copy " + (sum/1024f)/1024f + " MB";
                        });

                        DirectoryInfo source = new DirectoryInfo(StartDirectory);
                        DirectoryInfo destination = new DirectoryInfo(EndDirectory);

                        


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
                                            listBox1.TopIndex = listBox1.Items.Count - 1;
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
                                        listBox1.TopIndex = listBox1.Items.Count - 1;
                                        listBox1.Items.Add("Finished Copying  " + SourceStream.Name);
                                    });
                                    token.ThrowIfCancellationRequested();

                                }
                            }
                        }
                    });
                    await t;
                    listBox1.Items.Add("**********File Copy has completed!*****");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                    PrepareControlsForCancel();
                       
                }
                catch (OperationCanceledException)
                {
                    listBox1.Items.Add("Cancelled.");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                    PrepareControlsForCancel();
                }
            }
        }
        public async Task RunCopyorMoveAfterSeven(PauseToken pausetoken, CancellationToken cancelToken, IProgress<int> progress)
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
                                            listBox1.TopIndex = listBox1.Items.Count - 1;
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
                                        listBox1.TopIndex = listBox1.Items.Count - 1;
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
                    listBox1.TopIndex = listBox1.Items.Count - 1;
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
                                            listBox1.TopIndex = listBox1.Items.Count - 1;
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
                                        listBox1.TopIndex = listBox1.Items.Count - 1;
                                        listBox1.Items.Add("Finished Copying  " + SourceStream.Name);
                                    });
                                    cancelToken.ThrowIfCancellationRequested();
                                }
                            }
                        }
                    });
                    await moveTask;
                    listBox1.Items.Add("**********File Move has Completed!*****");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                    PrepareControlsForCancel();
                }
                    
                catch (OperationCanceledException)
                {
                    listBox1.Items.Add("Cancelled.");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                    PrepareControlsForCancel();
                }

            }
        }

        private void SrcButton_Click(object sender, EventArgs e)
        {
            using (Ookii.Dialogs.VistaFolderBrowserDialog browserDialog = new Ookii.Dialogs.VistaFolderBrowserDialog())
            {
                if (browserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SrcPath.Text = browserDialog.SelectedPath;
                }
            }
                
        }
        private void button2_Click(object sender, EventArgs e)
        {
            using (Ookii.Dialogs.VistaFolderBrowserDialog browserDialog = new Ookii.Dialogs.VistaFolderBrowserDialog())
            {
                if (browserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    DestPath.Text = browserDialog.SelectedPath;
                }
            }
        }
    }
}   
      
