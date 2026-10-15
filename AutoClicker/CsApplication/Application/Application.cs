internal sealed class Application : Form
{
    readonly Core core = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 10 };
    readonly ToolTip toolTip = new();
    readonly Button hotKey = new() { Text = "None", Dock = DockStyle.Fill, Margin = new Padding(1) };
    readonly ComboBox click = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(1) };
    readonly NumericUpDown gap, powerOff;

    public Application()
    {
        Text = "AutoClicker";
        Font = new Font("Segoe UI", 8f);
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(360, 64);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        KeyPreview = true;

        hotKey.Click += (_, _) => { core.ToggleHotKeyBinding(); DrawUI(); };
        click.Items.AddRange(["Left", "Middle", "Right"]); click.SelectedIndex = core.clickType;
        click.SelectedIndexChanged += (_, _) => core.clickType = click.SelectedIndex;
        gap = Number(core.gapMs); gap.ValueChanged += (_, _) => core.gapMs = (int)gap.Value;
        powerOff = Number(core.powerOffMins); powerOff.ValueChanged += (_, _) => core.powerOffMins = (int)powerOff.Value;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Padding = new Padding(2) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.Controls.Add(Label("Click"), 0, 0); layout.Controls.Add(click, 1, 0);
        layout.Controls.Add(Label("Gap(ms)"), 2, 0); layout.Controls.Add(gap, 3, 0);
        layout.Controls.Add(Label("Hotkey"), 0, 1); layout.Controls.Add(hotKey, 1, 1);
        layout.Controls.Add(Label("PowerOff(mins)"), 2, 1); layout.Controls.Add(powerOff, 3, 1);
        Controls.Add(layout);

        toolTip.SetToolTip(click, "Simu the L/M/R mouse click.");
        toolTip.SetToolTip(hotKey, "Click to bind a key. Esc cancels. Works in the background.\nEnter and KeypadEnter share the same hotkey.");
        toolTip.SetToolTip(powerOff, "Normal PC shutdown after this many minutes of clicking. 0 or less disables it.\nStopping cancels; restarting resets the countdown.");

        timer.Tick += (_, _) => { try { core.Update(); DrawUI(); } catch (Exception ex) { timer.Stop(); MessageBox.Show(ex.Message, "AutoClicker"); } };
        timer.Start();
    }

    void DrawUI() => hotKey.Text = core.isSettingHotKey ? "Press a key" : core.hotKeyName;
    static Label Label(string s) => new() { Text = s, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false };
    static NumericUpDown Number(int v) => new() { Minimum = int.MinValue, Maximum = int.MaxValue, Value = v, Dock = DockStyle.Fill, Margin = new Padding(1) };

    protected override bool ProcessCmdKey(ref Message m, Keys k) => core.isSettingHotKey || (int)(k & Keys.KeyCode) == core.hotKey || base.ProcessCmdKey(ref m, k);
    protected override bool ProcessKeyPreview(ref Message m) => core.isSettingHotKey || ((m.Msg is 0x100 or 0x101 or 0x104 or 0x105) && m.WParam.ToInt64() == core.hotKey) || base.ProcessKeyPreview(ref m);
    protected override void Dispose(bool d) { if (d) { timer.Dispose(); toolTip.Dispose(); } base.Dispose(d); }
}