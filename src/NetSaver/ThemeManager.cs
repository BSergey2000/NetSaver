using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace NetSaver;

public enum ThemeMode { Auto, Light, Dark }

public static class ThemeManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NetSaver", "settings.json");

    public static ThemeMode Mode { get; private set; } = ThemeMode.Auto;

    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            if (doc.RootElement.TryGetProperty("theme", out var t))
            {
                Mode = (t.GetString() ?? "Auto") switch
                {
                    "Light" => ThemeMode.Light,
                    "Dark" => ThemeMode.Dark,
                    _ => ThemeMode.Auto
                };
            }
        }
        catch { }
    }

    public static void SetMode(ThemeMode m)
    {
        Mode = m;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(new { theme = Mode.ToString() }));
        }
        catch { }
    }

    public static bool IsDark()
    {
        if (Mode == ThemeMode.Dark) return true;
        if (Mode == ThemeMode.Light) return false;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var v = key?.GetValue("AppsUseLightTheme");
            if (v is int i) return i == 0;
        }
        catch { }
        return false;
    }

    public static void Apply(Form form)
    {
        bool dark = IsDark();

        var bg = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
        var fg = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
        var inputBg = dark ? Color.FromArgb(48, 48, 52) : Color.White;
        var border = dark ? Color.FromArgb(80, 80, 80) : Color.Gainsboro;
        var btnBg = dark ? Color.FromArgb(60, 60, 64) : SystemColors.Control;
        var selBg = dark ? Color.FromArgb(0, 120, 215) : SystemColors.Highlight;

        form.BackColor = bg;
        form.ForeColor = fg;

        Walk(form, bg, fg, inputBg, border, btnBg, selBg, dark);

        if (form.MainMenuStrip != null)
        {
            form.MainMenuStrip.BackColor = bg;
            form.MainMenuStrip.ForeColor = fg;
            form.MainMenuStrip.Renderer = dark
                ? new ToolStripProfessionalRenderer(new DarkColorTable())
                : new ToolStripProfessionalRenderer();
        }
    }

    private static void Walk(Control parent, Color bg, Color fg,
                             Color inputBg, Color border, Color btnBg,
                             Color selBg, bool dark)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                // CheckedListBox ДО ListBox — иначе не скомпилируется,
                // CheckedListBox наследуется от ListBox.
                case CheckedListBox clb:
                    clb.BackColor = inputBg;
                    clb.ForeColor = fg;
                    clb.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ListBox lb:
                    lb.BackColor = inputBg;
                    lb.ForeColor = fg;
                    lb.BorderStyle = BorderStyle.FixedSingle;
                    break;

                // RadioButton/CheckBox ДО Button — они не Button, но на всякий случай.
                case RadioButton rb:
                    rb.BackColor = Color.Transparent;
                    rb.ForeColor = fg;
                    break;

                case CheckBox cbx:
                    cbx.BackColor = Color.Transparent;
                    cbx.ForeColor = fg;
                    break;

                case Button b:
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = border;
                    b.BackColor = btnBg;
                    b.ForeColor = fg;
                    b.UseVisualStyleBackColor = false;
                    break;

                case TextBox tb:
                    tb.BackColor = inputBg;
                    tb.ForeColor = fg;
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case RichTextBox rtb:
                    rtb.BackColor = inputBg;
                    rtb.ForeColor = fg;
                    break;

                case ComboBox cb:
                    cb.BackColor = inputBg;
                    cb.ForeColor = fg;
                    cb.FlatStyle = FlatStyle.Flat;
                    break;

                case Label lbl:
                    lbl.BackColor = Color.Transparent;
                    if (lbl.ForeColor == Color.DimGray)
                        lbl.ForeColor = dark ? Color.FromArgb(160, 160, 160) : Color.DimGray;
                    else if (lbl.ForeColor == Color.Firebrick)
                        lbl.ForeColor = dark ? Color.FromArgb(240, 130, 130) : Color.Firebrick;
                    else if (lbl.ForeColor == Color.DarkGreen)
                        lbl.ForeColor = dark ? Color.FromArgb(120, 220, 120) : Color.DarkGreen;
                    else
                        lbl.ForeColor = fg;
                    break;

                case Panel p:
                    p.BackColor = bg;
                    break;

                case GroupBox gb:
                    gb.BackColor = bg;
                    gb.ForeColor = fg;
                    break;

                case MenuStrip ms:
                    ms.BackColor = bg;
                    ms.ForeColor = fg;
                    ms.Renderer = dark
                        ? new ToolStripProfessionalRenderer(new DarkColorTable())
                        : new ToolStripProfessionalRenderer();
                    break;

                case ToolStrip ts:
                    ts.BackColor = bg;
                    ts.ForeColor = fg;
                    break;
            }

            if (c.HasChildren) Walk(c, bg, fg, inputBg, border, btnBg, selBg, dark);
        }
    }

    private class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color MenuBg = Color.FromArgb(45, 45, 48);
        private static readonly Color Hover = Color.FromArgb(62, 62, 66);
        private static readonly Color Border = Color.FromArgb(80, 80, 80);

        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemBorder => Border;
        public override Color MenuBorder => Border;
        public override Color MenuItemPressedGradientBegin => MenuBg;
        public override Color MenuItemPressedGradientEnd => MenuBg;
        public override Color ToolStripDropDownBackground => MenuBg;
        public override Color ImageMarginGradientBegin => MenuBg;
        public override Color ImageMarginGradientMiddle => MenuBg;
        public override Color ImageMarginGradientEnd => MenuBg;
    }
}