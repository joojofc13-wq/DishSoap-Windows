using System.Drawing;
using System.Windows.Forms;

namespace Dishwasher
{
    internal sealed class QolConfigurationPanel : UserControl
    {
        private readonly CheckBox _quickExits = new(), _pause = new(), _shake = new();
        internal QolConfigurationPanel(DesktopSettings settings)
        {
            Dock = DockStyle.Fill;
            Add(_quickExits, "Quick room exits", 20, settings.QuickRoomExits);
            Note("At an open side exit, point toward it to leave immediately.\nRemoves the hold delay; normal fades and loading still apply.", 52);
            Add(_pause, "Pause when switching windows", 123, settings.PauseOnFocusLoss);
            Note("Automatically pauses normal gameplay when the game loses focus.\nResume manually after returning. Waits for transitions to finish.", 155);
            Add(_shake, "Reduced camera shake", 226, settings.ReducedCameraShake);
            Note("Reduces camera shake and rotation to 25% of normal.\nController vibration has its own setting in the Game tab.", 258);
            Note("Save settings and restart the game to apply these options.\nQuick room exits is enabled by default. Uncheck it for the original hold delay.", 352);
        }
        private void Add(CheckBox box, string text, int y, bool value)
        { box.Text = text; box.SetBounds(16, y, 430, 27); box.Checked = value; Controls.Add(box); }
        private void Note(string text, int y) => Controls.Add(new Label { Text = text, Location = new Point(18, y), Size = new Size(430, 60) });
        internal void SaveChoices(DesktopSettings settings)
        { settings.QuickRoomExits = _quickExits.Checked; settings.PauseOnFocusLoss = _pause.Checked; settings.ReducedCameraShake = _shake.Checked; }
    }
}
