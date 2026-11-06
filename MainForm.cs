using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace StreamDeckPC
{
    internal class MainForm : Form
    {
        static readonly Color Bg = Theme.Bg;
        static readonly Color PanelBg = Theme.PanelBg;
        static readonly Color CardBg = Theme.CardBg;
        static readonly Color Fg = Theme.Fg;
        static readonly Color Muted = Theme.Muted;
        static readonly Color Accent = Theme.Accent;
        static readonly Color Line = Theme.Line;

        DeckConfig cfg;
        DeckServer server;
        Caster caster;
        string configPath;
        string iconsDir;
        string backgroundsDir;
        int dragFrom = -1;
        bool dragging;

        TableLayoutPanel grid;
        Button[] cells = new Button[0];
        int selected = 0;
        bool loadingFields;
        bool syncingCast;

        Label lblUrl, lblSub, lblHint, lblResult, lblNet, lblBg, lblStatus, lblCast, lblSection;
        TextBox txtLabel, txtIcon, txtTarget, txtCastIp;
        DarkCombo cmbAction, cmbBgPos, cmbClock, cmbClockFmt;
        Button btnColor;
        Button btnToggle;
        NumericUpDown numCols, numRows;
        UiCheck chkCastOn;
        System.Windows.Forms.Timer netTimer, uiTimer;
        NotifyIcon tray;
        ContextMenuStrip trayMenu;
        ToolStripMenuItem miServer, miCastOn, miCastNow, miCastStatus, miStartWin, miStartTray;
        readonly EventWaitHandle showEvent;
        Icon appIcon;
        bool startHidden;
        bool exiting;
        bool tipShown;
        List<string> ipCache;
        DateTime ipCacheAt = DateTime.MinValue;

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "StreamDeckPC";

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        readonly string[] actionKeys =
        {
            "app", "url", "cmd", "powershell", "media", "key", "text",
            "lock", "sleep", "monitoroff", "shutdown", "restart", "none"
        };

        readonly string[] actionNames =
        {
            "Open app / file",
            "Open website",
            "Run CMD command",
            "Run PowerShell",
            "Media key",
            "Send keys",
            "Type text",
            "Lock PC",
            "Sleep",
            "Monitor off",
            "Shutdown",
            "Restart",
            "No action"
        };

        public MainForm(EventWaitHandle showHandle)
        {
            showEvent = showHandle;

            configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deck-config.txt");
            iconsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons");
            backgroundsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backgrounds");
            cfg = DeckConfig.Load(configPath);
            cfg.Normalize();
            MigrateIcons();

            bool trayArg = false;
            foreach (string a in Environment.GetCommandLineArgs())
            {
                if (string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase)) trayArg = true;
            }
            startHidden = trayArg || cfg.StartInTray;

            Text = "Stream Deck PC";
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1120, 930);
            MinimumSize = new Size(1020, 830);
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(6);
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            KeyPreview = true;

            appIcon = LoadAppIcon();
            Icon = appIcon;

            BuildRoot();
            RebuildGrid();

            caster = new Caster();
            caster.DeviceIp = cfg.CastIp;
            caster.Enabled = false;
            caster.Start();

            SetupTray();
            StartServer();
            UpdateCastStatus();
            CreateDesktopShortcut(true);

            netTimer = new System.Windows.Forms.Timer();
            netTimer.Interval = 30000;
            netTimer.Tick += delegate { RefreshUrl(); };
            netTimer.Start();

            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 2000;
            uiTimer.Tick += delegate { UpdateCastStatus(); };
            uiTimer.Start();

            WatchForShow();

            if (startHidden)
            {
                System.Windows.Forms.Timer hint = new System.Windows.Forms.Timer();
                hint.Interval = 1600;
                hint.Tick += delegate
                {
                    hint.Stop();
                    hint.Dispose();
                    ShowTrayTip();
                };
                hint.Start();
            }
        }

        void BuildRoot()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 216f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 292f));
            root.BackColor = Bg;
            root.Margin = new Padding(0);
            Controls.Add(root);

            root.Controls.Add(BuildTitleBar(), 0, 0);
            root.Controls.Add(BuildHeader(), 0, 1);
            root.Controls.Add(BuildGridArea(), 0, 2);
            root.Controls.Add(BuildEditor(), 0, 3);
        }

        Control BuildTitleBar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Color.FromArgb(12, 12, 15);
            bar.Margin = new Padding(0);

            PictureBox icon = new PictureBox();
            icon.Size = new Size(22, 22);
            icon.Location = new Point(18, 12);
            icon.SizeMode = PictureBoxSizeMode.StretchImage;
            try { icon.Image = new Bitmap(appIcon.ToBitmap(), 22, 22); }
            catch { }
            icon.BackColor = Color.Transparent;
            bar.Controls.Add(icon);

            Label title = new Label();
            title.AutoSize = false;
            title.Location = new Point(48, 6);
            title.Size = new Size(360, 20);
            title.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            title.ForeColor = Fg;
            title.BackColor = Color.Transparent;
            title.Text = "Stream Deck PC";
            bar.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.AutoSize = false;
            subtitle.Location = new Point(49, 25);
            subtitle.Size = new Size(360, 16);
            subtitle.Font = new Font("Segoe UI", 7.75f);
            subtitle.ForeColor = Muted;
            subtitle.BackColor = Color.Transparent;
            subtitle.Text = "Control center  ·  runs in the background";
            bar.Controls.Add(subtitle);

            UiButton close = new UiButton();
            close.Size = new Size(34, 28);
            close.Text = "\u2715";
            close.Font = new Font("Segoe UI", 9.5f);
            close.Radius = 7;
            close.DrawBorder = false;
            close.BackColor = bar.BackColor;
            close.BorderColor = bar.BackColor;
            close.HoverColor = Color.FromArgb(122, 42, 42);
            close.TabStop = false;
            close.Click += delegate { HideToTray(); };
            bar.Controls.Add(close);

            UiButton min = new UiButton();
            min.Size = new Size(34, 28);
            min.Text = "\u2013";
            min.Font = new Font("Segoe UI", 10f);
            min.Radius = 7;
            min.DrawBorder = false;
            min.BackColor = bar.BackColor;
            min.BorderColor = bar.BackColor;
            min.HoverColor = Color.FromArgb(34, 34, 42);
            min.TabStop = false;
            min.Click += delegate { WindowState = FormWindowState.Minimized; };
            bar.Controls.Add(min);

            Panel line = new Panel();
            line.Dock = DockStyle.Bottom;
            line.Height = 1;
            line.BackColor = Line;
            bar.Controls.Add(line);

            bar.Resize += delegate
            {
                close.Location = new Point(bar.Width - 44, 8);
                min.Location = new Point(bar.Width - 82, 8);
            };

            MouseEventHandler grab = delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
            };
            bar.MouseDown += grab;
            title.MouseDown += grab;
            subtitle.MouseDown += grab;
            icon.MouseDown += grab;

            return bar;
        }

        Control BuildHeader()
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Bg;
            host.Padding = new Padding(14, 10, 14, 6);
            host.Margin = new Padding(0);

            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.FillColor = PanelBg;
            card.BorderColor = Line;
            card.Margin = new Padding(0);
            host.Controls.Add(card);

            lblUrl = new Label();
            lblUrl.AutoSize = false;
            lblUrl.Location = new Point(20, 10);
            lblUrl.Size = new Size(650, 30);
            lblUrl.Font = new Font("Consolas", 16.5f, FontStyle.Bold);
            lblUrl.ForeColor = Accent;
            lblUrl.BackColor = Color.Transparent;
            lblUrl.Text = "http://...";
            card.Controls.Add(lblUrl);

            lblStatus = new Label();
            lblStatus.AutoSize = false;
            lblStatus.Size = new Size(130, 22);
            lblStatus.Location = new Point(900, 15);
            lblStatus.TextAlign = ContentAlignment.MiddleRight;
            lblStatus.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblStatus.ForeColor = Muted;
            lblStatus.BackColor = Color.Transparent;
            lblStatus.Text = "\u25CF Offline";
            card.Controls.Add(lblStatus);

            lblSub = new Label();
            lblSub.AutoSize = false;
            lblSub.Location = new Point(22, 41);
            lblSub.Size = new Size(900, 18);
            lblSub.Font = new Font("Segoe UI", 8.25f);
            lblSub.ForeColor = Muted;
            lblSub.BackColor = Color.Transparent;
            lblSub.AutoEllipsis = true;
            lblSub.Text = "Open this link on your tablet / smart display (same Wi-Fi)  ·  closing this window keeps it running in the tray";
            card.Controls.Add(lblSub);

            FlowLayoutPanel bar1 = MakeBar(64);
            card.Controls.Add(bar1);
            bar1.Controls.Add(MiniLabel("SERVER"));
            bar1.Controls.Add(MakeButton("Copy link", 94, delegate { CopyUrl(); }));
            bar1.Controls.Add(MakeButton("Open in browser", 122, delegate { OpenUrl(); }));
            bar1.Controls.Add(MakeButton("Allow firewall", 112, delegate { AllowFirewall(); }));
            btnToggle = MakeButton("Stop server", 104, delegate { ToggleServer(); });
            bar1.Controls.Add(btnToggle);

            Label lc = SmallLabel("Cols", new Padding(12, 7, 4, 0));
            bar1.Controls.Add(lc);

            numCols = MakeNumber(cfg.Cols, 2, 6);
            numCols.ValueChanged += delegate { if (!loadingFields) ApplyGridSize(); };
            bar1.Controls.Add(numCols);

            Label lr = SmallLabel("Rows", new Padding(10, 7, 4, 0));
            bar1.Controls.Add(lr);

            numRows = MakeNumber(cfg.Rows, 1, 6);
            numRows.ValueChanged += delegate { if (!loadingFields) ApplyGridSize(); };
            bar1.Controls.Add(numRows);

            FlowLayoutPanel bar2 = MakeBar(100);
            card.Controls.Add(bar2);
            bar2.Controls.Add(MiniLabel("APPEARANCE"));
            bar2.Controls.Add(MakeButton("Background image", 132, delegate { PickBackground(); }));

            Label lpos = SmallLabel("Position", new Padding(8, 7, 4, 0));
            bar2.Controls.Add(lpos);

            cmbBgPos = MakeCombo(110);
            cmbBgPos.Items.AddRange(new string[]
            {
                "center", "top", "bottom", "left", "right",
                "top left", "top right", "bottom left", "bottom right"
            });
            cmbBgPos.SelectedIndex = IndexOfPos(cfg.BackgroundPos);
            cmbBgPos.SelectedIndexChanged += delegate
            {
                if (loadingFields) return;
                cfg.BackgroundPos = cmbBgPos.Text;
                DeckConfig.Save(cfg, configPath);
                lblResult.Text = "Position: " + cmbBgPos.Text;
            };
            bar2.Controls.Add(cmbBgPos);

            bar2.Controls.Add(MakeButton("Remove", 78, delegate { ClearBackground(); }));

            lblBg = new Label();
            lblBg.AutoSize = false;
            lblBg.Width = 140;
            lblBg.Height = 22;
            lblBg.AutoEllipsis = true;
            lblBg.ForeColor = Muted;
            lblBg.BackColor = Color.Transparent;
            lblBg.TextAlign = ContentAlignment.MiddleLeft;
            lblBg.Margin = new Padding(9, 5, 0, 0);
            lblBg.Text = BackgroundLabel();
            bar2.Controls.Add(lblBg);

            Label lclock = SmallLabel("Clock", new Padding(10, 7, 4, 0));
            bar2.Controls.Add(lclock);

            cmbClock = MakeCombo(92);
            cmbClock.Items.AddRange(new string[] { "Bold", "Thin", "Minimal", "Hidden" });
            cmbClock.SelectedIndex = ClockStyleIndex(cfg.ClockStyle);
            cmbClock.SelectedIndexChanged += delegate
            {
                if (loadingFields) return;
                cfg.ClockStyle = ClockStyles[cmbClock.SelectedIndex];
                DeckConfig.Save(cfg, configPath);
                lblResult.Text = "Clock: " + cmbClock.Text;
            };
            bar2.Controls.Add(cmbClock);

            Label lfmt = SmallLabel("Format", new Padding(8, 7, 4, 0));
            bar2.Controls.Add(lfmt);

            cmbClockFmt = MakeCombo(78);
            cmbClockFmt.Items.AddRange(new string[] { "24-hour", "12-hour" });
            cmbClockFmt.SelectedIndex = (cfg.ClockFormat == "12") ? 1 : 0;
            cmbClockFmt.SelectedIndexChanged += delegate
            {
                if (loadingFields) return;
                cfg.ClockFormat = (cmbClockFmt.SelectedIndex == 1) ? "12" : "24";
                DeckConfig.Save(cfg, configPath);
                lblResult.Text = "Clock format: " + cmbClockFmt.Text;
            };
            bar2.Controls.Add(cmbClockFmt);

            FlowLayoutPanel bar3 = MakeBar(136);
            card.Controls.Add(bar3);
            bar3.Controls.Add(MiniLabel("CAST"));
            bar3.Controls.Add(SmallLabel("Display IP", new Padding(0, 7, 4, 0)));

            txtCastIp = MakeText();
            txtCastIp.Dock = DockStyle.None;
            txtCastIp.Width = 128;
            txtCastIp.Height = 22;
            txtCastIp.Margin = new Padding(0, 4, 8, 0);
            txtCastIp.Text = cfg.CastIp;
            txtCastIp.Leave += delegate { SaveCastIp(); };
            txtCastIp.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                SaveCastIp();
                DoCastNow();
            };
            bar3.Controls.Add(txtCastIp);

            bar3.Controls.Add(MakeButton("Cast now", 90, delegate { DoCastNow(); }));

            chkCastOn = new UiCheck();
            chkCastOn.Text = "Keep casting";
            chkCastOn.Width = 116;
            chkCastOn.Margin = new Padding(2, 1, 8, 0);
            chkCastOn.Checked = cfg.CastOn;
            chkCastOn.CheckedChanged += delegate { if (!loadingFields) SetCastEnabled(chkCastOn.Checked); };
            bar3.Controls.Add(chkCastOn);

            lblCast = new Label();
            lblCast.AutoSize = false;
            lblCast.Width = 220;
            lblCast.Height = 22;
            lblCast.AutoEllipsis = true;
            lblCast.ForeColor = Muted;
            lblCast.BackColor = Color.Transparent;
            lblCast.TextAlign = ContentAlignment.MiddleLeft;
            lblCast.Margin = new Padding(4, 5, 0, 0);
            lblCast.Text = "\u25CF Off";
            bar3.Controls.Add(lblCast);

            card.Resize += delegate
            {
                lblStatus.Location = new Point(Math.Max(20, card.Width - 160), 15);
                lblSub.Width = Math.Max(120, card.Width - 42);
                bar1.Width = Math.Max(120, card.Width - 36);
                bar2.Width = Math.Max(120, card.Width - 36);
                bar3.Width = Math.Max(120, card.Width - 36);
            };

            return host;
        }

        FlowLayoutPanel MakeBar(int y)
        {
            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Location = new Point(18, y);
            bar.Size = new Size(800, 30);
            bar.WrapContents = false;
            bar.AutoScroll = false;
            bar.BackColor = Color.Transparent;
            bar.Margin = new Padding(0);
            return bar;
        }

        static Label MiniLabel(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = false;
            l.Width = 80;
            l.Height = 22;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.ForeColor = Theme.AccentSoft;
            l.BackColor = Color.Transparent;
            l.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            l.Margin = new Padding(0, 6, 8, 0);
            return l;
        }

        static Label SmallLabel(string text, Padding margin)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = Muted;
            l.BackColor = Color.Transparent;
            l.Margin = margin;
            return l;
        }

        static NumericUpDown MakeNumber(int value, int min, int max)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.Value = value;
            n.Width = 46;
            n.Height = 22;
            n.Margin = new Padding(0, 4, 8, 0);
            n.BackColor = CardBg;
            n.ForeColor = Fg;
            n.BorderStyle = BorderStyle.FixedSingle;
            return n;
        }

        static DarkCombo MakeCombo(int width)
        {
            DarkCombo c = new DarkCombo();
            c.Width = width;
            c.Margin = new Padding(0, 4, 8, 0);
            return c;
        }

        static Button MakeButton(string text, int width, EventHandler onClick)
        {
            UiButton b = new UiButton();
            b.Text = text;
            b.Width = width;
            b.Height = 28;
            b.Margin = new Padding(0, 1, 8, 0);
            b.BorderColor = Line;
            b.BackColor = CardBg;
            b.ForeColor = Fg;
            if (onClick != null) b.Click += onClick;
            return b;
        }

        Panel BuildGridArea()
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Bg;
            host.Padding = new Padding(14, 0, 14, 6);
            host.Margin = new Padding(0);

            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.FillColor = Color.FromArgb(11, 11, 14);
            card.BorderColor = Line;
            card.Padding = new Padding(10);
            card.Margin = new Padding(0);
            host.Controls.Add(card);

            grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.BackColor = Color.FromArgb(11, 11, 14);
            grid.Margin = new Padding(0);
            card.Controls.Add(grid);
            return host;
        }

        Panel BuildEditor()
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Bg;
            host.Padding = new Padding(14, 8, 14, 14);
            host.Margin = new Padding(0);

            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.FillColor = PanelBg;
            card.BorderColor = Line;
            card.Padding = new Padding(16);
            card.Margin = new Padding(0);
            host.Controls.Add(card);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 4;
            t.RowCount = 6;
            t.BackColor = PanelBg;
            t.Margin = new Padding(0);
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            card.Controls.Add(t);

            lblSection = new Label();
            lblSection.AutoSize = false;
            lblSection.Dock = DockStyle.Fill;
            lblSection.TextAlign = ContentAlignment.MiddleLeft;
            lblSection.ForeColor = Theme.AccentSoft;
            lblSection.BackColor = Color.Transparent;
            lblSection.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            lblSection.Margin = new Padding(0);
            lblSection.Text = "SELECTED TILE";
            t.Controls.Add(lblSection, 0, 0);
            t.SetColumnSpan(lblSection, 2);

            lblResult = new Label();
            lblResult.AutoSize = false;
            lblResult.Dock = DockStyle.Fill;
            lblResult.TextAlign = ContentAlignment.MiddleRight;
            lblResult.ForeColor = Muted;
            lblResult.BackColor = Color.Transparent;
            lblResult.AutoEllipsis = true;
            lblResult.Margin = new Padding(0);
            lblResult.Text = "Ready";
            t.Controls.Add(lblResult, 2, 0);
            t.SetColumnSpan(lblResult, 2);

            t.Controls.Add(FieldLabel("Label"), 0, 1);
            txtLabel = MakeText();
            t.Controls.Add(txtLabel, 1, 1);
            t.Controls.Add(FieldLabel("Icon"), 2, 1);
            txtIcon = MakeText();
            txtIcon.AllowDrop = true;
            txtIcon.DragEnter += delegate(object s, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            txtIcon.DragDrop += delegate(object s, DragEventArgs e)
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0) SetIconFromFile(files[0]);
            };
            t.Controls.Add(txtIcon, 3, 1);

            t.Controls.Add(FieldLabel("Action"), 0, 2);
            cmbAction = new DarkCombo();
            cmbAction.Dock = DockStyle.Fill;
            cmbAction.Margin = new Padding(0, 6, 8, 6);
            cmbAction.Items.AddRange(actionNames);
            cmbAction.SelectedIndex = 0;
            cmbAction.SelectedIndexChanged += delegate { if (!loadingFields) UpdateHint(); };
            t.Controls.Add(cmbAction, 1, 2);

            t.Controls.Add(FieldLabel("Color"), 2, 2);
            btnColor = MakeButton("#39FF14", 110, delegate { PickColor(); });
            btnColor.Dock = DockStyle.Fill;
            btnColor.Margin = new Padding(0, 6, 8, 6);
            t.Controls.Add(btnColor, 3, 2);

            t.Controls.Add(FieldLabel("Target"), 0, 3);
            txtTarget = MakeText();
            txtTarget.Margin = new Padding(0, 6, 8, 6);
            t.Controls.Add(txtTarget, 1, 3);
            t.SetColumnSpan(txtTarget, 3);

            lblHint = new Label();
            lblHint.AutoSize = false;
            lblHint.Dock = DockStyle.Fill;
            lblHint.ForeColor = Muted;
            lblHint.BackColor = Color.Transparent;
            lblHint.TextAlign = ContentAlignment.MiddleLeft;
            lblHint.AutoEllipsis = true;
            lblHint.Margin = new Padding(0);
            t.Controls.Add(lblHint, 0, 4);
            t.SetColumnSpan(lblHint, 4);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.WrapContents = false;
            actions.AutoScroll = false;
            actions.BackColor = Color.Transparent;
            actions.Margin = new Padding(0, 6, 0, 0);
            t.Controls.Add(actions, 0, 5);
            t.SetColumnSpan(actions, 4);
            actions.Controls.Add(MakeButton("Test action", 102, delegate { TestAction(); }));
            actions.Controls.Add(MakeButton("Save button", 106, delegate { SaveCurrent(); }));
            actions.Controls.Add(MakeButton("Pick icon image", 124, delegate { PickIconImage(); }));

            lblNet = new Label();
            lblNet.AutoSize = false;
            lblNet.Width = 380;
            lblNet.Height = 26;
            lblNet.ForeColor = Theme.Dim;
            lblNet.BackColor = Color.Transparent;
            lblNet.TextAlign = ContentAlignment.MiddleLeft;
            lblNet.AutoEllipsis = true;
            lblNet.Margin = new Padding(12, 2, 0, 0);
            lblNet.Text = "Changes go live automatically  ·  no refresh needed on the display";
            actions.Controls.Add(lblNet);

            return host;
        }

        static Label FieldLabel(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.Dock = DockStyle.Fill;
            l.ForeColor = Muted;
            l.BackColor = Color.Transparent;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Margin = new Padding(0);
            return l;
        }

        static TextBox MakeText()
        {
            TextBox t = new TextBox();
            t.Dock = DockStyle.Fill;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = CardBg;
            t.ForeColor = Fg;
            t.Margin = new Padding(0, 6, 8, 6);
            return t;
        }

        // ---------- tray / background ----------

        void SetupTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.BackColor = Color.FromArgb(18, 18, 23);
            trayMenu.ForeColor = Fg;
            trayMenu.Font = new Font("Segoe UI", 9.5f);
            trayMenu.ShowImageMargin = false;
            trayMenu.Padding = new Padding(4);
            trayMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            trayMenu.RenderMode = ToolStripRenderMode.Professional;

            ToolStripMenuItem open = new ToolStripMenuItem("Open editor");
            open.Font = new Font(trayMenu.Font, FontStyle.Bold);
            open.Click += delegate { ShowEditor(); };
            trayMenu.Items.Add(open);

            ToolStripMenuItem openPage = new ToolStripMenuItem("Open deck page");
            openPage.Click += delegate { OpenUrl(); };
            trayMenu.Items.Add(openPage);

            ToolStripMenuItem copy = new ToolStripMenuItem("Copy link");
            copy.Click += delegate { CopyUrl(); };
            trayMenu.Items.Add(copy);

            trayMenu.Items.Add(new ToolStripSeparator());

            miServer = new ToolStripMenuItem("Stop server");
            miServer.Click += delegate { ToggleServer(); };
            trayMenu.Items.Add(miServer);

            miCastOn = new ToolStripMenuItem("Keep casting to display");
            miCastOn.Click += delegate { SetCastEnabled(!cfg.CastOn); };
            trayMenu.Items.Add(miCastOn);

            miCastNow = new ToolStripMenuItem("Cast now");
            miCastNow.Click += delegate { DoCastNow(); };
            trayMenu.Items.Add(miCastNow);

            miCastStatus = new ToolStripMenuItem("Cast: Off");
            miCastStatus.Enabled = false;
            trayMenu.Items.Add(miCastStatus);

            trayMenu.Items.Add(new ToolStripSeparator());

            miStartWin = new ToolStripMenuItem("Start with Windows");
            miStartWin.Click += delegate { SetStartWithWindows(!IsStartWithWindows()); };
            trayMenu.Items.Add(miStartWin);

            miStartTray = new ToolStripMenuItem("Start in background");
            miStartTray.Click += delegate
            {
                cfg.StartInTray = !cfg.StartInTray;
                DeckConfig.Save(cfg, configPath);
                RefreshTrayState();
            };
            trayMenu.Items.Add(miStartTray);

            ToolStripMenuItem shortcut = new ToolStripMenuItem("Create desktop shortcut");
            shortcut.Click += delegate { CreateDesktopShortcut(false); };
            trayMenu.Items.Add(shortcut);

            trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate
            {
                exiting = true;
                Close();
            };
            trayMenu.Items.Add(exit);

            trayMenu.Opening += delegate { RefreshTrayState(); };

            tray = new NotifyIcon();
            tray.Icon = appIcon;
            tray.Text = "Stream Deck PC";
            tray.Visible = true;
            tray.ContextMenuStrip = trayMenu;
            tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ShowEditor();
            };
            tray.BalloonTipClicked += delegate { ShowEditor(); };
        }

        void RefreshTrayState()
        {
            if (miServer != null)
                miServer.Text = (server != null && server.Running) ? "Stop server" : "Start server";
            if (miCastOn != null) miCastOn.Checked = cfg.CastOn;
            if (miStartWin != null) miStartWin.Checked = IsStartWithWindows();
            if (miStartTray != null) miStartTray.Checked = cfg.StartInTray;
            if (miCastStatus != null)
                miCastStatus.Text = "Cast: " + (caster != null ? caster.LastStatus : "Off");
        }

        void UpdateCastStatus()
        {
            if (caster == null) return;
            string st = caster.LastStatus ?? "Off";

            if (lblCast != null)
            {
                string text = "\u25CF " + st;
                if (lblCast.Text != text) lblCast.Text = text;
                Color color = Muted;
                if (st.IndexOf("Casting", StringComparison.OrdinalIgnoreCase) >= 0) color = Accent;
                else if (st.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         st.IndexOf("unreach", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         st.IndexOf("Timeout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         st.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0) color = Theme.Danger;
                if (lblCast.ForeColor != color) lblCast.ForeColor = color;
            }

            if (miCastStatus != null)
            {
                string mt = "Cast: " + st;
                if (miCastStatus.Text != mt) miCastStatus.Text = mt;
            }
            if (miCastOn != null && miCastOn.Checked != cfg.CastOn) miCastOn.Checked = cfg.CastOn;

            if (tray != null)
            {
                string tip = "Stream Deck PC";
                if (server != null && server.Running) tip += "  ·  Online";
                if (cfg.CastOn) tip += "  ·  Cast " + st;
                if (tip.Length > 63) tip = tip.Substring(0, 63);
                if (tray.Text != tip) tray.Text = tip;
            }
        }

        void ShowTrayTip()
        {
            if (tipShown || tray == null) return;
            tipShown = true;
            try
            {
                tray.ShowBalloonTip(3000, "Stream Deck PC",
                    "Running in the background. Click the tray icon to open the editor.", ToolTipIcon.Info);
            }
            catch { }
        }

        void HideToTray()
        {
            SaveCurrent();
            Hide();
            ShowTrayTip();
        }

        public void ShowEditor()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(ShowEditor));
                return;
            }
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show();
            Activate();
            BringToFront();
            RefreshUrl();
            RefreshTrayState();
        }

        void WatchForShow()
        {
            if (showEvent == null) return;
            Thread th = new Thread(delegate()
            {
                while (true)
                {
                    try
                    {
                        if (!showEvent.WaitOne()) break;
                    }
                    catch { break; }
                    try
                    {
                        if (IsDisposed) break;
                        BeginInvoke(new MethodInvoker(ShowEditor));
                    }
                    catch { break; }
                }
            });
            th.IsBackground = true;
            th.Start();
        }

        void SetCastEnabled(bool on)
        {
            if (syncingCast) return;
            syncingCast = true;
            cfg.CastOn = on;
            if (chkCastOn != null) chkCastOn.Checked = on;
            DeckConfig.Save(cfg, configPath);
            if (caster != null)
            {
                caster.DeviceIp = cfg.CastIp;
                caster.Url = (server != null && server.Running) ? CurrentUrl() : "";
                caster.Enabled = on && server != null && server.Running;
                if (on) caster.Wake();
            }
            syncingCast = false;
            UpdateCastStatus();
            RefreshTrayState();
        }

        void SaveCastIp()
        {
            if (txtCastIp == null) return;
            string ip = txtCastIp.Text.Trim();
            if (ip == cfg.CastIp) return;
            cfg.CastIp = ip;
            DeckConfig.Save(cfg, configPath);
            if (caster != null)
            {
                caster.DeviceIp = ip;
                caster.Wake();
            }
            lblResult.Text = "Display IP saved";
        }

        void DoCastNow()
        {
            SaveCastIp();
            if (!cfg.CastOn) SetCastEnabled(true);
            if (caster != null)
            {
                caster.DeviceIp = cfg.CastIp;
                caster.Url = (server != null && server.Running) ? CurrentUrl() : "";
                caster.CastNow();
            }
            UpdateCastStatus();
        }

        bool IsStartWithWindows()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    return k.GetValue(RunName) != null;
                }
            }
            catch { return false; }
        }

        void SetStartWithWindows(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return;
                    if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --tray");
                    else k.DeleteValue(RunName, false);
                }
                lblResult.Text = on ? "Will start with Windows" : "Start with Windows off";
                RefreshTrayState();
            }
            catch (Exception ex)
            {
                lblResult.Text = "Registry: " + ex.Message;
            }
        }

        void CreateDesktopShortcut(bool quiet)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnk = Path.Combine(desktop, "Stream Deck PC.lnk");
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) throw new Exception("WScript.Shell unavailable");
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type st = shortcut.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { Application.ExecutablePath });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { AppDomain.CurrentDomain.BaseDirectory });
                st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { Application.ExecutablePath + ",0" });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Stream Deck PC control center" });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                if (!quiet && lblResult != null) lblResult.Text = "Desktop shortcut created";
            }
            catch (Exception ex)
            {
                if (!quiet && lblResult != null) lblResult.Text = "Shortcut: " + ex.Message;
            }
            finally
            {
                if (shortcut != null) { try { Marshal.ReleaseComObject(shortcut); } catch { } }
                if (shell != null) { try { Marshal.ReleaseComObject(shell); } catch { } }
            }
        }

        static Icon LoadAppIcon()
        {
            try
            {
                Icon i = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (i != null) return i;
            }
            catch { }
            return SystemIcons.Application;
        }

        // ---------- config / ui helpers ----------

        static readonly string[] ClockStyles = { "bold", "thin", "minimal", "hidden" };

        static int ClockStyleIndex(string s)
        {
            int i = Array.IndexOf(ClockStyles, (s ?? "").Trim().ToLowerInvariant());
            return i < 0 ? 0 : i;
        }

        static int IndexOfPos(string p)
        {
            string[] all = { "center", "top", "bottom", "left", "right", "top left", "top right", "bottom left", "bottom right" };
            int i = Array.IndexOf(all, (p ?? "").Trim().ToLowerInvariant());
            return i < 0 ? 0 : i;
        }

        string BackgroundLabel()
        {
            if (string.IsNullOrEmpty(cfg.Background)) return "Background: none (black)";
            string n = cfg.Background.StartsWith("img:") ? cfg.Background.Substring(4) : cfg.Background;
            return "Background: " + n;
        }

        static Color SafeColor(string hex, Color fallback)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return fallback;
                return ColorTranslator.FromHtml(hex);
            }
            catch
            {
                return fallback;
            }
        }

        static string ToHex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        int ActionIndex()
        {
            int i = Array.IndexOf(actionKeys, (cfg.Buttons.Count > selected && selected >= 0)
                ? cfg.Buttons[selected].Action : "app");
            if (i < 0) i = 0;
            return i;
        }

        // ---------- grid ----------

        void RebuildGrid()
        {
            grid.SuspendLayout();
            Control[] old = new Control[grid.Controls.Count];
            grid.Controls.CopyTo(old, 0);
            foreach (Control c in old) c.Dispose();
            grid.Controls.Clear();
            grid.ColumnStyles.Clear();
            grid.RowStyles.Clear();
            grid.ColumnCount = cfg.Cols;
            grid.RowCount = cfg.Rows;
            for (int i = 0; i < cfg.Cols; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cfg.Cols));
            for (int i = 0; i < cfg.Rows; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / cfg.Rows));

            cells = new Button[cfg.Buttons.Count];
            for (int i = 0; i < cfg.Buttons.Count; i++)
            {
                Button b = new Button();
                b.Dock = DockStyle.Fill;
                b.Margin = new Padding(6);
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = Line;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(26, 26, 33);
                b.BackColor = CardBg;
                b.ForeColor = Fg;
                b.Font = new Font("Segoe UI Emoji", 10f);
                b.TextAlign = ContentAlignment.MiddleCenter;
                b.UseVisualStyleBackColor = false;
                b.Cursor = Cursors.Hand;
                b.Tag = i;
                int idx = i;
                b.Click += delegate
                {
                    SaveCurrent();
                    SelectCell(idx);
                };
                b.MouseDown += delegate(object s2, MouseEventArgs e2)
                {
                    if (e2.Button == MouseButtons.Left) dragFrom = idx;
                };
                b.MouseMove += delegate(object s2, MouseEventArgs e2)
                {
                    if (e2.Button == MouseButtons.Left && dragFrom == idx && !dragging)
                    {
                        dragging = true;
                        try { b.DoDragDrop(idx.ToString(), DragDropEffects.Move); }
                        finally { dragging = false; dragFrom = -1; }
                    }
                };
                b.AllowDrop = true;
                b.DragEnter += delegate(object s2, DragEventArgs e2)
                {
                    if (e2.Data.GetDataPresent(DataFormats.Text)) e2.Effect = DragDropEffects.Move;
                };
                b.DragOver += delegate(object s2, DragEventArgs e2)
                {
                    if (e2.Data.GetDataPresent(DataFormats.Text)) e2.Effect = DragDropEffects.Move;
                };
                b.DragDrop += delegate(object s2, DragEventArgs e2)
                {
                    object d = e2.Data.GetData(DataFormats.Text);
                    int from;
                    if (d != null && int.TryParse(d.ToString(), out from)) MoveButton(from, idx);
                };
                cells[i] = b;
                grid.Controls.Add(b, i % cfg.Cols, i / cfg.Cols);
            }
            grid.ResumeLayout();

            if (selected >= cfg.Buttons.Count) selected = cfg.Buttons.Count - 1;
            if (selected < 0) selected = 0;
            RefreshCells();
            SelectCell(selected);
        }

        void RefreshCells()
        {
            for (int i = 0; i < cells.Length && i < cfg.Buttons.Count; i++)
            {
                DeckButton b = cfg.Buttons[i];
                Button c = cells[i];
                if (c == null) continue;
                string icon = b.Icon ?? "";
                if (c.Image != null) { Image old = c.Image; c.Image = null; old.Dispose(); }
                if (icon.StartsWith("img:"))
                {
                    string file = Path.Combine(iconsDir, icon.Substring(4));
                    if (File.Exists(file))
                    {
                        try
                        {
                            using (Image tmp = Image.FromFile(file))
                            {
                                c.Image = new Bitmap(tmp, 44, 44);
                            }
                            c.Text = b.Label;
                            c.TextImageRelation = TextImageRelation.ImageAboveText;
                            c.ImageAlign = ContentAlignment.MiddleCenter;
                        }
                        catch
                        {
                            c.Text = b.Label;
                        }
                    }
                    else
                    {
                        c.Text = b.Label;
                    }
                }
                else
                {
                    c.Text = icon + "\n" + b.Label;
                    c.TextImageRelation = TextImageRelation.ImageBeforeText;
                }
                c.ForeColor = SafeColor(b.Color, Accent);
                bool sel = (i == selected);
                c.FlatAppearance.BorderSize = sel ? 2 : 1;
                c.FlatAppearance.BorderColor = sel ? Accent : Line;
                c.BackColor = sel ? Color.FromArgb(19, 27, 17) : CardBg;
            }
        }

        void SelectCell(int i)
        {
            if (cfg.Buttons.Count == 0) return;
            if (i < 0) i = 0;
            if (i >= cfg.Buttons.Count) i = cfg.Buttons.Count - 1;
            selected = i;
            DeckButton b = cfg.Buttons[i];
            loadingFields = true;
            txtLabel.Text = b.Label;
            txtIcon.Text = b.Icon;
            int ai = Array.IndexOf(actionKeys, b.Action);
            cmbAction.SelectedIndex = ai >= 0 ? ai : 0;
            txtTarget.Text = b.Target;
            btnColor.Text = b.Color;
            btnColor.ForeColor = SafeColor(b.Color, Accent);
            lblSection.Text = "TILE " + (i + 1);
            UpdateHint();
            loadingFields = false;
            lblResult.Text = "Tile " + (i + 1);
            RefreshCells();
        }

        void UpdateHint()
        {
            int i = cmbAction.SelectedIndex;
            string key = (i >= 0 && i < actionKeys.Length) ? actionKeys[i] : "app";
            switch (key)
            {
                case "app":
                    lblHint.Text = "Examples:  chrome.exe   |   C:\\Windows\\notepad.exe   |   ms-settings:   |   C:\\folder";
                    break;
                case "url":
                    lblHint.Text = "Examples:  https://youtube.com   |   https://chat.openai.com";
                    break;
                case "cmd":
                    lblHint.Text = "Examples:  explorer C:\\Users\\pc\\Desktop   |   start spotify";
                    break;
                case "powershell":
                    lblHint.Text = "Runs hidden.  Example:  Stop-Process -Name notepad   |   (New-Object -ComObject WScript.Shell).SendKeys([char]175)";
                    break;
                case "media":
                    lblHint.Text = "Keys:  play  |  next  |  prev  |  stop  |  mute  |  volup  |  voldown";
                    break;
                case "key":
                    lblHint.Text = "Combos:  win+shift+s   |   ctrl+shift+esc   |   alt+f4   |   win+d   |   f5";
                    break;
                case "text":
                    lblHint.Text = "Any text - it will be typed into the window that is currently focused.";
                    break;
                default:
                    lblHint.Text = "No target needed for this action.";
                    break;
            }
        }

        // ---------- files ----------

        void PickBackground()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Pick a background image (high resolution is fine)";
                d.Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp|All files|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Directory.CreateDirectory(backgroundsDir);
                    string ext = Path.GetExtension(d.FileName).ToLowerInvariant();
                    if (ext.Length == 0) ext = ".jpg";
                    string name = "bg_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ext;
                    File.Copy(d.FileName, Path.Combine(backgroundsDir, name), true);
                    cfg.Background = "img:" + name;
                    DeckConfig.Save(cfg, configPath);
                    lblBg.Text = BackgroundLabel();
                    lblResult.Text = "Background set";
                }
                catch (Exception ex)
                {
                    lblResult.Text = "Error: " + ex.Message;
                }
            }
        }

        void ClearBackground()
        {
            cfg.Background = "";
            DeckConfig.Save(cfg, configPath);
            lblBg.Text = BackgroundLabel();
            lblResult.Text = "Background removed";
        }

        void PickIconImage()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Pick an icon image";
                d.Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.ico|All files|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                SetIconFromFile(d.FileName);
            }
        }

        static Image LoadAny(string path)
        {
            if (Path.GetExtension(path).ToLowerInvariant() == ".ico")
            {
                using (Icon ico = new Icon(path, 256, 256)) return ico.ToBitmap();
            }
            return Image.FromFile(path);
        }

        static bool MakePng(string srcPath, string destPath, int size)
        {
            try
            {
                using (Image src = LoadAny(srcPath))
                {
                    using (Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.Clear(Color.Transparent);
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            float s = Math.Min((float)size / src.Width, (float)size / src.Height);
                            int w = Math.Max(1, (int)(src.Width * s));
                            int h = Math.Max(1, (int)(src.Height * s));
                            g.DrawImage(src, (size - w) / 2, (size - h) / 2, w, h);
                        }
                        bmp.Save(destPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                return true;
            }
            catch { return false; }
        }

        void MigrateIcons()
        {
            bool changed = false;
            foreach (DeckButton b in cfg.Buttons)
            {
                if (b.Icon == null || !b.Icon.StartsWith("img:")) continue;
                string name = b.Icon.Substring(4);
                if (!name.ToLowerInvariant().EndsWith(".ico")) continue;
                string src = Path.Combine(iconsDir, name);
                if (!File.Exists(src)) continue;
                string png = Path.ChangeExtension(name, ".png");
                if (MakePng(src, Path.Combine(iconsDir, png), 256))
                {
                    b.Icon = "img:" + png;
                    changed = true;
                }
            }
            if (changed) DeckConfig.Save(cfg, configPath);
        }

        void SetIconFromFile(string path)
        {
            try
            {
                Directory.CreateDirectory(iconsDir);
                string name = "icon_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png";
                string dest = Path.Combine(iconsDir, name);
                if (!MakePng(path, dest, 256)) throw new Exception("Unsupported image file");
                txtIcon.Text = "img:" + name;
                lblResult.Text = "Image icon ready - press Save button";
            }
            catch (Exception ex)
            {
                lblResult.Text = "Error: " + ex.Message;
            }
        }

        void MoveButton(int from, int to)
        {
            if (from == to) return;
            if (from < 0 || to < 0 || from >= cfg.Buttons.Count || to >= cfg.Buttons.Count) return;
            DeckButton tmp = cfg.Buttons[from];
            cfg.Buttons[from] = cfg.Buttons[to];
            cfg.Buttons[to] = tmp;
            DeckConfig.Save(cfg, configPath);
            selected = to;
            RebuildGrid();
            lblResult.Text = "Moved to tile " + (to + 1);
        }

        void PickColor()
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.FullOpen = true;
                cd.Color = SafeColor(btnColor.Text, Accent);
                if (cd.ShowDialog(this) == DialogResult.OK)
                {
                    btnColor.Text = ToHex(cd.Color);
                    btnColor.ForeColor = cd.Color;
                }
            }
        }

        // ---------- save / actions ----------

        DeckButton FromFields()
        {
            DeckButton b = new DeckButton();
            b.Label = txtLabel.Text.Trim().Length > 0 ? txtLabel.Text.Trim() : "Button";
            b.Icon = txtIcon.Text.Trim().Length > 0 ? txtIcon.Text.Trim() : "\u2022";
            b.Color = btnColor.Text.Trim();
            int i = cmbAction.SelectedIndex;
            b.Action = (i >= 0 && i < actionKeys.Length) ? actionKeys[i] : "app";
            b.Target = txtTarget.Text.Trim();
            return b;
        }

        void TestAction()
        {
            DeckButton b = FromFields();
            lblResult.Text = "Running...";
            Application.DoEvents();
            string msg = Runner.Run(b);
            lblResult.Text = msg;
        }

        void SaveCurrent()
        {
            if (selected < 0 || selected >= cfg.Buttons.Count) return;
            DeckButton b = cfg.Buttons[selected];
            DeckButton n = FromFields();
            b.Label = n.Label;
            b.Icon = n.Icon;
            b.Color = n.Color;
            b.Action = n.Action;
            b.Target = n.Target;
            DeckConfig.Save(cfg, configPath);
            RefreshCells();
            lblResult.Text = "Saved";
        }

        void ApplyGridSize()
        {
            cfg.Cols = (int)numCols.Value;
            cfg.Rows = (int)numRows.Value;
            cfg.Normalize();
            DeckConfig.Save(cfg, configPath);
            RebuildGrid();
        }

        // ---------- network / server ----------

        static List<string> ScanLocalIps()
        {
            List<string> wifi = new List<string>();
            List<string> eth = new List<string>();
            List<string> other = new List<string>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ip = ua.Address.ToString();
                        if (ip.StartsWith("169.254")) continue;
                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) wifi.Add(ip);
                        else if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) eth.Add(ip);
                        else other.Add(ip);
                    }
                }
            }
            catch { }
            List<string> all = new List<string>();
            all.AddRange(wifi);
            all.AddRange(eth);
            all.AddRange(other);
            if (all.Count == 0) all.Add("127.0.0.1");
            return all;
        }

        List<string> GetLocalIps()
        {
            if (ipCache != null && (DateTime.UtcNow - ipCacheAt).TotalSeconds < 30) return ipCache;
            ipCache = ScanLocalIps();
            ipCacheAt = DateTime.UtcNow;
            return ipCache;
        }

        string CurrentUrl()
        {
            int port = server != null ? server.Port : 8090;
            return "http://" + GetLocalIps()[0] + ":" + port + "/";
        }

        void RefreshUrl()
        {
            if (server == null || !server.Running)
            {
                if (lblUrl.Text != "Server stopped") lblUrl.Text = "Server stopped";
                if (lblStatus.Text != "\u25CF Offline") lblStatus.Text = "\u25CF Offline";
                lblStatus.ForeColor = Theme.Dim;
                if (btnToggle != null) btnToggle.Text = "Start server";
                if (caster != null) caster.Enabled = false;
                return;
            }

            List<string> ips = GetLocalIps();
            int port = server.Port;
            string url = "http://" + ips[0] + ":" + port + "/";
            if (lblUrl.Text != url) lblUrl.Text = url;

            string alt = "";
            for (int i = 1; i < ips.Count; i++) alt += "   |   http://" + ips[i] + ":" + port + "/";
            string sub = "Open this link on your tablet / smart display (same Wi-Fi)" + alt +
                         "  ·  closing this window keeps it running in the tray";
            if (lblSub.Text != sub) lblSub.Text = sub;

            if (lblStatus.Text != "\u25CF Online") lblStatus.Text = "\u25CF Online";
            lblStatus.ForeColor = Accent;

            if (caster != null)
            {
                caster.Url = url;
                caster.Enabled = cfg.CastOn;
            }
        }

        void StartServer()
        {
            if (server != null && server.Running) return;
            server = new DeckServer(8090, cfg);
            if (server.Start())
            {
                btnToggle.Text = "Stop server";
                RefreshUrl();
                if (caster != null && cfg.CastOn) caster.Wake();
            }
            else
            {
                lblUrl.Text = "Could not start server";
                lblSub.Text = "Port busy? " + server.LastError;
                lblStatus.Text = "\u25CF Offline";
                lblStatus.ForeColor = Theme.Danger;
                btnToggle.Text = "Start server";
            }
        }

        void ToggleServer()
        {
            if (server != null && server.Running)
            {
                server.Stop();
                if (caster != null) caster.Enabled = false;
                lblUrl.Text = "Server stopped";
                lblStatus.Text = "\u25CF Offline";
                lblStatus.ForeColor = Theme.Dim;
                btnToggle.Text = "Start server";
            }
            else
            {
                StartServer();
            }
            UpdateCastStatus();
            RefreshTrayState();
        }

        void CopyUrl()
        {
            try
            {
                string url = CurrentUrl();
                Clipboard.SetText(url);
                lblSub.Text = "Link copied: " + url;
            }
            catch { }
        }

        void OpenUrl()
        {
            try { Process.Start(new ProcessStartInfo(CurrentUrl()) { UseShellExecute = true }); }
            catch (Exception ex) { lblSub.Text = "Error: " + ex.Message; }
        }

        void AllowFirewall()
        {
            string args = "advfirewall firewall add rule name=\"Stream Deck PC\" dir=in action=allow program=\"" +
                          Application.ExecutablePath + "\" enable=yes profile=any";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("netsh", args);
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                lblSub.Text = "Firewall rule added (or cancelled).";
            }
            catch
            {
                lblSub.Text = "Firewall: run the app as administrator, or allow it in the Windows prompt.";
            }
        }

        // ---------- window chrome ----------

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;
                return cp;
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            if (startHidden && !IsHandleCreated)
            {
                CreateHandle();
                base.SetVisibleCore(false);
                return;
            }
            base.SetVisibleCore(value);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath path = Theme.Round(new Rectangle(0, 0, Width, Height), 14))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), 14))
            {
                using (Pen p = new Pen(Color.FromArgb(48, 48, 60)))
                    e.Graphics.DrawPath(p, path);
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            base.WndProc(ref m);

            if (m.Msg != WM_NCHITTEST || (int)m.Result != 1 || WindowState != FormWindowState.Normal) return;

            Point p = PointToClient(Cursor.Position);
            int b = 6;
            bool left = p.X <= b;
            bool right = p.X >= ClientSize.Width - b;
            bool top = p.Y <= b;
            bool bottom = p.Y >= ClientSize.Height - b;

            if (top && left) m.Result = (IntPtr)13;
            else if (top && right) m.Result = (IntPtr)14;
            else if (bottom && left) m.Result = (IntPtr)16;
            else if (bottom && right) m.Result = (IntPtr)17;
            else if (left) m.Result = (IntPtr)10;
            else if (right) m.Result = (IntPtr)11;
            else if (top) m.Result = (IntPtr)12;
            else if (bottom) m.Result = (IntPtr)15;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            SaveCurrent();
            if (netTimer != null) netTimer.Stop();
            if (uiTimer != null) uiTimer.Stop();
            if (caster != null) caster.Stop();
            if (server != null) server.Stop();
            if (tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
                tray = null;
            }
            base.OnFormClosing(e);
        }
    }
}
