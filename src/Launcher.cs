using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Launcher
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--batch")
        {
            Environment.ExitCode = RunBatch(args[1], args[2]);
            return;
        }

        try
        {
            EnsureAssets();
        }
        catch (Exception ex)
        {
            MessageBox.Show("No pude preparar los componentes de la aplicación.\n\n" + ex.Message,
                "FC27 Carrera CSV", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainWindow());
    }

    private static int RunBatch(string save, string output)
    {
        try
        {
            EnsureAssets();
            string message = MainWindow.RunParser(save, output).GetAwaiter().GetResult();
            Console.WriteLine(message);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void EnsureAssets()
    {
        string root = MainWindow.AssetRoot;
        string node = Path.Combine(root, "runtime", "node.exe");
        string script = Path.Combine(root, "parser", "parse-fc27-career.mjs");
        if (File.Exists(node) && File.Exists(script)) return;

        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.CreateDirectory(root);
        string archive = Path.Combine(Path.GetTempPath(), "fc27-carrera-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("FC27CarreraCsv.Payload.zip"))
            {
                if (resource == null) throw new InvalidOperationException("No encontré los componentes incluidos en el ejecutable.");
                using (var file = File.Create(archive)) resource.CopyTo(file);
            }
            ZipFile.ExtractToDirectory(archive, root);
        }
        finally
        {
            if (File.Exists(archive)) File.Delete(archive);
        }
    }
}

internal sealed class MainWindow : Form
{
    internal static readonly string AssetRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FC27 Carrera CSV", "1.1");
    private readonly Label status = new Label();
    private readonly Label selected = new Label();
    private readonly Button choose = new Button();
    private readonly Button openFolder = new Button();
    private string lastOutput;

    public MainWindow()
    {
        Text = "FC27 Carrera → CSV";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 285);
        MinimumSize = new Size(620, 285);
        MaximumSize = new Size(620, 285);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var title = new Label {
            Text = "Exportar jugadores de tu carrera de mánager",
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(28, 25)
        };
        var help = new Label {
            Text = "Elige el archivo de guardado de FC27. Crearé un CSV en la misma carpeta,\nsin cambiar el archivo original.",
            Font = new Font("Segoe UI", 10),
            AutoSize = true,
            Location = new Point(30, 76)
        };
        selected.Text = "Todavía no has elegido un archivo.";
        selected.Font = new Font("Segoe UI", 9);
        selected.AutoEllipsis = true;
        selected.Location = new Point(31, 132);
        selected.Size = new Size(555, 34);

        choose.Text = "Elegir guardado y exportar CSV…";
        choose.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        choose.Size = new Size(270, 42);
        choose.Location = new Point(30, 179);
        choose.Click += async (sender, e) => await ChooseAndExport();

        openFolder.Text = "Abrir carpeta del CSV";
        openFolder.Size = new Size(175, 42);
        openFolder.Location = new Point(315, 179);
        openFolder.Enabled = false;
        openFolder.Click += (sender, e) => {
            if (!String.IsNullOrEmpty(lastOutput) && File.Exists(lastOutput))
                Process.Start("explorer.exe", "/select,\"" + lastOutput + "\"");
        };

        status.Text = "Listo.";
        status.Font = new Font("Segoe UI", 9);
        status.Location = new Point(31, 237);
        status.Size = new Size(555, 24);

        Controls.Add(title);
        Controls.Add(help);
        Controls.Add(selected);
        Controls.Add(choose);
        Controls.Add(openFolder);
        Controls.Add(status);
    }

    private async Task ChooseAndExport()
    {
        using (var dialog = new OpenFileDialog())
        {
            dialog.Title = "Selecciona tu guardado de Carrera de mánager";
            dialog.Filter = "Guardado de FC27 (CmMgrC*)|CmMgrC*|Todos los archivos (*.*)|*.*";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            string save = dialog.FileName;
            string output = Path.Combine(Path.GetDirectoryName(save),
                Path.GetFileNameWithoutExtension(save) + "_jugadores.csv");
            selected.Text = save;

            if (File.Exists(output) && MessageBox.Show(this,
                "Ya existe un CSV con ese nombre. ¿Quieres reemplazarlo?",
                "Confirmar reemplazo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                status.Text = "No se hizo ningún cambio.";
                return;
            }

            choose.Enabled = false;
            openFolder.Enabled = false;
            status.Text = "Leyendo el guardado y creando el CSV…";
            try
            {
                string details = await RunParser(save, output);
                lastOutput = output;
                openFolder.Enabled = true;
                status.Text = details;
                MessageBox.Show(this, details + "\n\nEl archivo se guardó aquí:\n" + output,
                    "Exportación terminada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                status.Text = "No se pudo crear el CSV.";
                MessageBox.Show(this, ex.Message, "Error al leer el guardado",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                choose.Enabled = true;
            }
        }
    }

    internal static async Task<string> RunParser(string save, string output)
    {
        string app = AssetRoot;
        string node = Path.Combine(app, "runtime", "node.exe");
        string parserDir = Path.Combine(app, "parser");
        string script = Path.Combine(parserDir, "parse-fc27-career.mjs");
        if (!File.Exists(node) || !File.Exists(script))
            throw new FileNotFoundException("Faltan archivos de la aplicación. Vuelve a extraer la carpeta completa del programa.");

        var start = new ProcessStartInfo {
            FileName = node,
            Arguments = Quote(script) + " --save " + Quote(save) + " --out " + Quote(output),
            WorkingDirectory = parserDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using (var process = new Process())
        {
            process.StartInfo = start;
            process.Start();
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            await Task.Run(() => process.WaitForExit());
            string message = (await stdout).Trim();
            string error = (await stderr).Trim();
            if (process.ExitCode != 0)
                throw new InvalidOperationException(String.IsNullOrWhiteSpace(error) ? message : error);
            return String.IsNullOrWhiteSpace(message) ? "CSV creado correctamente." : message;
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
