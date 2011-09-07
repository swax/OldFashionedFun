using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using System.Runtime.InteropServices;


namespace OldFashionedFun
{
    public partial class MainForm : Form
    {
        public Win32.EnumWindowsProc EnumWindow;

        NotifyIcon TrayIcon = new NotifyIcon();

        List<IntPtr> WinHandles = new List<IntPtr>(); 
        Dictionary<IntPtr, WinInfo> WindowPos = new Dictionary<IntPtr, WinInfo>();

        double DownAcceleration = 12000; // px/s^2 earth gravity is 38582
        double UpAcceleration = -24000.0;

        List<WinInfo> MoveWindows = new List<WinInfo>();
        List<Win32.RECT> Shelves = new List<Win32.RECT>();

        const int ShelfTest = 5;


        public MainForm()
        {
            InitializeComponent();

            EnumWindow = new Win32.EnumWindowsProc(OnEnumWindow);

            TrayIcon.ContextMenu = new ContextMenu(new MenuItem[] {
                new MenuItem("About", (s, e) => MessageBox.Show(
@"We're gathered here tonight to have a fight but I say man that's not all right. 
Violence is not the solution it just adds to all this pollution. 
Lets talk about our problems one on one then we can have some fun. 
Well I'm David Wain and I'm here to say there's got to be a better way. 
Fighting's not good activity painting, drawing, and dancing yo. 
Now I'd like to introduce my friend Michael Show. 
AKKKAHAHAHAHA HA HAWAHAHABOOM! CHACKALACKABOOMCHACKALAKA BOOM SHAKALAKA
BOOM SHAKALAKA GET UP!!! GET UP!!! GET UP!!! YEEEAAAAA!!!
Yah! Now rap is in we hope you agree that the way to go is posativity our 
message is simple lets all get along that's why we call this rap the 
friendship song! the friendship song! the friendship song!", "Old Fashioned Fun" )),

                new MenuItem("Exit", (s, e) => Close())
            });

            TrayIcon.Icon = Properties.Resources.bike;
            TrayIcon.Visible = true;
        }


        public bool OnEnumWindow(IntPtr hwnd, IntPtr lParam)
        {
            if (ShouldWindowBeDisplayed(hwnd))
                freshWindows.Add(hwnd);

            return true;
        }

        private bool ShouldWindowBeDisplayed(IntPtr window)
        {
            uint windowStyles = (uint)Win32.GetWindowLongPtr(window, (int)Win32.GStyles.GWL_STYLE);

            return ((uint)Win32.WindowStyles.WS_VISIBLE & windowStyles) > 0;
        }


        private void StepTimer_Tick(object sender, EventArgs e)
        {
           // winHandles.Clear();
           // winHandles.Add( Win32.FindWindow("Notepad", "todo.txt - Notepad"));

            MoveWindows.Clear();

            Win32.RECT rect = new Win32.RECT();

            // first pass select what windows we should maniplate
            foreach (IntPtr handle in WinHandles)
            {
                if(!Win32.GetWindowRect(handle, ref rect))
                    continue;

                // if off screen or minimized
                if (rect.Top == -32000 || 
                    (rect.Left == 0 && rect.Top == TaskbarTop))
                    continue;

                // if the height is bigger the the vertical space, dont move
                if (rect.Height > Desktop.Height - Abd.rc.Height || rect.Width > Desktop.Width)
                    continue;

                if (!WindowPos.ContainsKey(handle))
                    WindowPos[handle] = new WinInfo();

                WinInfo info = WindowPos[handle];
                info.Rect = rect;
                info.Handle = handle;

                MoveWindows.Add(info);
            }


            // of those windows what are the shelves, windows other windows can rest on
            Shelves.Clear();
          
            foreach (WinInfo info in MoveWindows)
            {
                 // shelf may need to be broken into multiple pieces depending on whats covering it
                Recurse = 0;
                TestShelf(info.Handle, info.Rect);
            }

            IntPtr activeWindow = Win32.GetForegroundWindow();

            // move the windows according to gravity and the shelf space
            foreach (WinInfo info in MoveWindows)
            {
                IntPtr handle = info.Handle;
                rect = info.Rect;
                
                double dT = (double)(DateTime.Now.Ticks - info.LastRun.Ticks) / (double)TimeSpan.TicksPerSecond;


                // prevent window from dropping while user is holding it
                short keyState = Win32.GetKeyState(Win32.VirtualKeyStates.VK_LBUTTON);

                bool mouseDown = (keyState & Win32.KEY_PRESSED) > 0;

                Win32.POINT cursorPos;
                Win32.GetCursorPos(out cursorPos); // used also at end of func

                // only give window momentum if dragging the header
                if (mouseDown && activeWindow == handle &&
                    rect.Left < cursorPos.X && cursorPos.X < rect.Right &&
                    rect.Top < cursorPos.Y && cursorPos.Y < rect.Top + 26)
                {
                    info.Vx = (double)(cursorPos.X - info.LastX) / dT;
                    info.Vy = (double)(cursorPos.Y - info.LastY) / dT;

                    /*smush - cant resize window while dragging for some reason
                    // if drag started off screen ignore smush

                    if (rect.Right > Abd.rc.Width)
                    {
                        int width = Abd.rc.Width - rect.Left;

                        uint windowStyles = (uint)Win32.GetWindowLongPtr(handle, (int)Win32.GStyles.GWL_STYLE);

                        Win32.SetWindowPos(handle, IntPtr.Zero, rect.Left, rect.Top, width, rect.Height, 0);
                        //Win32.MoveWindow(handle, rect.Left, rect.Top, width, rect.Height, false);
                    }
                    else if (rect.Left < 0)
                    {

                    }*/
                }

                int moveX = 0;
                int moveY = 0;

                // move window in x direction
                if (!mouseDown)
                {
                    double Ax = 0;

                    if (info.Vx > 0) // moving left, slow down right
                        Ax = -12000; 
                    else if (info.Vx < 0) // moving left, slow down right
                        Ax = 12000; 

                    // if past right side , bounce back
                    if (rect.Right + moveX > Abd.rc.Width)
                        Ax = -12000;
                    else if (rect.Left + moveX < 0)
                        Ax = 12000;

                    // calc the amount of next move given current velocity and acceleration during time change
                    moveX = (int)(info.Vx * dT + Ax * Math.Pow(dT, 2)); // x = vt + at^2

                    // if passing right side
                    if (rect.Right < Abd.rc.Width && rect.Right + moveX > Abd.rc.Width)
                    {
                        moveX = Bounce(dT, Abd.rc.Width - rect.Right, ref info.Vx, Ax);

                        // if still past bounds after second bounce, move into place
                        if (rect.Right + moveX > Abd.rc.Width)
                        {
                            info.Vx = 0;
                            moveX = Abd.rc.Width - rect.Right;
                        }
                    }
                    // if passing left side
                    else if (rect.Left > 0 && rect.Left + moveX < 0)
                    {
                        moveX = Bounce(dT, -rect.Left, ref info.Vx, Ax);

                        // if still past bounds after second bounce, move into place
                        if (rect.Left + moveX < 0)
                        {
                            info.Vx = 0;
                            moveX = 0 - rect.Left;
                        }
                    }

                    // if window coast in x in one direction and starts going in other, then stop it
                    double newVx = info.Vx + Ax * dT;

                    if (info.Vx > 0 && newVx < 0) // moving left
                        info.Vx = 0;
                    else if (info.Vx < 0 && newVx > 0) // moving right
                        info.Vx = 0;
                    else
                        info.Vx = newVx;
                }

                // not on taskbar and mouse not down,  drop it or pop it
                if (!(mouseDown && activeWindow == handle) && !OnShelf(info))
                {
                    double Ay = 0;

                    // if the top is above the taskbar move down
                    if (rect.Bottom < TaskbarTop)
                        Ay = DownAcceleration;

                    // else the bottom is below the taskbar, move up
                    else
                        Ay = UpAcceleration;

                    moveY = (int)(info.Vy * dT + Ay * Math.Pow(dT, 2)); // x = vt + at^2

                    // if moving down, set target shelf
                    if (Ay > 0)
                        info.TargetShelf = GetTargetShelf(info);

                    // bounce - if accelerating down, and moving through bottom
                    if (Ay > 0 && rect.Bottom + moveY > info.TargetShelf)
                    {
                        moveY = Bounce(dT, info.TargetShelf - rect.Bottom, ref info.Vy, Ay);

                        // if still below taskbar, thats two bounces below, end it here
                        if (rect.Bottom + moveY > info.TargetShelf)
                        {
                            info.Vy = 0;
                            moveY = info.TargetShelf - rect.Bottom;
                        }
                    }
                    // not hitting anything, move normally
                    else
                    {
                        // v = a * t
                        info.Vy = info.Vy + Ay * dT;
                    }

                    //System.Diagnostics.Debug.WriteLine(DateTime.Now.Millisecond.ToString() + " - h:" + handle.ToString() + " x:" + (rect.Top + moveY).ToString() + " dx:" + moveY.ToString() + " v:" + info.Vy.ToString());
                }

                if (moveY != 0 || moveX != 0)
                    Win32.MoveWindow(handle, rect.Left + moveX, rect.Top + moveY, rect.Width, rect.Height, false);

                // always set this if either run move or not
                info.LastRun = DateTime.Now;
                info.LastX = cursorPos.X;
                info.LastY = cursorPos.Y;
            }
        }

        int Recurse;

        private void TestShelf(IntPtr handle, Win32.RECT shelf)
        {
            if (shelf.Width == 0)
                return;

            // test top 3 points on window
            Win32.POINT testPoint = new Win32.POINT();

            Recurse++;
            if (Recurse > 5)
            {
                int i = 0;
                i++;
                return;
            }

            for (int i = 0; i < ShelfTest; i++)
            {
                testPoint.X = shelf.Left + (shelf.Width-1) * i / (ShelfTest-1); // 0/4, 5 points, is 4 widths
                testPoint.Y = shelf.Top;

                IntPtr maybeChild = Win32.WindowFromPoint(testPoint);
                IntPtr winAtPoint = Win32.GetParent(maybeChild);
                if (winAtPoint == IntPtr.Zero)
                    winAtPoint = maybeChild;

                // if we're on top, continue
                if (winAtPoint == handle)
                    continue;

                Win32.RECT rect = new Win32.RECT();
                if (!Win32.GetWindowRect(winAtPoint, ref rect))
                    continue;

                // left side point
                if(i == 0)
                {
                    // test if shelf completely covered, return if so
                    if (rect.Left <= shelf.Left && shelf.Right <= rect.Right)
                        return;

                    // left side of shelf is covered, mod it to covering window
                    shelf.Left = rect.Right;
                }
                // interior point
                else if(i < ShelfTest - 1)
                {
                    // check if right side covered
                    if (rect.Right > shelf.Right)
                    {
                        shelf.Right = rect.Left;
                        break;
                    }

                    // shelf is split into two, recurse right side
                    Win32.RECT rightShelf = shelf;
                    rightShelf.Left = rect.Right + 1 ;
                    TestShelf(handle, rightShelf);

                    // add left side
                    shelf.Right = rect.Left;
                    break;
                }
                // right point
                else
                {
                    // set right of shelf to covering windows extent
                    shelf.Right = rect.Left;
                }
            }

            Shelves.Add(shelf);
        }

        private int GetTargetShelf(WinInfo info)
        {
            Win32.RECT rect = info.Rect;

            int closest = int.MaxValue;
            int shelfTop = TaskbarTop;

            // check taskbar top
            if (TaskbarTop - rect.Bottom > 0 &&
                TaskbarTop - rect.Bottom < closest)
            {
                closest = TaskbarTop - rect.Bottom;
                shelfTop = TaskbarTop;
            }

            // no way to check which window is above what yet, see button click for options
            // check all MoveWindows
            foreach (Win32.RECT shelf in Shelves)
                if (shelf != rect &&
                    shelf.Top - rect.Bottom > 0 &&
                    shelf.Top - rect.Bottom < closest)
                    if ((shelf.Left < rect.Right && rect.Right < shelf.Right) ||
                       (shelf.Left < rect.Left && rect.Left < shelf.Right) ||
                       (rect.Left < shelf.Left && shelf.Right < rect.Right))
                    {
                        closest = shelf.Top - rect.Bottom;
                        shelfTop = shelf.Top;
                    }

            return shelfTop;
        }

        bool OnShelf(WinInfo info)
        {
            Win32.RECT rect = info.Rect;

            // check taskbar top
            if (TaskbarTop == rect.Bottom)
                return true;

            // check all MoveWindows
            foreach(Win32.RECT shelf in Shelves)
                if (shelf != rect && shelf.Top == rect.Bottom)
                    if ((shelf.Left < rect.Right && rect.Right < shelf.Right) ||
                        (shelf.Left < rect.Left && rect.Left < shelf.Right) ||
                        (rect.Left < shelf.Left && shelf.Right < rect.Right))
                   return true;
           
            return false;
        }

        private int Bounce(double dT, int groundDistance, ref double vel, double accel)
        {
            // find time to impact
            // 0 = Vt * At^2 - X
            double a = accel;
            double b = vel;
            double c = -groundDistance;

            double sqr = Math.Sqrt(Math.Pow(b, 2) - 4.0 * a * c);
            double root1 = (-b + sqr) / (2.0 * a);
            double root2 = (-b - sqr) / (2.0 * a);
            
             // take the root thats with in the time change - probably not the right way to find the right root..
             double downTime = root1;
             if (0 <= root2 && root2 <= dT)
                 downTime = root2;

            // find velocity at impact
            double hitVelocity = vel + accel * downTime;
            
            // bounce, reverse velocity, absorb energy
            vel = hitVelocity * -0.3;

            double upTime = dT - downTime;
            int move = (int)(vel * upTime + accel * Math.Pow(upTime, 2)); // x = vt + at^2
            vel += accel * upTime;

            return move;
        }

        class WinInfo
        {           
            public double Vy;
            public double Vx;

            public DateTime LastRun = DateTime.Now;
            public int LastX;
            public int LastY;

            public IntPtr Handle;
            public Win32.RECT Rect;

            public int TargetShelf;
        }



        List<IntPtr> freshWindows = new List<IntPtr>();
        Win32.APPBARDATA Abd;
        int TaskbarTop;
        Win32.RECT Desktop;

        bool onoff = true;
        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (onoff)
            {
                freshWindows = new List<IntPtr>();
                
                Win32.EnumWindows(EnumWindow, IntPtr.Zero);

                Abd = new Win32.APPBARDATA();
                Win32.SHAppBarMessage(Win32.ABM_GETTASKBARPOS, ref Abd);
                TaskbarTop = Abd.rc.Top;

                Desktop = new Win32.RECT();
                Win32.GetWindowRect(Win32.GetDesktopWindow(), ref Desktop);

                onoff = false;
            }
            else
            {
                WinHandles = freshWindows;
                onoff = true;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            foreach (Win32.RECT shelf in Shelves)
            {
                Debug.WriteLine(string.Format("Top:{0}, Left:{1}, Right:{2}, Width{3}",
                    shelf.Top, shelf.Left, shelf.Right, shelf.Width));
                Debug.WriteLine("");
            }



            /*IntPtr handle =  Win32.GetTopWindow(IntPtr.Zero);

            Stopwatch sw = new Stopwatch();
            sw.Start();

            int i = 0;

            IntPtr next = handle;

            // this process takes forever
            while (next != IntPtr.Zero)
            {
                next = Win32.GetWindow(next, (uint)(Win32.GetWindow_Cmd.GW_HWNDNEXT ));
                i++;
            }

            */


            // can probably do some top sampling to see if top of window is hidden


            // just do 3 samples per window top
            /*Random rnd = new Random();

            for (int i = 0; i < 100; i++)
            {
                // this is not very accurate, but runs very quick
                Win32.WindowFromPoint(new Win32.POINT(rnd.Next(1900), rnd.Next(1200)));
            }


            long x = sw.ElapsedMilliseconds;*/
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            TrayIcon.Dispose();

        }
    }

}