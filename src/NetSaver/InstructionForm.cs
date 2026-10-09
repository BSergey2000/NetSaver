using System;
using System.Drawing;
using System.Windows.Forms;

namespace NetSaver;

public class InstructionForm : Form
{
    public InstructionForm()
    {
        Text = "Инструкция — как пользоваться NetSaver";
        Width = 820;
        Height = 740;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10f);
        MinimizeBox = false;
        ShowIcon = false;

        var rtb = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
            Font = new Font("Segoe UI", 10f),
            WordWrap = true
        };

        FillText(rtb);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(12) };
        var btnClose = new Button
        {
            Text = "Закрыть",
            Width = 120, Height = 34,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Left = bottom.Width - 132, Top = 10
        };
        btnClose.Click += (_, _) => Close();
        bottom.Controls.Add(btnClose);

        Controls.Add(rtb);
        Controls.Add(bottom);

        ThemeManager.Apply(this);
    }

    private static void FillText(RichTextBox rtb)
    {
        void H1(string s)
        {
            rtb.SelectionFont = new Font("Segoe UI", 13f, FontStyle.Bold);
            rtb.SelectionColor = ThemeManager.IsDark()
                ? Color.FromArgb(120, 200, 255)
                : Color.FromArgb(0, 70, 160);
            rtb.AppendText(s + "\n");
            rtb.SelectionColor = rtb.ForeColor;
        }

        void H2(string s)
        {
            rtb.SelectionFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            rtb.AppendText(s + "\n");
        }

        void P(string s)
        {
            rtb.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Regular);
            rtb.AppendText(s);
        }

        H1("NetSaver — краткая инструкция");
        P("\n");

        H2("Что делает программа");
        P("Сохраняет настройки сетевых адаптеров (IP, маску, шлюз, DNS, режим DHCP)\n" +
          "в отдельные профили и позволяет быстро применять их обратно.\n\n");

        H2("1. Как сохранить текущие настройки");
        P("1) В верхней панели выбери адаптер из выпадающего списка.\n" +
          "   Ориентируйся на пометку «✓ интернет» — это рабочий адаптер.\n" +
          "   Виртуальные (VirtualBox, Hyper-V, WSL) лучше не трогать.\n\n" +
          "2) В правой колонке введи имя профиля и, при желании, заметку.\n\n" +
          "3) Нажми «💾 Сохранить текущие настройки».\n" +
          "   Профиль появится в списке слева.\n\n");

        H2("2. Как создать профиль вручную");
        P("1) Нажми «➕ Создать новый профиль вручную».\n\n" +
          "2) Заполни поля: имя, адаптер, режим (DHCP или Вручную).\n\n" +
          "3) Если «Вручную» — укажи IP, маску, шлюз и DNS:\n" +
          "   • Маску можно вводить как 24, 255.255.255.0 или /24.\n" +
          "   • DNS — по одному в строке.\n\n" +
          "4) Нажми «💾 Сохранить профиль».\n\n");

        H2("3. Как применить профиль");
        P("• Выбери профиль в списке и нажми «⚙ Применить выбранный профиль».\n" +
          "• Или сделай двойной клик по профилю.\n" +
          "• Или нажми Enter на выбранной строке.\n\n" +
          "Перед применением программа покажет, что будет изменено,\n" +
          "и спросит подтверждение.\n\n");

        H2("4. Как программа выбирает адаптер");
        P("В выпадающем списке сверху:\n" +
          "  ✓ интернет   — есть шлюз, обычно физический порт\n" +
          "  [Ethernet]   — проводной\n" +
          "  [Wi-Fi]      — беспроводной\n" +
          "  без галочки  — виртуальные (VirtualBox, Hyper-V, WSL)\n\n" +
          "По умолчанию выбирается первый с интернетом.\n\n");

        H2("5. Тема оформления");
        P("Меню «Тема» сверху:\n" +
          "  • Автоматически — как в системе\n" +
          "  • Светлая — принудительно\n" +
          "  • Тёмная — принудительно\n\n" +
          "Выбор сохраняется и применяется при следующем запуске.\n\n");

        H2("6. Где лежат профили");
        P("Документы\\NetSaver\\<имя профиля>\n\n" +
          "Внутри каждого профиля:\n" +
          "  • network.json — структурированные настройки (для применения)\n" +
          "  • info.json    — человекочитаемое описание\n" +
          "  • ipconfig_all.txt, route_print.txt, arp_a.txt, netstat_ano.txt\n\n");

        H2("7. Горячие клавиши");
        P("Enter         — применить выбранный профиль\n" +
          "Двойной клик  — применить профиль\n" +
          "F5            — обновить список профилей\n" +
          "Ctrl+I        — открыть эту инструкцию\n" +
          "Ctrl+D        — тёмная тема\n" +
          "Ctrl+L        — светлая тема\n" +
          "Ctrl+A        — тема «авто»\n");

        rtb.SelectionStart = 0;
        rtb.SelectionLength = 0;
        rtb.ScrollToCaret();
    }
}