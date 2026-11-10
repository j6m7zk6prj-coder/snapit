namespace Snapit;

sealed class SettingsForm : Form
{
    public event Action Saved;

    readonly AppSettings settings;
    readonly CheckBox chkCopy, chkSave, chkPreview, chkSound, chkStartup, chkPrt;
    readonly TextBox txtFolder;

    int S(int v) => Ui.S(this, v);

    public SettingsForm(AppSettings settings)
    {
        this.settings = settings;

        Text = "Réglages de Snapit";
        Icon = TrayApp.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        AutoScaleMode = AutoScaleMode.None;
        Font = Ui.F(10f);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(0, 0, S(28), S(22));

        var flow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = new Point(S(28), S(18)),
            Margin = Padding.Empty,
        };
        Controls.Add(flow);

        Label Section(string t) => new Label
        {
            Text = t, AutoSize = true, ForeColor = Ui.Secondary, Font = Ui.F(9f, FontStyle.Bold),
            Margin = new Padding(0, S(16), 0, S(4)),
        };
        CheckBox Check(string t, bool v) => new CheckBox
        {
            Text = t, Checked = v, AutoSize = true, ForeColor = Ui.TextColor, Margin = new Padding(0, S(3), 0, S(3)),
        };

        flow.Controls.Add(new Label
        {
            Text = "Réglages", AutoSize = true, Font = Ui.F(15f, FontStyle.Bold), ForeColor = Ui.TextColor,
            Margin = new Padding(0, 0, 0, S(2)),
        });

        flow.Controls.Add(Section("APRÈS UNE CAPTURE"));
        flow.Controls.Add(chkCopy = Check("Copier dans le presse-papiers", settings.CopyToClipboard));
        flow.Controls.Add(chkSave = Check("Enregistrer l’image dans le dossier", settings.SaveToFile));
        flow.Controls.Add(chkPreview = Check("Afficher l’aperçu flottant (clic pour annoter)", settings.ShowPreview));
        flow.Controls.Add(chkSound = Check("Son d’obturateur", settings.PlaySound));

        flow.Controls.Add(Section("DOSSIER D’ENREGISTREMENT"));
        var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = Padding.Empty };
        txtFolder = new TextBox { Text = settings.SaveFolder, ReadOnly = true, Width = S(330), Margin = new Padding(0, S(2), S(8), 0), BackColor = Ui.Bg };
        var btnBrowse = new Button { Text = "Changer…", AutoSize = true, Margin = Padding.Empty };
        btnBrowse.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = txtFolder.Text, UseDescriptionForTitle = true, Description = "Dossier des captures" };
            if (dlg.ShowDialog(this) == DialogResult.OK) txtFolder.Text = dlg.SelectedPath;
        };
        row.Controls.Add(txtFolder);
        row.Controls.Add(btnBrowse);
        flow.Controls.Add(row);

        flow.Controls.Add(Section("GÉNÉRAL"));
        flow.Controls.Add(chkStartup = Check("Ouvrir Snapit au démarrage de Windows", AppSettings.StartWithWindows));
        flow.Controls.Add(chkPrt = Check("Utiliser la touche Impr. écran pour capturer une zone", settings.UsePrintScreen));

        flow.Controls.Add(Section("RACCOURCIS"));
        var keys = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = Padding.Empty };
        void Key(string k, string d)
        {
            keys.Controls.Add(new Label { Text = k, AutoSize = true, Font = Ui.F(9.5f, FontStyle.Bold), ForeColor = Ui.TextColor, Margin = new Padding(0, S(2), S(18), S(2)) });
            keys.Controls.Add(new Label { Text = d, AutoSize = true, ForeColor = Ui.Secondary, Margin = new Padding(0, S(2), 0, S(2)) });
        }
        Key("Ctrl + Maj + 3", "Écran entier");
        Key("Ctrl + Maj + 4", "Zone (glisser) ou fenêtre (clic)");
        Key("Ctrl + Maj + 5", "Panneau de capture (minuteur…)");
        Key("Impr. écran", "Zone ou fenêtre (si activé)");
        Key("Échap", "Annuler");
        flow.Controls.Add(keys);

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, S(20), 0, 0) };
        var ok = new Button { Text = "Enregistrer", AutoSize = true, Margin = new Padding(0, 0, S(8), 0), Padding = new Padding(S(10), S(2), S(10), S(2)) };
        var cancel = new Button { Text = "Annuler", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = Padding.Empty, Padding = new Padding(S(6), S(2), S(6), S(2)) };
        ok.Click += (s, e) => SaveAndClose();
        cancel.Click += (s, e) => Close();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        flow.Controls.Add(buttons);

        AcceptButton = ok;
        CancelButton = cancel;
    }

    void SaveAndClose()
    {
        settings.CopyToClipboard = chkCopy.Checked;
        settings.SaveToFile = chkSave.Checked;
        settings.ShowPreview = chkPreview.Checked;
        settings.PlaySound = chkSound.Checked;
        settings.UsePrintScreen = chkPrt.Checked;
        settings.SaveFolder = txtFolder.Text;
        settings.Save();
        if (chkStartup.Checked != AppSettings.StartWithWindows) AppSettings.StartWithWindows = chkStartup.Checked;
        Saved?.Invoke();
        Close();
    }
}
