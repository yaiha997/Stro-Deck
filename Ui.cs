using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StreamDeckPC
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(9, 9, 11);
        public static readonly Color PanelBg = Color.FromArgb(15, 15, 19);
        public static readonly Color CardBg = Color.FromArgb(18, 18, 23);
        public static readonly Color CardBg2 = Color.FromArgb(22, 22, 28);
        public static readonly Color Fg = Color.FromArgb(234, 234, 240);
        public static readonly Color Muted = Color.FromArgb(137, 137, 151);
        public static readonly Color Dim = Color.FromArgb(102, 102, 116);
        public static readonly Color Accent = Color.FromArgb(57, 255, 20);
        public static readonly Color AccentSoft = Color.FromArgb(150, 196, 140);
        public static readonly Color Line = Color.FromArgb(42, 42, 52);
        public static readonly Color Hover = Color.FromArgb(32, 32, 40);
        public static readonly Color Danger = Color.FromArgb(255, 106, 106);

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d < 2)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    internal class CardPanel : Panel
    {
        public Color FillColor = Theme.PanelBg;
        public Color BorderColor = Theme.Line;
        public int Radius = 13;

        public CardPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = FillColor;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath path = Theme.Round(new Rectangle(0, 0, Width, Height), Radius))
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
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Round(r, Radius))
            {
                using (Pen p = new Pen(BorderColor)) e.Graphics.DrawPath(p, path);
            }
        }
    }

    internal class UiButton : Button
    {
        public Color HoverColor = Theme.Hover;
        public Color BorderColor = Theme.Line;
        public int Radius = 8;
        public bool DrawBorder = true;
        bool hover;
        bool down;

        public UiButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Theme.CardBg;
            ForeColor = Theme.Fg;
            Font = new Font("Segoe UI", 9f);
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            down = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            down = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            down = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color parentColor = Theme.PanelBg;
            if (Parent != null && Parent.BackColor.A > 0 && Parent.BackColor != Color.Transparent)
                parentColor = Parent.BackColor;
            using (SolidBrush pb = new SolidBrush(parentColor)) g.FillRectangle(pb, ClientRectangle);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = BackColor;
            if (!Enabled) fill = Theme.CardBg;
            else if (down) fill = Color.FromArgb(48, 48, 58);
            else if (hover) fill = HoverColor;

            using (GraphicsPath path = Theme.Round(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                if (DrawBorder)
                {
                    using (Pen p = new Pen(Enabled ? BorderColor : Theme.Line)) g.DrawPath(p, path);
                }
            }

            Color text = Enabled ? ForeColor : Theme.Dim;
            Rectangle tr = new Rectangle(4, 0, Width - 8, Height);
            TextRenderer.DrawText(g, Text, Font, tr, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal class UiCheck : CheckBox
    {
        bool hover;

        public UiCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            ForeColor = Theme.Fg;
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);
            Cursor = Cursors.Hand;
            AutoSize = false;
            Height = 24;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int box = 16;
            Rectangle r = new Rectangle(1, (Height - box) / 2, box, box);

            Color parentColor = Theme.PanelBg;
            if (Parent != null && Parent.BackColor.A > 0 && Parent.BackColor != Color.Transparent)
                parentColor = Parent.BackColor;
            using (SolidBrush pb = new SolidBrush(parentColor)) g.FillRectangle(pb, ClientRectangle);

            using (GraphicsPath path = Theme.Round(r, 4))
            {
                using (SolidBrush b = new SolidBrush(Checked ? Theme.Accent : (hover ? Theme.Hover : Theme.CardBg)))
                    g.FillPath(b, path);
                using (Pen p = new Pen(Checked ? Theme.Accent : Theme.Line)) g.DrawPath(p, path);
            }

            if (Checked)
            {
                using (Pen p = new Pen(Color.FromArgb(10, 20, 8), 2f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLines(p, new Point[]
                    {
                        new Point(r.X + 4, r.Y + 8),
                        new Point(r.X + 7, r.Y + 11),
                        new Point(r.X + 12, r.Y + 5)
                    });
                }
            }

            Rectangle tr = new Rectangle(box + 7, 0, Width - box - 8, Height);
            TextRenderer.DrawText(g, Text, Font, tr, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal class DarkCombo : ComboBox
    {
        public DarkCombo()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            BackColor = Theme.CardBg;
            ForeColor = Theme.Fg;
            ItemHeight = 22;
            Font = new Font("Segoe UI", 9f);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color bg = sel ? Color.FromArgb(34, 44, 32) : Theme.CardBg;
            using (SolidBrush b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, e.Bounds);
            Rectangle tr = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, Convert.ToString(Items[e.Index]), e.Font, tr,
                sel ? Theme.Fg : Color.FromArgb(208, 208, 218),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal class DarkMenuColors : ProfessionalColorTable
    {
        static readonly Color MenuBg = Color.FromArgb(18, 18, 23);
        static readonly Color Sel = Color.FromArgb(35, 35, 44);

        public override Color ToolStripDropDownBackground { get { return MenuBg; } }
        public override Color ImageMarginGradientBegin { get { return MenuBg; } }
        public override Color ImageMarginGradientMiddle { get { return MenuBg; } }
        public override Color ImageMarginGradientEnd { get { return MenuBg; } }
        public override Color MenuBorder { get { return Color.FromArgb(52, 52, 64); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(44, 44, 56); } }
        public override Color MenuItemSelected { get { return Sel; } }
        public override Color MenuItemSelectedGradientBegin { get { return Sel; } }
        public override Color MenuItemSelectedGradientEnd { get { return Sel; } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(42, 42, 52); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(42, 42, 52); } }
        public override Color ButtonSelectedHighlight { get { return Sel; } }
        public override Color ButtonSelectedGradientBegin { get { return Sel; } }
        public override Color ButtonSelectedGradientEnd { get { return Sel; } }
        public override Color ButtonPressedGradientBegin { get { return Color.FromArgb(42, 42, 52); } }
        public override Color ButtonPressedGradientEnd { get { return Color.FromArgb(42, 42, 52); } }
        public override Color SeparatorDark { get { return Color.FromArgb(40, 40, 50); } }
        public override Color SeparatorLight { get { return Color.FromArgb(55, 55, 66); } }
        public override Color CheckBackground { get { return Sel; } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(44, 58, 40); } }
        public override Color CheckPressedBackground { get { return Color.FromArgb(44, 58, 40); } }
        public override Color GripDark { get { return Color.FromArgb(45, 45, 55); } }
        public override Color GripLight { get { return Color.FromArgb(60, 60, 72); } }
    }
}
