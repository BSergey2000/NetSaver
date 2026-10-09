using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace NetSaver;

public class NewProfileForm : Form
{
    private readonly string _root;

    private TextBox _txtName = null!;
    private ComboBox _cmbAdapter = null!;
    private RadioButton _rbDhcp = null!, _rbStatic = null!;
    private Panel _staticPanel = null!;
    private TextBox _txtIp = null!, _txtMask = null!, _txtGateway = null!;
    private TextBox _txtDns = null!, _txtNotes = null!;

    public NewProfileForm(string root)
    {
        _root = root;
        Text = "Новый профиль сети";
        Width = 660;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        BuildUi();
        LoadAdapters();
    }

    private void BuildUi()
    {
        int y = 15;

        // Имя профиля
        Controls.Add(new Label { Text = "Имя профиля:", Left = 15, Top = y + 3, Width = 120 });
        _txtName = new TextBox { Left = 140, Top = y, Width = 480 };
        Controls.Add(_txtName);
        y += 34;

        // Адаптер
        Controls.Add(new Label { Text = "Адаптер:", Left = 15, Top = y + 3, Width = 120 });
        _cmbAdapter = new ComboBox
        {
            Left = 140, Top = y, Width = 480,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Controls.Add(_cmbAdapter);
        y += 42;

        // Режим
        Controls.Add(new Label { Text = "Режим:", Left = 15, Top = y + 3, Width = 120 });
        _rbDhcp = new RadioButton { Text = "Автоматически (DHCP)", Left = 140, Top = y, Width = 230 };
        _rbStatic = new RadioButton { Text = "Вручную (статический)", Left = 380, Top = y, Width = 250 };
        _rbDhcp.Checked = true;
        _rbDhcp.CheckedChanged += (_, _) => UpdateMode();
        _rbStatic.CheckedChanged += (_, _) => UpdateMode();
        Controls.Add(_rbDhcp);
        Controls.Add(_rbStatic);
        y += 40;

        // Панель со статическими параметрами
        _staticPanel = new Panel { Left = 15, Top = y, Width = 610, Height = 250 };

        int yy = 0;
        _staticPanel.Controls.Add(new Label { Text = "IP-адрес:", Left = 0, Top = yy + 3, Width = 120 });
        _txtIp = new TextBox { Left = 125, Top = yy, Width = 170 };
        _staticPanel.Controls.Add(_txtIp);

        _staticPanel.Controls.Add(new Label { Text = "Маска:", Left = 310, Top = yy + 3, Width = 60 });
        _txtMask = new TextBox { Left = 375, Top = yy, Width = 230, Text = "255.255.255.0" };
        _staticPanel.Controls.Add(_txtMask);
        yy += 34;

        _staticPanel.Controls.Add(new Label { Text = "Шлюз:", Left = 0, Top = yy + 3, Width = 120 });
        _txtGateway = new TextBox { Left = 125, Top = yy, Width = 480 };
        _staticPanel.Controls.Add(_txtGateway);
        yy += 34;

        _staticPanel.Controls.Add(new Label { Text = "DNS-серверы:", Left = 0, Top = yy + 3, Width = 120 });
        _staticPanel.Controls.Add(new Label
        {
            Text = "(по одному в строке)",
            Left = 125, Top = yy + 3, Width = 250, ForeColor = Color.DimGray
        });
        yy += 22;
        _txtDns = new TextBox
        {
            Left = 125, Top = yy, Width = 480, Height = 90,
            Multiline = true, ScrollBars = ScrollBars.Vertical
        };
        _staticPanel.Controls.Add(_txtDns);

        Controls.Add(_staticPanel);
        y += _staticPanel.Height + 10;

        // Заметки
        Controls.Add(new Label { Text = "Заметки:", Left = 15, Top = y + 3, Width = 120 });
        _txtNotes = new TextBox
        {
            Left = 140, Top = y, Width = 480, Height = 55, Multiline = true
        };
        Controls.Add(_txtNotes);
        y += 70;

        // Кнопки
        var btnOk = new Button
        {
            Text = "💾  Сохранить профиль",
            Left = 380, Top = y, Width = 150, Height = 34
        };
        btnOk.Click += (_, _) => Save();
        Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "Отмена",
            Left = 540, Top = y, Width = 80, Height = 34
        };
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(btnCancel);

        UpdateMode();
    }

