using System.Drawing;
using System.Windows.Forms;

namespace SB2Hotas;

// Shown on the centre monitor when SpaceBourne 2 stops responding while the bridge has it stretched over every
// monitor: a frozen window that covers all screens leaves nothing else to click. It stays on top of the game.
internal sealed class FrozenPrompt : Form
{
    public FrozenPrompt(Action close, Action wait)
    {
        Text = "SB2 HOTAS";
        TopMost = true;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 11f);
        Padding = new Padding(18);
        var text = new Label
        {
            Text = "SpaceBourne 2 isn't responding, and it covers all your monitors.\r\n\r\n"
                 + "It may still be loading: a long load comes back by itself, so wait a little first. "
                 + "If it stays frozen, close it to get your desktop back (progress since your last save is lost).\r\n\r\n"
                 + "Ctrl+Alt+End also closes a frozen SpaceBourne 2.",
            AutoSize = false, Dock = DockStyle.Fill,
        };
        var closeButton = new Button { Text = "Close SpaceBourne 2", AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
        var waitButton = new Button { Text = "Wait", AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
        closeButton.Click += (_, _) => close();
        waitButton.Click += (_, _) => wait();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        buttons.Controls.Add(waitButton);
        buttons.Controls.Add(closeButton);
        Controls.Add(text);
        Controls.Add(buttons);
        ClientSize = new Size(560, 250);
        AcceptButton = waitButton;
    }

    // `index` is the centre monitor's place counting from the left, as Screens.Displays() sorts them.
    public void ShowOn(int index)
    {
        var all = Screen.AllScreens.OrderBy(s => s.Bounds.X).ToArray();
        var area = (index >= 0 && index < all.Length ? all[index] : Screen.PrimaryScreen!).WorkingArea;
        Location = new Point(area.X + (area.Width - Width) / 2, area.Y + (area.Height - Height) / 2);
        Show();
        Activate();
    }
}
