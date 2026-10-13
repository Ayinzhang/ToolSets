internal sealed class Application : Form
{
    readonly Core core = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    readonly ToolTip toolTip = new();
    readonly Button hotKey = new() { Text = "None", Dock = DockStyle.Fill, Margin = new Padding(1) };

    public Application()
    {
        Text = "AutoClicker"; ClientSize = new Size(360, 64);
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = Size; MaximizeBox = false; KeyPreview = true;

        var click = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(1) };
        click.Items.AddRange(["Left", "Middle", "Right"]); click.SelectedIndex = core.clickType;
        click.SelectedIndexChanged += (_, _) => core.clickType = click.SelectedIndex;
        var gap = Number(core.gapMs); gap.ValueChanged += (_, _) => core.gapMs = (int)gap.Value;
        var powerOff = Number(core.powerOffMins); powerOff.ValueChanged += (_, _) => core.powerOffMins = (int)powerOff.Value;
        hotKey.Click += (_, _) => { core.ToggleHotKeyBinding(); DrawUI(); };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Padding = new Padding(4) };
        foreach (int width in new[] { 50, 90, 120 }) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.Controls.Add(Label("Click"), 0, 0); layout.Controls.Add(click, 1, 0);
        layout.Controls.Add(Label("Gap(ms)"), 2, 0); layout.Controls.Add(gap, 3, 0);
        layout.Controls.Add(Label("Hotkey"), 0, 1); layout.Controls.Add(hotKey, 1, 1);
        layout.Controls.Add(Label("PowerOff(mins)"), 2, 1); layout.Controls.Add(powerOff, 3, 1);
        Controls.Add(layout);

        toolTip.SetToolTip(click, "Simu the L/M/R mouse click.");
        toolTip.SetToolTip(hotKey, "Click to bind a key. Esc cancels. Works in the background.\nEnter and KeypadEnter share the same hotkey.");
        toolTip.SetToolTip(powerOff, "Normal PC shutdown after this many minutes of clicking. 0 or less disables it.\nStopping cancels; restarting resets the countdown.");
        timer.Tick += (_, _) => { core.Update(); DrawUI(); };
        timer.Start();
    }

    private void DrawUI() => hotKey.Text = core.isSettingHotKey ? "Press a key" : core.hotKeyName;

    private static Label Label(string text) => new()
    {
        Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = true
    };

    private static NumericUpDown Number(int value) => new()
    {
        Minimum = int.MinValue, Maximum = int.MaxValue, Value = value, Dock = DockStyle.Fill, Margin = new Padding(1)
    };

    protected override bool ProcessCmdKey(ref Message message, Keys keyData) =>
        core.isSettingHotKey || (int)(keyData & Keys.KeyCode) == core.hotKey || base.ProcessCmdKey(ref message, keyData);

    protected override bool ProcessKeyPreview(ref Message message) =>
        core.isSettingHotKey ||
        ((message.Msg is 0x100 or 0x101 or 0x104 or 0x105) && message.WParam.ToInt64() == core.hotKey) ||
        base.ProcessKeyPreview(ref message);

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); toolTip.Dispose(); }
        base.Dispose(disposing);
    }
}
