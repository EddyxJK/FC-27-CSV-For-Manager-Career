using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Collections.Generic;
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
        string python = Path.Combine(root, "runtime", "python", "python.exe");
        string script = Path.Combine(root, "parser", "parse-fc27-career.mjs");
        string extractor = Path.Combine(root, "parser", "extract-fc27-databases.py");
        if (File.Exists(node) && File.Exists(python) && File.Exists(script) && File.Exists(extractor)) return;

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
        "FC27 Carrera CSV", "2.2");
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
            Text = "Elige el guardado de FC27. La primera vez prepararé los nombres desde el juego;\nel CSV se guardará junto al archivo sin modificarlo.",
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
                string playerNames = Path.Combine(AssetRoot, "parser", "playernames-fc27.json");
                string careerNames = Path.Combine(AssetRoot, "parser", "career-names-fc27.json");
                if (!File.Exists(playerNames) || !File.Exists(careerNames))
                {
                    string gameDirectory = FindGameDirectory();
                    if (String.IsNullOrWhiteSpace(gameDirectory))
                    {
                        using (var folder = new FolderBrowserDialog())
                        {
                            folder.Description = "Selecciona la carpeta donde está instalado EA SPORTS FC 27.";
                            folder.ShowNewFolderButton = false;
                            if (folder.ShowDialog(this) != DialogResult.OK) return;
                            gameDirectory = folder.SelectedPath;
                        }
                    }
                    status.Text = "Preparando nombres de FC27; esto solo se hace la primera vez…";
                    string selectedGame = gameDirectory;
                    await Task.Run(() => PrepareGameNames(selectedGame));
                }
                status.Text = "Leyendo el guardado y creando el CSV…";
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
        string playerNames = Path.Combine(AssetRoot, "parser", "playernames-fc27.json");
        string careerNames = Path.Combine(AssetRoot, "parser", "career-names-fc27.json");
        if (!File.Exists(playerNames) || !File.Exists(careerNames))
            await Task.Run(() => PrepareGameNames(FindGameDirectory()));

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

    private static string FindGameDirectory()
    {
        string configured = Environment.GetEnvironmentVariable("FC27_GAME_DIR");
        var candidates = new List<string>();
        if (!String.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
        candidates.AddRange(new[] {
            @"C:\Program Files (x86)\Steam\steamapps\common\EA SPORTS FC 27",
            @"C:\Program Files\EA Games\EA SPORTS FC 27",
            @"C:\Program Files (x86)\EA Games\EA SPORTS FC 27"
        });
        for (char drive = 'D'; drive <= 'Z'; drive++)
        {
            candidates.Add(drive + @":\SteamLibrary\steamapps\common\EA SPORTS FC 27");
            candidates.Add(drive + @":\Program Files (x86)\Steam\steamapps\common\EA SPORTS FC 27");
            candidates.Add(drive + @":\Program Files\EA Games\EA SPORTS FC 27");
        }
        foreach (string candidate in candidates)
        {
            string bundle = Path.Combine(candidate, "Data", "Win32", "superbundlelayout", "installpackage_00");
            if (Directory.Exists(bundle) && Directory.GetFiles(bundle, "cas_*.cas").Length > 0) return candidate;
        }
        return null;
    }

    private static void PrepareGameNames(string gameDirectory)
    {
        if (String.IsNullOrWhiteSpace(gameDirectory))
            throw new InvalidOperationException("No encontré FC27 instalado. Selecciona la carpeta del juego e inténtalo de nuevo.");

        string root = AssetRoot;
        string parserDir = Path.Combine(root, "parser");
        string python = Path.Combine(root, "runtime", "python", "python.exe");
        string node = Path.Combine(root, "runtime", "node.exe");
        string extractor = Path.Combine(parserDir, "extract-fc27-databases.py");
        string nameBuilder = Path.Combine(parserDir, "build-career-names.mjs");
        string namesPath = Path.Combine(parserDir, "playernames-fc27.json");
        string careerNamesPath = Path.Combine(parserDir, "career-names-fc27.json");
        string gameDbDir = Path.Combine(root, "game-db");
        Directory.CreateDirectory(gameDbDir);

        string[] casFiles = { "cas_08.cas", "cas_01.cas", "cas_02.cas", "cas_03.cas", "cas_04.cas", "cas_05.cas", "cas_06.cas", "cas_07.cas", "cas_09.cas", "cas_10.cas" };
        string lastError = "";
        foreach (string cas in casFiles)
        {
            string casPath = Path.Combine(gameDirectory, "Data", "Win32", "superbundlelayout", "installpackage_00", cas);
            if (!File.Exists(casPath)) continue;
            ProcessResult extracted = RunChild(python, Quote(extractor) + " --game-dir " + Quote(gameDirectory) +
                " --cas " + Quote(cas) + " --out " + Quote(gameDbDir), parserDir);
            if (extracted.ExitCode != 0) { lastError = extracted.Error; continue; }

            foreach (string db in Directory.GetFiles(gameDbDir, "*.fifa_ng_db"))
            {
                string args = Quote(nameBuilder) + " --game-db " + Quote(db) + " --out " + Quote(careerNamesPath) +
                    " --names-out " + Quote(namesPath);
                ProcessResult built = RunChild(node, args, parserDir);
                if (built.ExitCode == 0 && File.Exists(namesPath) && File.Exists(careerNamesPath)) return;
                lastError = built.Error;
            }
        }
        throw new InvalidOperationException("No pude preparar los nombres desde los archivos de FC27. " +
            "Comprueba que seleccionaste la carpeta correcta del juego. " + lastError);
    }

    private static ProcessResult RunChild(string executable, string arguments, string workingDirectory)
    {
        var start = new ProcessStartInfo {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
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
            process.WaitForExit();
            return new ProcessResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
        }
    }

    private sealed class ProcessResult
    {
        internal readonly int ExitCode;
        internal readonly string Output;
        internal readonly string Error;
        internal ProcessResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
