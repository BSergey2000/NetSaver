using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace NetSaver;

public class MainForm : Form
{
    private readonly string _root;
    private ListBox _listProfiles = null!;
    private TextBox _txtName = null!, _txtNotes = null!;
    private Label _lblAdapter = null!, _lblIPv4 = null!, _lblGateway = null!, _lblDNS = null!, _lblDhcp = null!;
    private Button _btnApply = null!;
    private ComboBox _cmbAdapter = null!;
    private bool _loadingAdapters = false;
    private NetworkInfo? _current;
    private MenuStrip _menu = null!;
    private ToolStripMenuItem _themeAuto = null!, _themeLight = null!, _themeDark = null!;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "NetSaver_log.txt");

    private static void Log(string s)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}  [form] {s}\n"); } catch { }
    }

    private static void LogErr(string where, Exception ex) => Log($"[{where}] {ex}");

    public MainForm()
    {
        ThemeManager.Load();

        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "NetSaver");
        Directory.CreateDirectory(_root);

        Text = "NetSaver — профили сетевых настроек";
        Width = 1050;
        Height = 740;
        MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        KeyPreview = true;

        BuildUi();

        Shown += (_, _) =>
        {
            ThemeManager.Apply(this);
            UpdateThemeChecks();

            BeginInvoke(new Action(() =>
            {
                try { LoadAdapters(); } catch (Exception ex) { LogErr("LoadAdapters", ex); }
                try { LoadProfiles(); } catch (Exception ex) { LogErr("LoadProfiles", ex); }
            }));
        };
    }

    private void BuildUi()
    {
        // ===== Меню =====
        _menu = new MenuStrip { Dock = DockStyle.Top };

        var fileMenu = new ToolStripMenuItem("Файл");
        fileMenu.DropDownItems.Add("📖  Инструкция", null, (_, _) => ShowInstruction());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Выход", null, (_, _) => Close());

        var themeMenu = new ToolStripMenuItem("Тема");
        _themeAuto = new ToolStripMenuItem("Автоматически (по системе)");
        _themeLight = new ToolStripMenuItem("Светлая");
        _themeDark = new ToolStripMenuItem("Тёмная");
        _themeAuto.Click += (_, _) => SetTheme(ThemeMode.Auto);
        _themeLight.Click += (_, _) => SetTheme(ThemeMode.Light);
        _themeDark.Click += (_, _) => SetTheme(ThemeMode.Dark);
        themeMenu.DropDownItems.Add(_themeAuto);
        themeMenu.DropDownItems.Add(_themeLight);
        themeMenu.DropDownItems.Add(_themeDark);

        var helpMenu = new ToolStripMenuItem("Справка");
        helpMenu.DropDownItems.Add("📖  Инструкция", null, (_, _) => ShowInstruction());
        helpMenu.DropDownItems.Add("Горячие клавиши", null, (_, _) => ShowInstruction());
        helpMenu.DropDownItems.Add(new ToolStripSeparator());
        helpMenu.DropDownItems.Add("О программе", null, (_, _) =>
        {
            MessageBox.Show(
                "NetSaver\n\nПрофили сетевых настроек:\nIP, маска, шлюз, DNS, DHCP.\n\n" +
                "Профили хранятся в Документах в папке NetSaver.",
                "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });

        _menu.Items.Add(fileMenu);
        _menu.Items.Add(themeMenu);
        _menu.Items.Add(helpMenu);
        MainMenuStrip = _menu;

        // ===== Верхняя панель =====
        var topPanel = new Panel { Dock = DockStyle.Top, Height = 200, Padding = new Padding(12) };
        topPanel.Paint += (s, e) =>
        {
            bool dark = ThemeManager.IsDark();
            using var pen = new Pen(dark ? Color.FromArgb(70, 70, 70) : Color.Gainsboro);
            e.Graphics.DrawLine(pen, 0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "Текущие настройки сети",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Left = 12, Top = 8, Width = 200, Height = 26
        };

        var lblPick = new Label
        {
            Text = "Адаптер:",
            Left = 220, Top = 12, Width = 60, Height = 22
        };

        _cmbAdapter = new ComboBox
        {
            Left = 285, Top = 9, Width = 420, Height = 26,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        _cmbAdapter.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingAdapters) return;
            try { RefreshCurrent(); } catch (Exception ex) { LogErr("RefreshCurrent", ex); }
        };

        var btnRefreshCurrent = new Button
        {
            Text = "Обновить список",
            Left = 715, Top = 8, Width = 150, Height = 28,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnRefreshCurrent.Click += (_, _) =>
        {
            try { LoadAdapters(); } catch (Exception ex) { LogErr("LoadAdapters", ex); }
        };

        var btnRefreshOne = new Button
        {
            Text = "Обновить",
            Left = 871, Top = 8, Width = 150, Height = 28,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnRefreshOne.Click += (_, _) =>
        {
            try { RefreshCurrent(); } catch (Exception ex) { LogErr("RefreshCurrent", ex); }
        };

        _lblAdapter = MakeInfoLabel(12, 52, "Адаптер:");
        _lblIPv4    = MakeInfoLabel(12, 78, "IPv4:");
        _lblGateway = MakeInfoLabel(12, 104, "Шлюз:");
        _lblDNS     = MakeInfoLabel(12, 130, "DNS-серверы:");
        _lblDhcp    = MakeInfoLabel(600, 52, "Режим:");

        var lblHint = new Label
        {
            Left = 12, Top = 160, Width = 1000, Height = 22,
            ForeColor = Color.DimGray,
            Text = "Совет: для сохранения рабочего профиля выбери физический Ethernet или Wi-Fi (с пометкой «✓ интернет»), а не VirtualBox/Hyper-V/WSL."
        };

        topPanel.Controls.AddRange(new Control[]
        {
            lblTitle, lblPick, _cmbAdapter, btnRefreshCurrent, btnRefreshOne,
            _lblAdapter, _lblIPv4, _lblGateway, _lblDNS, _lblDhcp, lblHint
        });

        // ===== Список профилей =====
        var midPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        var lblProfilesTitle = new Label
        {
            Text = "Сохранённые профили  (Enter / двойной клик — применить)",
            Dock = DockStyle.Top, Height = 26,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        _listProfiles = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10),
            IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        _listProfiles.SelectedIndexChanged += (_, _) => UpdateApplyButton();
        _listProfiles.DoubleClick += (_, _) =>
        {
            try { ApplySelectedProfile(); } catch (Exception ex) { LogErr("Apply", ex); }
        };

        var listWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 26, 0, 0) };
        listWrapper.Controls.Add(_listProfiles);

        // ===== Правая колонка =====
        var rightPanel = new Panel { Dock = DockStyle.Right, Width = 320, Padding = new Padding(12, 0, 0, 0) };

        var lblName = new Label { Text = "Имя профиля:", Left = 12, Top = 30, Width = 290 };
        _txtName = new TextBox { Left = 12, Top = 53, Width = 290 };

        var lblNotes = new Label { Text = "Заметки:", Left = 12, Top = 85, Width = 290 };
        _txtNotes = new TextBox { Left = 12, Top = 108, Width = 290 };

        var btnSave = new Button
        {
            Text = "💾  Сохранить текущие настройки",
            Left = 12, Top = 148, Width = 290, Height = 36
        };
        btnSave.Click += (_, _) =>
        {
            try { SaveCurrentAsProfile(); } catch (Exception ex) { LogErr("Save", ex); }
        };

        var btnNew = new Button
        {
            Text = "➕  Создать новый профиль вручную",
            Left = 12, Top = 190, Width = 290, Height = 36
        };
        btnNew.Click += (_, _) =>
        {
            try
            {
                using var f = new NewProfileForm(_root);
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    LoadProfiles();
                    ThemeManager.Apply(this);
                }
            }
            catch (Exception ex) { LogErr("NewProfile", ex); }
        };

        _btnApply = new Button
        {
            Text = "⚙  Применить выбранный профиль",
            Left = 12, Top = 234, Width = 290, Height = 36
        };
        _btnApply.Click += (_, _) =>
        {
            try { ApplySelectedProfile(); } catch (Exception ex) { LogErr("Apply", ex); }
        };
        _btnApply.Enabled = false;

        var btnRename = new Button { Text = "Переименовать", Left = 12, Top = 288, Width = 140, Height = 32 };
        btnRename.Click += (_, _) =>
        {
            try { RenameSelected(); } catch (Exception ex) { LogErr("Rename", ex); }
        };

        var btnDelete = new Button { Text = "Удалить", Left = 162, Top = 288, Width = 140, Height = 32 };
        btnDelete.Click += (_, _) =>
        {
            try { DeleteSelected(); } catch (Exception ex) { LogErr("Delete", ex); }
        };

        var btnOpen = new Button { Text = "Открыть папку профиля", Left = 12, Top = 328, Width = 290, Height = 32 };
        btnOpen.Click += (_, _) =>
        {
            try { OpenSelected(); } catch (Exception ex) { LogErr("Open", ex); }
        };

        var btnRefreshList = new Button { Text = "Обновить список (F5)", Left = 12, Top = 368, Width = 290, Height = 32 };
        btnRefreshList.Click += (_, _) =>
        {
            try { LoadProfiles(); } catch (Exception ex) { LogErr("LoadProfiles", ex); }
        };

        var btnInstruction = new Button
        {
            Text = "📖  Инструкция (Ctrl+I)",
            Left = 12, Top = 408, Width = 290, Height = 36
        };
        btnInstruction.Click += (_, _) => ShowInstruction();

        var hint = new Label
        {
            Left = 12, Top = 458, Width = 290, Height = 200,
            ForeColor = Color.DimGray,
            Text = "Горячие клавиши:\n" +
                   "  Enter       — применить профиль\n" +
                   "  F5          — обновить список\n" +
                   "  Ctrl+I      — инструкция\n" +
                   "  Ctrl+D/L/A  — тема: тёмная/светлая/авто\n\n" +
                   "Профили хранятся в Документах,\n" +
                   "в папке NetSaver."
        };

        rightPanel.Controls.AddRange(new Control[]
        {
            lblName, _txtName, lblNotes, _txtNotes,
            btnSave, btnNew, _btnApply, btnRename, btnDelete,
            btnOpen, btnRefreshList, btnInstruction, hint
        });

        midPanel.Controls.Add(listWrapper);
        midPanel.Controls.Add(lblProfilesTitle);
        midPanel.Controls.Add(rightPanel);

        Controls.Add(midPanel);
        Controls.Add(topPanel);
        Controls.Add(_menu);
    }

    private static Label MakeInfoLabel(int x, int y, string prefix) => new()
    {
        Text = prefix + " —",
        Left = x, Top = y, Width = 1000, Height = 24,
        AutoEllipsis = true
    };

    private void SetTheme(ThemeMode m)
    {
        ThemeManager.SetMode(m);
        ThemeManager.Apply(this);
        UpdateThemeChecks();
    }

    private void UpdateThemeChecks()
    {
        _themeAuto.Checked = ThemeManager.Mode == ThemeMode.Auto;
        _themeLight.Checked = ThemeManager.Mode == ThemeMode.Light;
        _themeDark.Checked = ThemeManager.Mode == ThemeMode.Dark;
    }

    private void ShowInstruction()
    {
        using var f = new InstructionForm();
        f.ShowDialog(this);
        ThemeManager.Apply(this);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.I))
        {
            ShowInstruction();
            return true;
        }
        if (keyData == (Keys.Control | Keys.D))
        {
            SetTheme(ThemeMode.Dark);
            return true;
        }
        if (keyData == (Keys.Control | Keys.L))
        {
            SetTheme(ThemeMode.Light);
            return true;
        }
        if (keyData == (Keys.Control | Keys.A))
        {
            SetTheme(ThemeMode.Auto);
            return true;
        }
        if (keyData == Keys.F5)
        {
            try { LoadProfiles(); } catch (Exception ex) { LogErr("LoadProfiles", ex); }
            return true;
        }
        if (keyData == Keys.Enter && _listProfiles.Focused && _listProfiles.SelectedIndex >= 0)
        {
            try { ApplySelectedProfile(); } catch (Exception ex) { LogErr("Apply", ex); }
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void LoadAdapters()
    {
        _loadingAdapters = true;
        try
        {
            var prev = (_cmbAdapter.SelectedItem as AdapterInfo)?.Name;
            _cmbAdapter.Items.Clear();

            var list = NetworkInfo.ListAdapters();
            foreach (var a in list) _cmbAdapter.Items.Add(a);

            if (list.Count == 0)
            {
                _cmbAdapter.Items.Add("(нет активных адаптеров)");
                _cmbAdapter.SelectedIndex = 0;
                return;
            }

            if (prev != null)
            {
                for (int i = 0; i < _cmbAdapter.Items.Count; i++)
                {
                    if ((_cmbAdapter.Items[i] as AdapterInfo)?.Name == prev)
                    {
                        _cmbAdapter.SelectedIndex = i;
                        return;
                    }
                }
            }
            _cmbAdapter.SelectedIndex = 0;
        }
        finally
        {
            _loadingAdapters = false;
        }

        RefreshCurrent();
    }

    private AdapterInfo? SelectedAdapter() => _cmbAdapter.SelectedItem as AdapterInfo;

    private void RefreshCurrent()
    {
        var sel = SelectedAdapter();
        if (sel == null)
        {
            _lblAdapter.Text = "Адаптер: —";
            _lblIPv4.Text = "IPv4: —";
            _lblGateway.Text = "Шлюз: —";
            _lblDNS.Text = "DNS-серверы: —";
            _lblDhcp.Text = "Режим: —";
            return;
        }

        _current = NetworkInfo.ReadAdapter(sel.Name);
        if (_current == null)
        {
            _lblAdapter.Text = $"Адаптер: {sel.Name} — нет данных";
            _lblIPv4.Text = "IPv4: —";
            _lblGateway.Text = "Шлюз: —";
            _lblDNS.Text = "DNS-серверы: —";
            _lblDhcp.Text = "Режим: —";
            return;
        }

        _lblAdapter.Text = $"Адаптер: {_current.InterfaceAlias}";
        _lblIPv4.Text = _current.IpAddress == null
            ? "IPv4: —"
            : $"IPv4: {_current.IpAddress}/{_current.PrefixLength}   ({PrefixToMask(_current.PrefixLength ?? 0)})";
        _lblGateway.Text = $"Шлюз: {(_current.Gateway ?? "—")}";
        _lblDNS.Text = "DNS-серверы: " +
            (_current.Dns.Count > 0 ? string.Join(", ", _current.Dns) : "—");
        _lblDhcp.Text = "Режим: " +
            (_current.DhcpEnabled ? "Автоматически (DHCP)" : "Вручную");
    }

    private static string PrefixToMask(int prefix)
    {
        if (prefix < 0 || prefix > 32) return "";
        uint mask = prefix == 0 ? 0 : 0xFFFFFFFFu << (32 - prefix);
        return $"{(mask >> 24) & 0xFF}.{(mask >> 16) & 0xFF}.{(mask >> 8) & 0xFF}.{mask & 0xFF}";
    }

    private void UpdateApplyButton() => _btnApply.Enabled = _listProfiles.SelectedIndex >= 0;

    private class ProfileItem
    {
        public string Name = "";
        public string Dir = "";
        public override string ToString() => Name;
    }

    private void LoadProfiles()
    {
        _listProfiles.Items.Clear();
        if (!Directory.Exists(_root)) return;

        foreach (var dir in Directory.GetDirectories(_root)
                     .OrderByDescending(Directory.GetCreationTime))
        {
            var name = Path.GetFileName(dir);
            var infoPath = Path.Combine(dir, "info.json");
            var notes = "";
            var summary = "нет данных";

            if (File.Exists(infoPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(infoPath));
                    var r = doc.RootElement;
                    if (r.TryGetProperty("notes", out var n)) notes = n.GetString() ?? "";
                    bool dhcp = r.TryGetProperty("dhcp", out var dhcpEl) && dhcpEl.ValueKind == JsonValueKind.True;
                    bool manual = r.TryGetProperty("manual", out var manEl) && manEl.ValueKind == JsonValueKind.True;
                    if (dhcp) summary = "DHCP (авто)";
                    else
                    {
                        var ip = r.TryGetProperty("ip", out var ipEl) ? ipEl.GetString() : "";
                        var gw = r.TryGetProperty("gateway", out var gwEl) ? gwEl.GetString() : "";
                        var dns = "";
                        if (r.TryGetProperty("dns", out var dnsEl) && dnsEl.ValueKind == JsonValueKind.Array)
                            dns = string.Join(",", dnsEl.EnumerateArray()
                                .Where(x => x.ValueKind == JsonValueKind.String)
                                .Select(x => x.GetString()));
                        summary = $"IP {ip}  GW {gw}  DNS [{dns}]";
                    }
                    if (manual) summary = "✎ " + summary;
                }
                catch { }
            }

            var item = new ProfileItem
            {
                Name = string.IsNullOrWhiteSpace(notes)
                    ? $"{name,-28}  {summary}"
                    : $"{name,-28}  {summary}   — {notes}",
                Dir = dir
            };
            _listProfiles.Items.Add(item);
        }
        UpdateApplyButton();
    }

    private string? SelectedDir() => (_listProfiles.SelectedItem as ProfileItem)?.Dir;

    // ---------- Сохранение ----------

    private void SaveCurrentAsProfile()
    {
        var sel = SelectedAdapter();
        if (sel == null)
        {
            MessageBox.Show("Сначала выбери адаптер в списке сверху.",
                "Нет адаптера", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var info = NetworkInfo.ReadAdapter(sel.Name);
        if (info == null)
        {
            MessageBox.Show(
                "Не удалось прочитать настройки выбранного адаптера.",
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var name = string.IsNullOrWhiteSpace(_txtName.Text)
            ? DateTime.Now.ToString("yyyyMMdd_HHmmss")
            : Sanitize(_txtName.Text);

        var dir = Path.Combine(_root, name);
        if (Directory.Exists(dir))
        {
            if (MessageBox.Show(
                    $"Профиль «{name}» уже существует.\nСоздать с меткой времени?",
                    "Профиль занят", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            dir = Path.Combine(_root, name + "_" + DateTime.Now.ToString("HHmmss"));
        }
        Directory.CreateDirectory(dir);

        info.Notes = _txtNotes.Text;
        info.SavedAt = DateTime.Now;
        File.WriteAllText(Path.Combine(dir, "network.json"),
            JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));

        var view = new
        {
            name,
            notes = _txtNotes.Text,
            created = DateTime.Now.ToString("o"),
            computer = Environment.MachineName,
            user = Environment.UserName,
            adapter = info.InterfaceAlias,
            dhcp = info.DhcpEnabled,
            ip = info.IpAddress,
            prefix = info.PrefixLength,
            mask = info.PrefixLength.HasValue ? PrefixToMask(info.PrefixLength.Value) : null,
            gateway = info.Gateway,
            dns = info.Dns,
            manual = false,
            os = Environment.OSVersion.ToString()
        };
        File.WriteAllText(Path.Combine(dir, "info.json"),
            JsonSerializer.Serialize(view, new JsonSerializerOptions { WriteIndented = true }));

        RunToFile("ipconfig", "/all", Path.Combine(dir, "ipconfig_all.txt"));
        RunToFile("route", "print", Path.Combine(dir, "route_print.txt"));
        RunToFile("arp", "-a", Path.Combine(dir, "arp_a.txt"));
        RunToFile("netstat", "-ano", Path.Combine(dir, "netstat_ano.txt"));

        _txtName.Clear();
        _txtNotes.Clear();
        LoadProfiles();
        MessageBox.Show("Профиль сохранён:\n" + dir, "Готово",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ---------- Применение ----------

    private void ApplySelectedProfile()
    {
        var dir = SelectedDir();
        if (dir == null) return;

        var jsonPath = Path.Combine(dir, "network.json");
        if (!File.Exists(jsonPath))
        {
            MessageBox.Show(
                "В выбранном профиле нет network.json — применить его нельзя.\n" +
                "Такие профили были сохранены старой версией (только текст).",
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        NetworkInfo? info;
        try
        {
            info = JsonSerializer.Deserialize<NetworkInfo>(File.ReadAllText(jsonPath));
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось прочитать профиль:\n" + ex.Message,
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (info == null) return;

        var sb = new StringBuilder();
        sb.AppendLine($"Адаптер: {info.InterfaceAlias}");
        sb.AppendLine($"Режим:   {(info.DhcpEnabled ? "Автоматически (DHCP)" : "Вручную")}");
        if (!info.DhcpEnabled)
        {
            sb.AppendLine($"IPv4:    {info.IpAddress}/{info.PrefixLength}" +
                          $" ({PrefixToMask(info.PrefixLength ?? 0)})");
            sb.AppendLine($"Шлюз:    {info.Gateway}");
            sb.AppendLine($"DNS:     {(info.Dns.Count > 0 ? string.Join(", ", info.Dns) : "—")}");
        }
        sb.AppendLine();
        sb.AppendLine("Применить эти настройки к системе?");

        if (MessageBox.Show(sb.ToString(), "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            ApplyProfile(info);
            System.Threading.Thread.Sleep(1500);
            LoadAdapters();
            MessageBox.Show("Настройки применены.", "Готово",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка применения:\n" + ex.Message,
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ApplyProfile(NetworkInfo info)
    {
        var alias = info.InterfaceAlias.Replace("'", "''");

        if (info.DhcpEnabled)
        {
            RunPs($"Set-NetIPInterface -InterfaceAlias '{alias}' -Dhcp Enabled -ErrorAction Stop");
            RunPs($"Set-DnsClientServerAddress -InterfaceAlias '{alias}' -ResetServerAddresses -ErrorAction Stop");
            return;
        }

        if (string.IsNullOrWhiteSpace(info.IpAddress) || info.PrefixLength == null)
            throw new InvalidOperationException("В профиле нет IPv4-адреса или длины префикса.");

        RunPs($"Remove-NetIPAddress -InterfaceAlias '{alias}' -Confirm:$false -ErrorAction SilentlyContinue");
        RunPs($"Remove-NetRoute -InterfaceAlias '{alias}' -Confirm:$false -ErrorAction SilentlyContinue");

        var gwPart = string.IsNullOrWhiteSpace(info.Gateway)
            ? ""
            : $" -DefaultGateway '{info.Gateway!.Replace("'", "''")}'";

        RunPs($"New-NetIPAddress -InterfaceAlias '{alias}' " +
              $"-IPAddress '{info.IpAddress}' -PrefixLength {info.PrefixLength}" +
              $"{gwPart} -ErrorAction Stop");

        if (info.Dns.Count > 0)
        {
            var list = string.Join(",", info.Dns.Select(d => $"'{d.Replace("'", "''")}'"));
            RunPs($"Set-DnsClientServerAddress -InterfaceAlias '{alias}' -ServerAddresses {list} -ErrorAction Stop");
        }
        else
        {
            RunPs($"Set-DnsClientServerAddress -InterfaceAlias '{alias}' -ResetServerAddresses -ErrorAction SilentlyContinue");
        }
    }

    // ---------- Кнопки ----------

    private void RenameSelected()
    {
        var dir = SelectedDir();
        if (dir == null) return;

        var oldName = Path.GetFileName(dir);
        var newName = Prompt.Show("Новое имя профиля:", "Переименование", oldName);
        if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;

        var target = Path.Combine(_root, Sanitize(newName));
        if (Directory.Exists(target))
        {
            MessageBox.Show("Профиль с таким именем уже есть.");
            return;
        }
        Directory.Move(dir, target);
        LoadProfiles();
    }

    private void DeleteSelected()
    {
        var dir = SelectedDir();
        if (dir == null) return;
        if (MessageBox.Show(
                $"Удалить профиль «{Path.GetFileName(dir)}»?\nПапка будет удалена безвозвратно.",
                "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        Directory.Delete(dir, true);
        LoadProfiles();
    }

    private void OpenSelected()
    {
        var dir = SelectedDir();
        if (dir == null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }

    // ---------- Утилиты ----------

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Trim();
    }

    private static void RunToFile(string exe, string args, string outFile)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using var p = Process.Start(psi)!;
            var outp = p.StandardOutput.ReadToEnd();
            var err = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(15000))
            {
                try { p.Kill(true); } catch { }
                File.WriteAllText(outFile, "Timeout: команда не ответила за 15 секунд.");
                return;
            }
            File.WriteAllText(outFile, outp + Environment.NewLine + err, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            File.WriteAllText(outFile, "Ошибка: " + ex.Message);
        }
    }

    private static string RunPs(string script)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var psi = new ProcessStartInfo(
            "powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var p = Process.Start(psi)!;
        var outpTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(30000))
        {
            try { p.Kill(true); } catch { }
            throw new TimeoutException("PowerShell не ответил за 30 секунд.");
        }
        outpTask.Wait(2000);
        errTask.Wait(2000);
        var outp = outpTask.Result;
        var err = errTask.Result;
        if (p.ExitCode != 0)
            throw new Exception(string.IsNullOrWhiteSpace(err) ? outp : err);
        return outp;
    }
}

// ---------- Модель сетевых настроек ----------

public class AdapterInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
    public bool HasGateway { get; set; }

    public override string ToString()
    {
        var gw = HasGateway ? "  ✓ интернет" : "";
        return $"{Name}  [{Type}]{gw}  —  {Description}";
    }
}

public class NetworkInfo
{
    public string InterfaceAlias { get; set; } = "";
    public int InterfaceIndex { get; set; }
    public string? IpAddress { get; set; }
    public int? PrefixLength { get; set; }
    public string? Gateway { get; set; }
    public List<string> Dns { get; set; } = new();
    public bool DhcpEnabled { get; set; }
    public string Notes { get; set; } = "";
    public DateTime SavedAt { get; set; }

    public static List<AdapterInfo> ListAdapters()
    {
        var list = new List<AdapterInfo>();
        foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                continue;

            var t = ni.NetworkInterfaceType;
            if (t == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
            if (t == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel) continue;

            System.Net.NetworkInformation.IPInterfaceProperties p;
            try { p = ni.GetIPProperties(); } catch { continue; }

            bool hasGw = p.GatewayAddresses.Any(g =>
                g.Address != null &&
                g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            string typeName = t switch
            {
                System.Net.NetworkInformation.NetworkInterfaceType.Ethernet => "Ethernet",
                System.Net.NetworkInformation.NetworkInterfaceType.GigabitEthernet => "Ethernet",
                System.Net.NetworkInformation.NetworkInterfaceType.FastEthernetT => "Ethernet",
                System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                _ => t.ToString()
            };

            list.Add(new AdapterInfo
            {
                Name = ni.Name,
                Description = ni.Description,
                Type = typeName,
                HasGateway = hasGw
            });
        }

        return list
            .OrderByDescending(a => a.HasGateway)
            .ThenBy(a => a.Type == "Ethernet" ? 0 : a.Type == "Wi-Fi" ? 1 : 2)
            .ThenBy(a => a.Name)
            .ToList();
    }

    public static NetworkInfo? ReadAdapter(string name)
    {
        var ni = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.Name == name);
        if (ni == null) return null;

        var info = new NetworkInfo { InterfaceAlias = ni.Name };

        System.Net.NetworkInformation.IPInterfaceProperties p;
        try { p = ni.GetIPProperties(); } catch { return info; }

        var v4 = p.UnicastAddresses.FirstOrDefault(a =>
            a.Address != null &&
            a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        if (v4 != null)
        {
            info.IpAddress = v4.Address.ToString();
            info.PrefixLength = v4.PrefixLength;
        }

        var gw = p.GatewayAddresses.FirstOrDefault(g =>
            g.Address != null &&
            g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        if (gw != null) info.Gateway = gw.Address.ToString();

        info.Dns = p.DnsAddresses
            .Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(d => d.ToString())
            .ToList();

        try
        {
            var v4p = p.GetIPv4Properties();
            if (v4p != null) info.DhcpEnabled = v4p.IsDhcpEnabled;
        }
        catch { }

        return info;
    }

    public static NetworkInfo? ReadCurrent()
    {
        var all = ListAdapters();
        if (all.Count == 0) return null;
        return ReadAdapter(all[0].Name);
    }
}

// ---------- Мини-диалог ввода ----------

public static class Prompt
{
    public static string Show(string text, string caption, string def = "")
    {
        var form = new Form
        {
            Width = 420, Height = 150, Text = caption,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false, MaximizeBox = false
        };
        var lbl = new Label { Left = 12, Top = 12, Text = text, Width = 380 };
        var tb = new TextBox { Left = 12, Top = 42, Width = 380, Text = def };
        var ok = new Button { Text = "OK", Left = 232, Top = 76, Width = 80, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Отмена", Left = 318, Top = 76, Width = 80, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        ThemeManager.Apply(form);
        return form.ShowDialog() == DialogResult.OK ? tb.Text : "";
    }
}