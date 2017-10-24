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
using System.Diagnostics;
using System.Runtime.InteropServices;
using Alphaleonis.Win32.Filesystem;
using ConsoleApp1;

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
        public int files = 0;
        public int extraFiles = 0;
        public int filesSkipped = 0;
        public int filesCopied = 0;
        public bool isRunning = true;
        //bool timerRunning = true;
        long sum = 0;
        long endsum;
        long progressvalue = 0;
        long progressum = 0;
        string uname = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split('\\').Last();
        public string StartDirectory;
        public string EndDirectory;
        public string StartDirEnd;
        public string logdir;
        //int folders = 0;
        /// <summary>
        /// This tells when to run and when to pause. It runs at 6pm, pauses at 7am
        /// </summary>
        private DateTime SixPM = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 18, 0, 0);
        private DateTime SevenAM = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 7, 0, 0);
        private DateTime nowHour = DateTime.Now;
        TimeSpan targetTime;
        Stopwatch sw = new Stopwatch();
        /// <summary>
        /// Initializing and calling global vars.
        /// I also set the progress bar to not enabled so that if you hit exit it will not recongize the progess bar as enabled
        /// So it will work without any fuss.
        /// </summary>
        public FolderMoveWindow()
        {
            InitializeComponent();
            StopBtn.Enabled = false;
            PauseBtn.Enabled = false;
        }


        public CancellationTokenSource _cts = null;

        public PauseTokenSource _pts = null;
        
        /// <summary>
        /// Getting the time in milliseconds to
        /// Create a timespan.
        /// </summary>
        /// <param name="targetTime"></param>
        /// <returns></returns>
        private double MilliSecondsToNextTargetTime()
        {
            targetTime = SixPM - nowHour;
            DateTime dt = DateTime.Now.Add(targetTime);
            if (DateTime.Now > dt)
            {
                dt = dt.AddDays(1);
            }
            return dt.Subtract(DateTime.Now).TotalMilliseconds;
        }

        /// <summary>
        /// This is the timespan millisecond creation for pausing at 7am
        /// </summary>
        /// <param name="pauseTime"></param>
        /// <returns></returns>
        private double MilliSecondsToNextPauseTime()
        {
            TimeSpan pauseTime = new TimeSpan();
            if (nowHour > SevenAM)
            {
                pauseTime = nowHour - SevenAM;
            }
            else if (nowHour < SevenAM)
            {
                pauseTime = SevenAM - nowHour;
            }
            DateTime dt = DateTime.Now.Add(pauseTime);
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
            StartDirectory = @SrcPath.Text;
            EndDirectory = @DestPath.Text;
            StartDirEnd = Alphaleonis.Win32.Filesystem.Path.GetFileName(StartDirectory.TrimEnd(Alphaleonis.Win32.Filesystem.Path.DirectorySeparatorChar));
            logdir = @"\\path\to\store\logs";
            logdir = Alphaleonis.Win32.Filesystem.Path.Combine(logdir, uname);
            string logname = String.Format("{0}__{1}", DateTime.Now.ToString("yyyyMMdd_hh.mm.ss"), StartDirEnd);
            string logfile = Alphaleonis.Win32.Filesystem.Path.Combine(logdir, logname);
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _pts = new PauseTokenSource();
            var pausetoken = _pts.Token;
            
            
            progressBar1.Value = 0;
            progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Marquee;

            timer1.Tick += new EventHandler(Timer1_Tick);
            timer2.Tick += new EventHandler(Timer2_Tick);
            timer3.Tick += new EventHandler(Timer3_Tick);

            ///This will check the time. If after 6pm it will start now
            ///If before 6 it starts the timer loop (where it starts timer 2
            ///and then waits for it hit 7am, before pausing, going to timer 1 to 6).
            if (checkBox1.Checked)
            {
                /// Because of how I am doing the timings, I want a pause function to be set to paused whenever the After 6
                /// option is selected. This will prevent it from running until timer1 ends.
                _pts.IsPaused = !_pts.IsPaused;
                if (DateTime.Now < SixPM)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        listBox1.Items.Add("This will start after 6pm, and then stop at 7am. It will loop until complete");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });
                    PrepareControlForStart();
                    timer1.Interval = (int)MilliSecondsToNextTargetTime();
                    timer1.Start();
                    timer3.Interval = 1000;
                    timer3.Start();
                }
                /// If the user ends up starting after 6, this will just start the job right away and then start
                /// timer2. Since timer 2 is 7am, unless this starts past midnight, it won't throw any errors.
                /// TODO: Add code so that after midnight there is no errors. For now there is a very tiny chance this will happen.
                if (DateTime.Now >= SixPM)
                {
                    PauseBtn_Click(sender, e);
                    PrepareControlForStart();
                    this.Invoke((MethodInvoker)delegate
                    {
                        listBox1.Items.Add("**********File Copy has Started!*****");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });
                    timer2.Interval = (int)MilliSecondsToNextPauseTime();
                    timer2.Start();
                    await RunCopyorMove(StartDirectory, EndDirectory, pausetoken, token, logfile, filesSkipped, filesCopied, isRunning, sum, endsum, progressvalue, progressum);

                }

            }

            ///This is only indication of a move. Everything else is a copy, except for if you do checkbox 1, and then the check is in method
            if (!checkBox1.Checked && (checkBox2.Checked))
            {
                this.Invoke((MethodInvoker)delegate
                {
                    listBox1.Items.Add("**********File Move has Started!*****");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                    listBox1.Items.Add("This will delete the source path. If you did not intend that please hit Stop Copy");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
                PrepareControlForStart();
                await RunCopyorMove(StartDirectory, EndDirectory, pausetoken, token, logfile, filesSkipped, filesCopied, isRunning, sum, endsum, progressvalue, progressum);
            }
            /// If no option selected it runs now, and does a copy
            if (!checkBox1.Checked && (!checkBox2.Checked))
            {
                this.Invoke((MethodInvoker)delegate
                {
                    listBox1.Items.Add("**********File Copy has Started!*****");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
                PrepareControlForStart();
                await RunCopyorMove(StartDirectory, EndDirectory, pausetoken, token, logfile, filesSkipped, filesCopied, isRunning, sum, endsum, progressvalue, progressum);


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
            timer3.Stop();
            //timerRunning = false;
            startBtn_Click(sender, e);
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

            _pts.IsPaused = !_pts.IsPaused;

            timer1.Interval = (int)MilliSecondsToNextTargetTime();
            timer1.Start();
        }
        private void Timer3_Tick(object sender, EventArgs e)
        {
            DateTime currentTime = DateTime.Now;
            string currentTimeHuman = String.Format("{0:T}", currentTime);
            this.Invoke((MethodInvoker)delegate
            {
                label9.Text = currentTimeHuman;
            });
        }
        internal void PrepareControlForStart()
        {

            this.Invoke((MethodInvoker)delegate
            {
                StartBtn.Enabled = false;
                StopBtn.Enabled = true;
                PauseBtn.Enabled = true;
                label4.Text = String.Empty;
                label7.Text = String.Empty;
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
                PauseBtn.Enabled = false;
                progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
                label9.Text = "Speed: ";
                label7.Text = "Percent done 100%";
            });
            
            files = 0;
        }


        private void StopBtn_Click(object sender, EventArgs e)
        {
            PrepareControlsForCancel();
            _cts.Cancel();
            this.Invoke((MethodInvoker)delegate
            {
                listBox1.Items.Add("**********File Copy has Stopped!*****");
                listBox1.TopIndex = listBox1.Items.Count - 1;
                progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            });

        }
        /// <summary>
        /// This Async pause does work really well, just have to have methods that will actually take it as token, like cancel token.
        /// But even without that it does allow you to do your code a little bit cleaner, atlthough from the looks of things
        /// I am not too worried about how clean my code looks...
        /// </summary>
        public void PauseBtn_Click(object sender, EventArgs e)
        {
            _pts.IsPaused = !_pts.IsPaused;
            if (_pts.IsPaused)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    listBox1.Items.Add("Paused");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }
            else
            {
                this.Invoke((MethodInvoker)delegate
                {
                    listBox1.Items.Add("Resumed");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
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
        private void DestButton_Click(object sender, EventArgs e)
        {
            using (Ookii.Dialogs.VistaFolderBrowserDialog browserDialog = new Ookii.Dialogs.VistaFolderBrowserDialog())
            {
                if (browserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    DestPath.Text = browserDialog.SelectedPath;
                }
            }
        }
        /// <summary>
        /// This is the base copy/move function that I call when timing is not important. For me this would only run
        /// on our main network, and not at any facility site, but it would be really great for things like large
        /// path restores which will take a long time, and can function in a fire and forget kind of way.
        /// In all honestly the need for something like this is minuscule, since robocopy exists and it is much easier to implement
        /// But this would allow for easier use for end users, and in the end I wanted my app to be able to do both;
        /// Be a timer move/copy and a standard move/copy, so we can call a single app up to do multiple things.
        /// </summary>
        public async Task RunCopyorMove(String StartDirectory, String EndDirectory, PauseToken _pts, CancellationToken token, string logfile, int filesSkipped, int filesCopied, bool isRunning, long sum, long endsum, long progressvalue, long progressum)
        {
            if (!Alphaleonis.Win32.Filesystem.Directory.Exists(EndDirectory))
            {
                Alphaleonis.Win32.Filesystem.Directory.CreateDirectory(EndDirectory);
            }
            if (!Alphaleonis.Win32.Filesystem.Directory.Exists(logfile))
            {
                Alphaleonis.Win32.Filesystem.Directory.CreateDirectory(logfile);
            }
            List<String> FileNames = new List<String>();
            List<String> SkippedFiles = new List<String>();
            List<String> ExtraFiles = new List<String>();
            Alphaleonis.Win32.Filesystem.DirectoryInfo SrcDirct = new Alphaleonis.Win32.Filesystem.DirectoryInfo(StartDirectory);
            Alphaleonis.Win32.Filesystem.DirectoryInfo DestDirct = new Alphaleonis.Win32.Filesystem.DirectoryInfo(EndDirectory);

            IEnumerable<Alphaleonis.Win32.Filesystem.FileInfo> srclist = SrcDirct.EnumerateFiles("*", SearchOption.AllDirectories);
            IEnumerable<Alphaleonis.Win32.Filesystem.FileInfo> destlist = DestDirct.EnumerateFiles("*", SearchOption.AllDirectories);

            FileCompare myCompare = new FileCompare();

            var destListOnly = (from file in destlist select file).Except(srclist, myCompare);
            var destListLen = destlist.Intersect(srclist, myCompare);
            var srcListOnly = (from file in srclist select file).Except(destlist, myCompare);

            try
            {
                ///This warning is nice to have so the user does not accidentally do a move and not realize until it is too late.
                ///This could possibly be a message-box if you are really concered.
                if (checkBox2.Checked)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        listBox1.Items.Add("This will delete the source path. If you did not intend that please hit Stop Copy");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });
                }

                var filecompare = Task.Run(async () =>
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        label7.Text = "Getting Files Please wait";
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });
                    if (_pts.IsPaused)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            listBox1.Items.Add("Paused");
                            listBox1.TopIndex = listBox1.Items.Count - 1;
                        });
                    }
                    else
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            listBox1.Items.Add("Not paused");
                            listBox1.TopIndex = listBox1.Items.Count - 1;
                        });
                    }

                    foreach (var v in destListOnly)
                    {
                        await _pts.WaitWhilePausedAsync();
                        extraFiles++;
                        this.Invoke((MethodInvoker)delegate
                        {
                            listBox1.Items.Add("Extra File " + v.FullName);
                            listBox1.TopIndex = listBox1.Items.Count - 1;
                        });
                        ExtraFiles.Add(v.FullName);
                    }
                    foreach (var v in srcListOnly)
                    {
                        await _pts.WaitWhilePausedAsync();
                        sum += v.Length;
                    }
                    foreach (var v in destListLen)
                    {
                        await _pts.WaitWhilePausedAsync();
                        filesSkipped++;
                        files--;
                        this.Invoke((MethodInvoker)delegate
                        {
                            listBox1.Items.Add("Skipped File " + v.FullName);
                            listBox1.TopIndex = listBox1.Items.Count - 1;
                        });
                        SkippedFiles.Add(v.FullName);
                        sum += v.Length;
                        this.Invoke((MethodInvoker)delegate
                        {
                            label10.Text = v.Length.ToString();
                        });
                    }
                    this.Invoke((MethodInvoker)delegate
                    {
                        label5.Text = "Files Skipped " + filesSkipped + " Extra Files " + extraFiles;
                    });
                });
                await filecompare;
                ///Get the total number of files, and their size. Do note this will take a really long time.
                ///Which is why I left it off the After6 run, because those are usually very large moves.
                ///If you are talking 100s of gigs or even coming close to TB, it would eclipse the timers
                ///trying to get the info.
                var amountcopied = Task.Run(() =>
                {
                    var existingFiles = Alphaleonis.Win32.Filesystem.Directory.GetFiles(EndDirectory, "*", SearchOption.AllDirectories);
                    var existingRoot = Alphaleonis.Win32.Filesystem.Directory.GetFiles(EndDirectory);

                    //sum = Alphaleonis.Win32.Filesystem.Directory.GetFiles(StartDirectory, "*", SearchOption.AllDirectories)
                    //        .AsParallel()
                    //        .Select(f => new Alphaleonis.Win32.Filesystem.FileInfo(f).Length)
                    //        .Sum();
                    //sum += Alphaleonis.Win32.Filesystem.Directory.GetFiles(StartDirectory)
                    //        .AsParallel()
                    //        .Select(f => new Alphaleonis.Win32.Filesystem.FileInfo(f).Length)
                    //        .Sum();
                    //files += Alphaleonis.Win32.Filesystem.Directory.EnumerateFiles(StartDirectory).Count();
                    files += Alphaleonis.Win32.Filesystem.Directory.EnumerateFiles(StartDirectory, "*", SearchOption.AllDirectories).Count();
                    while (isRunning == true)
                    {

                        endsum = Alphaleonis.Win32.Filesystem.Directory.EnumerateFiles(EndDirectory, "*", SearchOption.AllDirectories)
                                    .Except(existingFiles)
                                    .Except(existingRoot)
                                    .AsParallel()
                                    .Select(f => new Alphaleonis.Win32.Filesystem.FileInfo(f).Length)
                                    .Sum();

                        //endsum += Alphaleonis.Win32.Filesystem.Directory.GetFiles(EndDirectory)
                        //            .Except(existingFiles)
                        //            .Except(existingRoot)
                        //            .AsParallel()
                        //            .Select(f => new Alphaleonis.Win32.Filesystem.FileInfo(f).Length)
                        //            .Sum();
                        float secelasped = ((float)sw.ElapsedMilliseconds / 1000);
                        float secleft = (int)Math.Ceiling((secelasped / endsum) * (sum - endsum));
                        TimeSpan lefttime = TimeSpan.FromSeconds(secleft);
                        this.Invoke((MethodInvoker)delegate
                        {
                            label9.Text = "Speed: " + (endsum / 1024d / 1024d / sw.Elapsed.TotalSeconds).ToString("0.00") + " mb/s";
                            if (sum > 1024 && sum < 1048576)
                            {
                                label6.Text = "Amount copied  " + ((endsum / 1024d)).ToString("0.00") + "/" + ((sum / 1024d)).ToString("0.00") + " KB";
                            }
                            else if (sum > 1048576 && sum < 1073741824)
                            {
                                label6.Text = "Amount copied  " + ((endsum / 1024d) / 1024d).ToString("0.00") + "/" + ((sum / 1024d) / 1024d).ToString("0.00") + " MB";
                            }
                            else if (sum > 1073741824)
                            {
                                label6.Text = "Amount copied  " + (((endsum / 1024d) / 1024d)/1024d).ToString("0.00") + "/" + (((sum / 1024d) / 1024d)/1024d).ToString("0.00") + " GB";
                            }
                            label4.Text = "Files to Copy " + (files - filesCopied) + " Files Copied " + (filesCopied);
                            //label10.Text = "Files Copied " + (filesCopied);
                            label8.Text = "Time Remaning " + lefttime.ToString();
                            label7.Text = "Percent done " + ((100 * endsum / sum)).ToString() + "%";
                        });

                        if (sum > Int32.MaxValue)
                        {
                            progressum = sum / 1024;
                            progressvalue = endsum / 1024;
                        }
                        else
                        {
                            progressum = sum;
                            progressvalue = endsum;
                        }
                        if (endsum > Int32.MaxValue)
                        {
                            progressvalue = endsum / 1024;
                            progressum = sum / 1024;
                        }
                        else
                        {
                            progressum = sum;
                            progressvalue = endsum;
                        }

                        this.Invoke((MethodInvoker)delegate
                        {
                            progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
                            progressBar1.Minimum = 0;
                            progressBar1.Maximum = Convert.ToInt32(progressum);

                            progressBar1.Value = Convert.ToInt32(progressvalue);
                        });
                    }
                    if (isRunning == false)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            label7.Text = "";
                        });
                    }
                });
                var moveTask = Task.Run(async () =>
                {
                    sw.Start();

                    ///Note, this only works for sub directories, and will not copy over root contents, the next foreach loop takes care of that.
                    ///This is also wrapped in the same task, so it will do the sub first, then the root, and run it both on the same thread.
                    foreach (string dirPath in Alphaleonis.Win32.Filesystem.Directory.EnumerateDirectories(StartDirectory, "*", SearchOption.AllDirectories))
                    {
                        ///I initially did this with a DirectoryInfo and Alphaleonis.Win32.Filesystem.FileInfo, inplace of the style I did now. But this had a weird outcome
                        ///Where a subdir folder would be created, but the files would be copied to root. Doing it this way works
                        Alphaleonis.Win32.Filesystem.Directory.CreateDirectory(dirPath.Replace(StartDirectory, EndDirectory));

                        foreach (string filename in Alphaleonis.Win32.Filesystem.Directory.EnumerateFiles(dirPath))
                        {
                            try
                            {
                                using (FileStream SourceStream = Alphaleonis.Win32.Filesystem.File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                {
                                    using (FileStream DestinationStream = Alphaleonis.Win32.Filesystem.File.Open(filename.Replace(StartDirectory, EndDirectory), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                                    {
                                        await _pts.WaitWhilePausedAsync();
                                        string source = Alphaleonis.Win32.Filesystem.Path.GetFinalPathNameByHandle(SourceStream.SafeFileHandle);
                                        string destination = Alphaleonis.Win32.Filesystem.Path.GetFinalPathNameByHandle(DestinationStream.SafeFileHandle);
                                        ///Check if the length match (since the file is created above)
                                        ///If the length is not right, it restarts the copy (meaning if stopped in the middle of copying it will
                                        ///start at the beginning rather than the same byte [TODO: Make it start the previous byte])
                                        ///Else it closes the current stream, and displays it already exists. Because of the foreach
                                        ///It will recursively start a new stream with the next file
                                        if (Alphaleonis.Win32.Filesystem.File.Exists(destination) && DestinationStream.Length == SourceStream.Length)
                                        {
                                            this.Invoke((MethodInvoker)delegate
                                            {
                                                listBox1.Items.Add("Skipping files. Please wait");
                                                listBox1.TopIndex = listBox1.Items.Count - 1;
                                            });
                                            //filesSkipped++;
                                            //files--;
                                            //endsum += DestinationStream.Length;
                                            //SkippedFiles.Add(destination);
                                            DestinationStream.Close();
                                            SourceStream.Close();
                                        }
                                        else if (Alphaleonis.Win32.Filesystem.File.Exists(source) && DestinationStream.Length != SourceStream.Length)
                                        {
                                            this.Invoke((MethodInvoker)delegate
                                            {
                                                listBox1.Items.Add("Starting Copy of  " + source);
                                                listBox1.TopIndex = listBox1.Items.Count - 1;
                                            });

                                            await SourceStream.CopyToAsync(DestinationStream, 262144, token);
                                            this.Invoke((MethodInvoker)delegate
                                            {
                                                listBox1.Items.Add("Finished Copying  " + destination);
                                                listBox1.TopIndex = listBox1.Items.Count - 1;
                                            });
                                            filesCopied++;
                                            FileNames.Add(destination);
                                        }

                                        token.ThrowIfCancellationRequested();

                                    }

                                }
                            }
                            catch (Exception ex)
                            {
                                this.Invoke((MethodInvoker)delegate
                                {
                                    listBox1.Items.Add(ex);
                                    listBox1.Items.Add("File move (subdir) date caused it");
                                });
                                throw;
                            }
                        }
                    }
                    foreach (string filename in Alphaleonis.Win32.Filesystem.Directory.EnumerateFiles(StartDirectory))
                    {
                        try
                        {
                            using (FileStream SourceStream = Alphaleonis.Win32.Filesystem.File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                using (FileStream DestinationStream = Alphaleonis.Win32.Filesystem.File.Open(EndDirectory + filename.Substring(filename.LastIndexOf('\\')), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                                {
                                    await _pts.WaitWhilePausedAsync();
                                    string source = Alphaleonis.Win32.Filesystem.Path.GetFinalPathNameByHandle(SourceStream.SafeFileHandle);
                                    string destination = Alphaleonis.Win32.Filesystem.Path.GetFinalPathNameByHandle(DestinationStream.SafeFileHandle);
                                    if (Alphaleonis.Win32.Filesystem.File.Exists(destination) && DestinationStream.Length == SourceStream.Length)
                                    {
                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            listBox1.Items.Add("Skipping files. Please wait");
                                            listBox1.TopIndex = listBox1.Items.Count - 1;
                                        });
                                        //filesSkipped++;
                                        //files--;
                                        //endsum += DestinationStream.Length;
                                        //SkippedFiles.Add(destination);
                                        DestinationStream.Close();
                                        SourceStream.Close();
                                    }
                                    else if (Alphaleonis.Win32.Filesystem.File.Exists(source) && DestinationStream.Length != SourceStream.Length)
                                    {
                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            listBox1.Items.Add("Starting Copy of  " + source);
                                            listBox1.TopIndex = listBox1.Items.Count - 1;
                                        });

                                        await SourceStream.CopyToAsync(DestinationStream, 262144, token);
                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            listBox1.Items.Add("Finished Copying  " + destination);
                                            listBox1.TopIndex = listBox1.Items.Count - 1;
                                        });
                                        filesCopied++;
                                        FileNames.Add(destination);
                                    }
                                    token.ThrowIfCancellationRequested();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                listBox1.Items.Add(ex);
                                listBox1.Items.Add("File move (rootdir) date caused it");
                                listBox1.TopIndex = listBox1.Items.Count - 1;
                            });
                            throw;
                        }
                    }
                });
                await moveTask;
                isRunning = false;
                var modify = Task.Run(async () =>
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        listBox1.Items.Add("Setting modify and change date. Please wait");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });

                    foreach (string filename in Alphaleonis.Win32.Filesystem.Directory.EnumerateFiles(StartDirectory))
                    {
                        try
                        {
                            using (FileStream SourceStream = Alphaleonis.Win32.Filesystem.File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                using (FileStream DestinationStream = Alphaleonis.Win32.Filesystem.File.Open(EndDirectory + filename.Substring(filename.LastIndexOf('\\')), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                                {
                                    await _pts.WaitWhilePausedAsync();
                                    string source = Alphaleonis.Win32.Filesystem.Path.GetFinalPathNameByHandle(SourceStream.SafeFileHandle);
                                    string destination = Alphaleonis.Win32.Filesystem.Path.GetFinalPathNameByHandle(DestinationStream.SafeFileHandle);
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        listBox1.Items.Add("Changing Modify, Creation, and Change date for " + destination);
                                        listBox1.TopIndex = listBox1.Items.Count - 1;
                                    });
                                    DateTime dt = Alphaleonis.Win32.Filesystem.File.GetCreationTime(source);
                                    DateTime at = Alphaleonis.Win32.Filesystem.File.GetLastAccessTime(source);
                                    DateTime wt = Alphaleonis.Win32.Filesystem.File.GetLastWriteTime(source);
                                    Alphaleonis.Win32.Filesystem.File.SetCreationTime(destination, dt);
                                    Alphaleonis.Win32.Filesystem.File.SetLastAccessTime(destination, at);
                                    Alphaleonis.Win32.Filesystem.File.SetLastWriteTime(destination, wt);
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        listBox1.Items.Add("Modify, Creation, and Change date set for " + destination);
                                        listBox1.TopIndex = listBox1.Items.Count - 1;
                                    });

                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                listBox1.Items.Add(ex);
                                listBox1.Items.Add("File modify date caused it");
                                listBox1.TopIndex = listBox1.Items.Count - 1;
                            });
                            throw;
                        }
                    }
                    foreach (string dirPath in Alphaleonis.Win32.Filesystem.Directory.EnumerateDirectories(StartDirectory, "*", SearchOption.AllDirectories))
                    {
                        foreach (string filename in Alphaleonis.Win32.Filesystem.Directory.EnumerateFiles(dirPath))
                        {
                            try
                            {
                                using (FileStream SourceStream = Alphaleonis.Win32.Filesystem.File.Open(filename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                                {
                                    using (FileStream DestinationStream = Alphaleonis.Win32.Filesystem.File.Open(filename.Replace(StartDirectory, EndDirectory), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                                    {
                                        await _pts.WaitWhilePausedAsync();
                                        string source = Alphaleonis.Win32.Filesystem.Path.GetFinalPathNameByHandle(SourceStream.SafeFileHandle);
                                        string destination = Alphaleonis.Win32.Filesystem.Path.GetFinalPathNameByHandle(DestinationStream.SafeFileHandle);
                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            listBox1.Items.Add("Changing Modify, Creation, and Change date for " + destination);
                                            listBox1.TopIndex = listBox1.Items.Count - 1;
                                        });
                                        DateTime dt = Alphaleonis.Win32.Filesystem.File.GetCreationTime(source);
                                        DateTime at = Alphaleonis.Win32.Filesystem.File.GetLastAccessTime(source);
                                        DateTime wt = Alphaleonis.Win32.Filesystem.File.GetLastWriteTime(source);
                                        Alphaleonis.Win32.Filesystem.File.SetCreationTime(destination, dt);
                                        Alphaleonis.Win32.Filesystem.File.SetLastAccessTime(destination, at);
                                        Alphaleonis.Win32.Filesystem.File.SetLastWriteTime(destination, wt);
                                        this.Invoke((MethodInvoker)delegate
                                        {
                                            listBox1.Items.Add("Modify, Creation, and Change date set for " + destination);
                                            listBox1.TopIndex = listBox1.Items.Count - 1;
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                this.Invoke((MethodInvoker)delegate
                                {
                                    listBox1.Items.Add(ex);
                                    listBox1.Items.Add("File modify date caused it");
                                    listBox1.TopIndex = listBox1.Items.Count - 1;
                                });
                                throw;
                            }
                        }
                    }
                    DateTime dirt = new DateTime();
                    DateTime dira = new DateTime();
                    DateTime dirw = new DateTime();
                    foreach (string dirPath in Alphaleonis.Win32.Filesystem.Directory.GetDirectories(StartDirectory, "*", SearchOption.AllDirectories))
                    {
                        dirt = Alphaleonis.Win32.Filesystem.Directory.GetCreationTime(dirPath);
                        dira = Alphaleonis.Win32.Filesystem.Directory.GetLastAccessTime(dirPath);
                        dirw = Alphaleonis.Win32.Filesystem.Directory.GetLastWriteTime(dirPath);
                    }
                    foreach (string endDirPath in Alphaleonis.Win32.Filesystem.Directory.EnumerateDirectories(EndDirectory, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            await _pts.WaitWhilePausedAsync();
                            this.Invoke((MethodInvoker)delegate
                            {
                                listBox1.Items.Add("Changing Modify, Creation, and Change date for " + endDirPath);
                                listBox1.TopIndex = listBox1.Items.Count - 1;
                            });
                            Alphaleonis.Win32.Filesystem.Directory.SetCreationTime(endDirPath, dirt);
                            Alphaleonis.Win32.Filesystem.Directory.SetLastAccessTime(endDirPath, dira);
                            Alphaleonis.Win32.Filesystem.Directory.SetLastWriteTime(endDirPath, dirw);
                            this.Invoke((MethodInvoker)delegate
                            {
                                listBox1.Items.Add("Modify, Creation, and Change date set for " + endDirPath);
                                listBox1.TopIndex = listBox1.Items.Count - 1;
                            });
                        }
                        catch (Exception ex)
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                listBox1.Items.Add(ex);
                                listBox1.Items.Add("Directory modify date caused it");
                                listBox1.TopIndex = listBox1.Items.Count - 1;
                            });
                            throw;
                        }
                    }
                });
                await modify;
                if (checkBox2.Checked)
                {
                    if (Alphaleonis.Win32.Filesystem.Directory.Exists(SrcPath.Text))
                    {
                        try
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                listBox1.Items.Add("Deleting the source directory, please wait.");
                                listBox1.TopIndex = listBox1.Items.Count - 1;
                            });

                            var folderdelete = Task.Run(async () =>
                            {
                                Alphaleonis.Win32.Filesystem.Directory.Delete(SrcPath.Text, true);
                            }, token);
                            await folderdelete;
                        }

                        catch (System.IO.IOException ex)
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                listBox1.Items.Add(ex);
                                listBox1.Items.Add("Folder Delete caused it");
                                listBox1.TopIndex = listBox1.Items.Count - 1;
                            });
                            throw;
                        }

                    }
                    this.Invoke((MethodInvoker)delegate
                    {
                        listBox1.Items.Add("**********File Move has Completed!*****");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });
                    PrepareControlsForCancel();
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        listBox1.Items.Add("**********File Copy has Completed!*****");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });
                    PrepareControlsForCancel();
                }

            }
            catch (OperationCanceledException)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    listBox1.Items.Add("Cancelled.");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
                PrepareControlsForCancel();
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    listBox1.Items.Add(ex);
                });
                ErrorLog(ex, logfile, StartDirectory);
            }
            FileLog(StartDirectory, logfile, FileNames, SkippedFiles, filesCopied, endsum, filesSkipped, extraFiles, ExtraFiles);
        }
        public static void ErrorLog(Exception ex, string logdirectory, string startdirectory)
        {
            //string erPath = Alphaleonis.Win32.Filesystem.Path.Combine(startdirectory, "Errors log");
            //Alphaleonis.Win32.Filesystem.Directory.CreateDirectory(erPath);
            string erFile = "ErrLog.txt";
            string erPath = Alphaleonis.Win32.Filesystem.Path.Combine(logdirectory, erFile);

            if (!Alphaleonis.Win32.Filesystem.File.Exists(erPath))
            {
                Alphaleonis.Win32.Filesystem.File.Create(erPath).Dispose();
            }

            using (StreamWriter sw = Alphaleonis.Win32.Filesystem.File.AppendText(erPath))
            {
                sw.WriteLine("=============Error Logging ===========");
                sw.WriteLine("===========Start============= " + DateTime.Now);
                sw.WriteLine("Error Message: " + ex.Message);
                sw.WriteLine("Stack Trace: " + ex.StackTrace);
                sw.WriteLine("===========End============= " + DateTime.Now);
            }
        }
        public static void FileLog(string startdirectory, string logdir, List<String> filename, List<String> skippedfiles, int files, long endsum, int filesSkipped, int extrafiles, List<String> extras)
        {
            //string filPath = Alphaleonis.Win32.Filesystem.Path.Combine(logdir, "File Log");
            //Alphaleonis.Win32.Filesystem.Directory.CreateDirectory(filPath);

            string erFile = "FileLog.txt";
            string filPath = Alphaleonis.Win32.Filesystem.Path.Combine(logdir, erFile);

            if (!Alphaleonis.Win32.Filesystem.File.Exists(filPath))
            {
                Alphaleonis.Win32.Filesystem.File.Create(filPath).Dispose();
            }
            using (StreamWriter sw = Alphaleonis.Win32.Filesystem.File.AppendText(filPath))
            {
                sw.WriteLine("=============Files Copied/Moved ===========");
                sw.WriteLine("===========Start============= " + DateTime.Now);
                filename.ForEach(sw.WriteLine);
                sw.WriteLine("===========End============= " + DateTime.Now);
                sw.WriteLine("");
                sw.WriteLine("");
                sw.WriteLine("");
                if (filesSkipped > 0)
                {
                    sw.WriteLine("=============Files Skipped ===========");
                    sw.WriteLine("===========Start============= " + DateTime.Now);
                    skippedfiles.ForEach(sw.WriteLine);
                    sw.WriteLine("===========End============= " + DateTime.Now);
                    sw.WriteLine("");
                    sw.WriteLine("");
                    sw.WriteLine("");
                }
                if (extrafiles > 0)
                {
                    sw.WriteLine("=============Extra Files ===========");
                    sw.WriteLine("===========Start============= " + DateTime.Now);
                    extras.ForEach(sw.WriteLine);
                    sw.WriteLine("===========End============= " + DateTime.Now);
                    sw.WriteLine("");
                    sw.WriteLine("");
                    sw.WriteLine("");
                }
                sw.WriteLine("Files copied: " + files);
                sw.WriteLine("Amount copied: " + (endsum / 1024) / 1024 + "MBs");
                sw.WriteLine("Files skipped: " + filesSkipped);
                sw.WriteLine("Extra Files: " + extrafiles);
            }
        }
    }
}   
      
