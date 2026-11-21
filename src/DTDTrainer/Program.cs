namespace DTDTrainer;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].StartsWith("--render"))
        {
            ApplicationConfiguration.Initialize();
            using var form = new MainForm(renderOnly: true);
            // --render-cat=<Nhóm>: chụp một nhóm mod, vd. --render-cat=Game
            var cat = args.FirstOrDefault(a => a.StartsWith("--render-cat="));
            if (cat != null) form.SelectCategoryForRender(cat["--render-cat=".Length..]);

            if (args[0] == "--render-settings")
            {
                form.ShowSettingsForRender();
            }
            else if (args[0] == "--render-active")
            {
                form.SimulateActiveCheatsForRender();
            }
            form.Show();
            Application.DoEvents();
            if (args[0] == "--render-hover")
            {
                form.SimulateHoverForRender("UnlimitedOxygen");
            }
            form.Refresh();
            using var bmp = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
            string outPath = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--")) ?? "render.png";
            bmp.Save(outPath);
            return;
        }

        L.English = TrainerSettings.Load().Language == "en";

        using var single = new Mutex(true, "DTDTrainer.SingleInstance", out bool first);
        if (!first)
        {
            MessageBox.Show(L.T("DTD Trainer đang chạy rồi.", "DTD Trainer is already running."), "DTD Trainer",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
