using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using NeleDesktop.Models;
using NeleDesktop.Services;
using WpfApplication = System.Windows.Application;
using WpfExitEventArgs = System.Windows.ExitEventArgs;
using WpfStartupEventArgs = System.Windows.StartupEventArgs;

namespace NeleDesktop;

public partial class App : WpfApplication
{
    public static bool IsShuttingDown { get; private set; }
    public static bool IsAutoStartLaunch { get; private set; }
    private NotifyIcon? _notifyIcon;

    protected override void OnStartup(WpfStartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = LoadSettings();
        InitializeTrayIcon();
        var window = new MainWindow();
        MainWindow = window;
        if (settings.AutoStartEnabled)
        {
            IsAutoStartLaunch = true;
            window.ShowInTaskbar = false;
            window.Opacity = 0;
            window.Show();
            window.Hide();
            window.Opacity = 1;
        }
        else
        {
            IsAutoStartLaunch = false;
            window.Show();
        }
    }

    protected override void OnExit(WpfExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        base.OnExit(e);
    }

    public static void RequestShutdown()
    {
        IsShuttingDown = true;
        WpfApplication.Current.Shutdown();
    }

    private void InitializeTrayIcon()
    {
        var icon = LoadTrayIcon() ?? SystemIcons.Application;
        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "Nele AI",
            Visible = true
        };

        var menu = new ContextMenuStrip
        {
            BackColor = Color.FromArgb(34, 34, 34),
            ForeColor = Color.FromArgb(230, 233, 238),
            ShowImageMargin = true,
            ShowCheckMargin = false,
            ImageScalingSize = new Size(18, 18),
            Padding = new Padding(4)
        };
        menu.Renderer = new TrayMenuRenderer()
        {
            RoundedEdges = true
        };

        menu.Items.Add(CreateTrayItem("\u00D6ffnen", "\uE721", (_, _) => ShowMainWindow()));
        menu.Items.Add(CreateTrayItem("Einstellungen", "\uE713", (_, _) => OpenSettings()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateTrayItem("Beenden", "\uE7E8", (_, _) => RequestShutdown()));
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (WpfApplication.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.ShowFromTray();
        }
    }

    private void OpenSettings()
    {
        if (WpfApplication.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.OpenSettingsFromTray();
        }
    }

    private Icon? LoadTrayIcon()
    {
        var resource = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Assets/NeleAi.ico"));
        if (resource is null)
        {
            return null;
        }

        return new Icon(resource.Stream);
    }

    private ToolStripMenuItem CreateTrayItem(string text, string glyph, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text, new Bitmap(1, 1), onClick)
        {
            Tag = glyph,
            ForeColor = TrayMenuRenderer.ItemTextColor,
            Padding = new Padding(12, 8, 12, 8),
            ImageScaling = ToolStripItemImageScaling.SizeToFit,
            TextAlign = ContentAlignment.MiddleLeft,
            ImageAlign = ContentAlignment.MiddleLeft
        };
        item.Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        return item;
    }

    private static AppSettings LoadSettings()
    {
        try
        {
            return new AppDataStore().LoadSettingsAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private sealed class TrayMenuRenderer : ToolStripProfessionalRenderer
    {
        public static readonly Color ItemTextColor = Color.FromArgb(225, 228, 234);
        private static readonly Color ItemHoverTextColor = ItemTextColor;
        private static readonly Color ItemCheckedTextColor = Color.FromArgb(152, 194, 255);
        private static readonly Color ItemCheckedIconColor = Color.FromArgb(88, 150, 255);
        private static readonly Color MenuBackground = Color.FromArgb(36, 36, 36);
        private static readonly Color MenuHoverBackground = Color.FromArgb(56, 56, 56);
        private static readonly Color MenuBorder = Color.FromArgb(30, 30, 30);
        private static readonly Color MenuSeparator = Color.FromArgb(64, 64, 64);
        private static readonly string IconFontName = ResolveIconFont();
        private static readonly Font MenuFont = new("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

        public TrayMenuRenderer() : base(new TrayColorTable())
        {
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var baseBrush = new SolidBrush(MenuBackground))
            {
                e.Graphics.FillRectangle(baseBrush, new Rectangle(Point.Empty, e.Item.Size));
            }

            if (!e.Item.Selected)
            {
                return;
            }

            var padding = 4;
            var bounds = new Rectangle(padding, 2, e.Item.Size.Width - (padding * 2), e.Item.Size.Height - 4);
            using var brush = new SolidBrush(MenuHoverBackground);
            using var path = CreateRoundedRectangle(bounds, 8);
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var isChecked = e.Item is ToolStripMenuItem menuItem && menuItem.Checked;
            var color = isChecked ? ItemCheckedTextColor : ItemTextColor;
            if (e.Item.Selected)
            {
                color = isChecked ? ItemCheckedTextColor : ItemHoverTextColor;
            }

            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var bounds = e.Item.Bounds;
            var left = e.TextRectangle.Left;
            var width = bounds.Width - left - 8;
            if (width <= 0)
            {
                left = Math.Max(36, e.TextRectangle.Left + 6);
                width = bounds.Width - left - 8;
            }
            if (width <= 0)
            {
                base.OnRenderItemText(e);
                return;
            }

            var textBounds = new Rectangle(left, 0, width, bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                e.Text,
                MenuFont,
                textBounds,
                color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Item is ToolStripMenuItem item && item.Tag is string glyph)
            {
                var color = item.Checked ? ItemCheckedIconColor : (item.Selected ? ItemHoverTextColor : ItemTextColor);
                using var font = new Font(IconFontName, 12.5f, FontStyle.Regular, GraphicsUnit.Pixel);
                e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                TextRenderer.DrawText(e.Graphics,
                    glyph,
                    font,
                    e.ImageRectangle,
                    color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            base.OnRenderItemImage(e);
        }

        private static string ResolveIconFont()
        {
            using var fonts = new InstalledFontCollection();
            foreach (var family in fonts.Families)
            {
                if (string.Equals(family.Name, "Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase))
                {
                    return family.Name;
                }
            }

            return "Segoe MDL2 Assets";
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var bounds = new Rectangle(12, e.Item.ContentRectangle.Height / 2, e.Item.ContentRectangle.Width - 24, 1);
            using var pen = new Pen(MenuSeparator);
            e.Graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Right, bounds.Top);
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();

            return path;
        }

        private sealed class TrayColorTable : ProfessionalColorTable
        {
            public TrayColorTable()
            {
                UseSystemColors = false;
            }

            public override Color ToolStripDropDownBackground => MenuBackground;
            public override Color ImageMarginGradientBegin => MenuBackground;
            public override Color ImageMarginGradientMiddle => MenuBackground;
            public override Color ImageMarginGradientEnd => MenuBackground;
            public override Color MenuItemSelected => MenuHoverBackground;
            public override Color MenuItemBorder => Color.FromArgb(68, 68, 68);
            public override Color SeparatorDark => MenuSeparator;
            public override Color SeparatorLight => MenuSeparator;
            public override Color ToolStripBorder => MenuBorder;
        }
    }
}