    private void LoadAdapters()
    {
        _cmbAdapter.Items.Clear();
        var list = NetworkInfo.ListAdapters();
        foreach (var a in list) _cmbAdapter.Items.Add(a);
        if (_cmbAdapter.Items.Count > 0) _cmbAdapter.SelectedIndex = 0;
    }

    private void UpdateMode()
    {
        bool enabled = _rbStatic.Checked;
        foreach (Control c in _staticPanel.Controls)
            c.Enabled = enabled;
        _staticPanel.Enabled = enabled;
    }

    private void Save()
    {
        // Имя
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("Введи имя профиля.", "Проверка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Адаптер
        var ad = _cmbAdapter.SelectedItem as AdapterInfo;
        if (ad == null)
        {
            MessageBox.Show("Выбери адаптер.", "Проверка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var info = new NetworkInfo
        {
            InterfaceAlias = ad.Name,
            DhcpEnabled = _rbDhcp.Checked,
            Notes = _txtNotes.Text,
            SavedAt = DateTime.Now
        };

        if (_rbStatic.Checked)
        {
            // IP
            var ip = _txtIp.Text.Trim();
            if (!System.Net.IPAddress.TryParse(ip, out var ipAddr))
            {
                MessageBox.Show("Некорректный IP-адрес.", "Проверка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Маска
            int prefix = ParseMask(_txtMask.Text.Trim());
            if (prefix < 0)
            {
                MessageBox.Show(
                    "Некорректная маска.\nПримеры: 24, 255.255.255.0 или /24.",
                    "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Шлюз (необязательно)
            var gw = _txtGateway.Text.Trim();
            if (!string.IsNullOrEmpty(gw) && !System.Net.IPAddress.TryParse(gw, out _))
            {
                MessageBox.Show("Некорректный шлюз.", "Проверка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // DNS (многострочно)
            var dnsList = new List<string>();
            foreach (var line in _txtDns.Text.Split(
                         new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var d = line.Trim();
                if (d.Length == 0) continue;
                if (!System.Net.IPAddress.TryParse(d, out _))
                {
                    MessageBox.Show($"Некорректный DNS-сервер: {d}", "Проверка",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                dnsList.Add(d);
            }

            info.IpAddress = ipAddr.ToString();
            info.PrefixLength = prefix;
            info.Gateway = string.IsNullOrWhiteSpace(gw) ? null : gw;
            info.Dns = dnsList;
        }

        var dir = Path.Combine(_root, Sanitize(_txtName.Text));
        if (Directory.Exists(dir))
        {
            if (MessageBox.Show(
                    $"Профиль «{_txtName.Text}» уже существует.\nПерезаписать?",
                    "Профиль занят", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            try { Directory.Delete(dir, true); } catch { }
        }
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "network.json"),
            JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));

        var view = new
        {
            name = _txtName.Text,
            notes = info.Notes,
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
            manual = true,
            os = Environment.OSVersion.ToString()
        };
        File.WriteAllText(Path.Combine(dir, "info.json"),
            JsonSerializer.Serialize(view, new JsonSerializerOptions { WriteIndented = true }));

        DialogResult = DialogResult.OK;
        Close();
    }

    private static int ParseMask(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return -1;
        s = s.Trim();
        if (s.StartsWith("/")) s = s.Substring(1);

        // Просто число 0..32
        if (int.TryParse(s, out var n) && n >= 0 && n <= 32) return n;

        // Маска вида 255.255.255.0
        if (!System.Net.IPAddress.TryParse(s, out var ip)) return -1;
        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return -1;
        uint m = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) |
                 ((uint)bytes[2] << 8) | bytes[3];
        int prefix = 0;
        for (int i = 31; i >= 0; i--)
        {
            if ((m & (1u << i)) != 0) prefix++;
            else break;
        }
        uint mask = prefix == 0 ? 0 : 0xFFFFFFFFu << (32 - prefix);
        if (mask != m) return -1;
        return prefix;
    }

    private static string PrefixToMask(int prefix)
    {
        if (prefix < 0 || prefix > 32) return "";
        uint mask = prefix == 0 ? 0 : 0xFFFFFFFFu << (32 - prefix);
        return $"{(mask >> 24) & 0xFF}.{(mask >> 16) & 0xFF}.{(mask >> 8) & 0xFF}.{mask & 0xFF}";
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Trim();
    }
}