using System.Drawing;
using System.Windows.Forms;

namespace Pcars2Collector;

public sealed class CollectorForm : Form
{
    private readonly CollectorStatus status;
    private readonly Label crest2Value = StatusValue();
    private readonly Label backendValue = StatusValue();
    private readonly Label latestValue = new();
    private readonly Label responseValue = new();
    private readonly ListView activityList = new();
    private readonly NotifyIcon notifyIcon;
    private readonly System.Windows.Forms.Timer refreshTimer;
    private DateTimeOffset? lastNotification;

    public CollectorForm(CollectorStatus status)
    {
        this.status = status;
        Text = "PCARS2 Time Trial Collector";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 500);
        MinimumSize = new Size(620, 420);
        BackColor = Color.FromArgb(15, 22, 23);
        ForeColor = Color.FromArgb(232, 241, 235);
        Font = new Font("Segoe UI", 10F);

        notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "PCARS2 Time Trial Collector",
            Visible = true
        };
        notifyIcon.DoubleClick += (_, _) => RestoreWindow();

        BuildLayout();
        refreshTimer = new System.Windows.Forms.Timer { Interval = 500 };
        refreshTimer.Tick += (_, _) => RefreshStatus();
        refreshTimer.Start();
        FormClosed += (_, _) =>
        {
            refreshTimer.Stop();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        };
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28),
            BackColor = BackColor,
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var title = new Label
        {
            Text = "PCARS2  /  TIME TRIAL COLLECTOR",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(215, 243, 106),
            Padding = new Padding(0, 4, 0, 0)
        };
        root.Controls.Add(title, 0, 0);

        var services = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 8, 0, 8) };
        services.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        services.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        services.Controls.Add(ServicePanel("CREST2", crest2Value), 0, 0);
        services.Controls.Add(ServicePanel("BACKEND", backendValue), 1, 0);
        root.Controls.Add(services, 0, 1);

        var latest = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 8, 0, 8) };
        latest.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        latest.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        latest.Controls.Add(Caption("LATEST LAP"), 0, 0);
        latest.Controls.Add(latestValue, 1, 0);
        latest.Controls.Add(Caption("BACKEND RESPONSE"), 0, 1);
        latest.Controls.Add(responseValue, 1, 1);
        root.Controls.Add(latest, 0, 2);

        activityList.Dock = DockStyle.Fill;
        activityList.View = View.Details;
        activityList.FullRowSelect = true;
        activityList.GridLines = true;
        activityList.BackColor = Color.FromArgb(22, 32, 31);
        activityList.ForeColor = ForeColor;
        activityList.BorderStyle = BorderStyle.None;
        activityList.Columns.Add("TIME", 90);
        activityList.Columns.Add("LAP", 100);
        activityList.Columns.Add("DRIVER / VEHICLE", 250);
        activityList.Columns.Add("RESULT", 220);
        root.Controls.Add(activityList, 0, 3);
    }

    private static Panel ServicePanel(string name, Label value)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(22, 32, 31), Margin = new Padding(0, 0, 8, 0), Padding = new Padding(15, 10, 15, 8) };
        panel.Controls.Add(new Label { Text = name, Dock = DockStyle.Top, Height = 22, ForeColor = Color.FromArgb(145, 160, 153), Font = new Font("Segoe UI", 8F, FontStyle.Bold) });
        panel.Controls.Add(value);
        return panel;
    }

    private static Label StatusValue() => new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 15F, FontStyle.Bold) };
    private static Label Caption(string text) => new() { Text = text, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(145, 160, 153), Font = new Font("Segoe UI", 8F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };

    private void RefreshStatus()
    {
        var snapshot = status.Snapshot();
        SetStatus(crest2Value, snapshot.Crest2Available);
        SetStatus(backendValue, snapshot.BackendAvailable);

        if (snapshot.LatestLap is not null)
        {
            var latest = snapshot.LatestLap;
            latestValue.Text = $"{latest.Lap.CarName}  /  {latest.Lap.TrackLocation}  /  {FormatLap(latest.Lap.LapTimeMilliseconds)}";
            responseValue.Text = latest.ResponseSummary;
            responseValue.ForeColor = ResultColor(latest.DeliveryState);
        }

        activityList.BeginUpdate();
        activityList.Items.Clear();
        foreach (var activity in snapshot.RecentLaps)
        {
            var item = new ListViewItem(activity.UpdatedAt.ToLocalTime().ToString("HH:mm:ss"));
            item.SubItems.Add(FormatLap(activity.Lap.LapTimeMilliseconds));
            item.SubItems.Add($"{activity.Lap.Gamertag} / {activity.Lap.CarName}");
            item.SubItems.Add(activity.ResponseSummary);
            item.ForeColor = ResultColor(activity.DeliveryState);
            activityList.Items.Add(item);
        }
        activityList.EndUpdate();

        var current = snapshot.LatestLap;
        if (current is not null && current.UpdatedAt != lastNotification && current.DeliveryState is not LapDeliveryState.Captured)
        {
            lastNotification = current.UpdatedAt;
            var title = current.DeliveryState == LapDeliveryState.Accepted ? "New lap recorded" : "Lap update";
            notifyIcon.ShowBalloonTip(5000, title, $"{FormatLap(current.Lap.LapTimeMilliseconds)}  {current.ResponseSummary}", ToolTipIcon.Info);
        }
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private static void SetStatus(Label label, bool available)
    {
        label.Text = available ? "CONNECTED" : "WAITING";
        label.ForeColor = available ? Color.FromArgb(115, 212, 194) : Color.FromArgb(255, 155, 120);
    }

    private static Color ResultColor(LapDeliveryState state) => state switch
    {
        LapDeliveryState.Accepted => Color.FromArgb(215, 243, 106),
        LapDeliveryState.Rejected => Color.FromArgb(255, 190, 115),
        LapDeliveryState.Failed => Color.FromArgb(255, 155, 120),
        _ => Color.FromArgb(145, 160, 153)
    };

    private static string FormatLap(long milliseconds)
    {
        var minutes = milliseconds / 60000;
        var seconds = (milliseconds % 60000) / 1000.0;
        return $"{minutes}:{seconds:00.000}";
    }
}
