using System;
using System.Windows.Forms;
using System.IO;

using static FolderMove.FolderMoveWindow;


namespace FolderMove
{

    class TimerMove
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FolderMoveWindow());
            
        }
        
    }
}
