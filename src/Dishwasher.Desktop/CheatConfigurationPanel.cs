using System;
using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace Dishwasher
{
    internal sealed class CheatConfigurationPanel : UserControl
    {
        private readonly CheckBox _enabled = new(), _god = new(), _ammo = new(), _magic = new(), _energy = new();
        private readonly NumericUpDown _ammoValue = new(), _magicValue = new(), _incoming = new(), _enemy = new(), _speed = new(), _orbs = new();
        private readonly ToolTip _help = new();
        private string _saved;
        internal string LastBackup { get; private set; }

        internal CheatConfigurationPanel()
        {
            Dock = DockStyle.Fill;
            Controls.Add(new Label {
                Text = "Cheats can permanently change your save.\nBack up saves before making any changes.",
                ForeColor = Color.Firebrick, Location = new Point(16, 12), Size = new Size(430, 44)
            });
            var backup = new Button { Text = "Back up saves", Location = new Point(16, 61), Size = new Size(160, 30) };
            backup.Click += (_, _) => {
                try
                {
                    string folder = DesktopSaveBackup.Create();
                    if (folder == null) MessageBox.Show(this, "No saves found yet.", "dishwasher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "dishwasher", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            Controls.Add(backup);
            AddCheck(_enabled, "Enable cheats", 100);
            AddCheck(_god, "Infinite health (damage protection)", 132);
            AddCheck(_ammo, "Lock ammunition to:", 164); AddNumber(_ammoValue, 164, 1, 999, 0, 1);
            AddCheck(_magic, "Lock magic skulls to:", 196); AddNumber(_magicValue, 196, 1, 9, 0, 1);
            AddCheck(_energy, "Infinite movement energy", 228);
            AddValue("Player damage taken:", _incoming, 264, 0, 10, 2, .25m);
            AddValue("Enemy damage taken:", _enemy, 298, .1m, 20, 2, .25m);
            AddValue("Movement speed:", _speed, 332, .5m, 3, 2, .1m);
            AddValue("Orbs earned:", _orbs, 366, 1, 100, 0, 1);
            Controls.Add(new Label { Text = "Values are multipliers (1 = normal). Save to apply in-game.\nEarned orbs and progress are saved normally.\nA save backup is made before saving enabled cheat changes.",
                Location = new Point(16, 405), Size = new Size(435, 54) });
            var reset = new Button { Text = "Reset cheats", Location = new Point(16, 465), Size = new Size(160, 28) };
            reset.Click += (_, _) => SetValues(new DesktopCheatSettings()); Controls.Add(reset);
            _help.SetToolTip(_god, "Prevents normal health damage. Does not revive dead characters or remove hit reactions and scripted events.");
            _help.SetToolTip(_enemy, "Scales health loss for enemies, including environmental damage. Boss phase restrictions still apply.");
            _help.SetToolTip(_energy, "Keeps the player's MP meter full for movement abilities.");
            _help.SetToolTip(_speed, "Scales the character's ground and air movement speed, not simulation speed or attack animations.");
            _help.SetToolTip(_orbs, "Multiplies newly earned orbs. Existing saved orbs and shop prices are unchanged. Earnings remain in the save.");
            _help.SetToolTip(_ammoValue, "Temporary ammunition value while locked. Unlocking restores the underlying ammunition count.");
            _help.SetToolTip(_magicValue, "Temporary skull count while locked. This does not unlock story abilities.");
            _enabled.CheckedChanged += (_, _) => UpdateEnabled();
            _ammo.CheckedChanged += (_, _) => UpdateEnabled(); _magic.CheckedChanged += (_, _) => UpdateEnabled();
            var initial = DesktopCheatSettings.Load(); _saved = JsonSerializer.Serialize(initial); SetValues(initial);
        }

        private void AddCheck(CheckBox box, string text, int y)
        { box.Text = text; box.SetBounds(16, y, 295, 26); Controls.Add(box); }
        private void AddNumber(NumericUpDown box, int y, decimal min, decimal max, int places, decimal step)
        { box.SetBounds(326, y, 118, 26); box.Minimum = min; box.Maximum = max; box.DecimalPlaces = places; box.Increment = step; Controls.Add(box); }
        private void AddValue(string label, NumericUpDown box, int y, decimal min, decimal max, int places, decimal step)
        { Controls.Add(new Label { Text = label, Location = new Point(16, y + 4), AutoSize = true }); AddNumber(box, y, min, max, places, step); }
        private void UpdateEnabled()
        {
            foreach (Control control in new Control[] { _god, _ammo, _magic, _energy, _incoming, _enemy, _speed, _orbs }) control.Enabled = _enabled.Checked;
            _ammoValue.Enabled = _enabled.Checked && _ammo.Checked; _magicValue.Enabled = _enabled.Checked && _magic.Checked;
        }
        private void SetValues(DesktopCheatSettings value)
        {
            _enabled.Checked = value.Enabled; _god.Checked = value.GodMode; _ammo.Checked = value.InfiniteAmmo;
            _magic.Checked = value.InfiniteMagic; _energy.Checked = value.InfiniteEnergy;
            _ammoValue.Value = value.AmmoValue; _magicValue.Value = value.MagicValue;
            _incoming.Value = (decimal)value.PlayerDamageTaken; _enemy.Value = (decimal)value.EnemyDamageTaken;
            _speed.Value = (decimal)value.MovementSpeed; _orbs.Value = value.OrbMultiplier; UpdateEnabled();
        }
        internal DesktopCheatSettings Values => new() {
            Enabled = _enabled.Checked, GodMode = _god.Checked, InfiniteAmmo = _ammo.Checked,
            InfiniteMagic = _magic.Checked, InfiniteEnergy = _energy.Checked,
            AmmoValue = (int)_ammoValue.Value, MagicValue = (int)_magicValue.Value,
            PlayerDamageTaken = (double)_incoming.Value, EnemyDamageTaken = (double)_enemy.Value,
            MovementSpeed = (double)_speed.Value, OrbMultiplier = (int)_orbs.Value
        };
        internal void SaveChoices()
        {
            var value = Values; value.Validate(); LastBackup = null;
            string json = JsonSerializer.Serialize(value);
            // Never activate changed cheats after a failed backup.
            if (value.Enabled && json != _saved) LastBackup = DesktopSaveBackup.Create();
            value.Save(); _saved = json;
        }
        protected override void Dispose(bool disposing) { if (disposing) _help.Dispose(); base.Dispose(disposing); }
    }
}
