using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

internal static class GeoAutoEngineBundleBuilder
{
    private const string PayloadResourceName = "GeoAutoEngine.builder.payload.zip";
    private const string BundleName = "GeoAutoEngine.bundle";
    private static readonly string[] RequiredAutodeskAssemblies =
    {
        "AcCoreMgd.dll",
        "AcDbMgd.dll",
        "AcMgd.dll",
        "AcWindows.dll",
        "AdWindows.dll",
    };

    private static int Main(string[] args)
    {
        Console.Title = "GeoAutoEngine - Gerador de Bundle";
        Console.WriteLine("GeoAutoEngine - Gerador de Bundle");
        Console.WriteLine("--------------------------------");
        Console.WriteLine();

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string logPath = Path.Combine(desktop, "GeoAutoEngine_BundleBuilder.log");

        try
        {
            File.WriteAllText(logPath, "GeoAutoEngine Bundle Builder" + Environment.NewLine);
            List<CadInstallation> installations = DetectCompatibleInstallations();
            CadInstallation selected = SelectInstallation(installations, args);
            if (selected == null)
            {
                Console.WriteLine("Operação cancelada.");
                Pause();
                return 1;
            }

            Console.WriteLine();
            Console.WriteLine("Versão selecionada: " + selected.Version);
            Console.WriteLine("Referências Autodesk: " + selected.Directory);
            Console.WriteLine();
            Console.WriteLine("Preparando ambiente de compilação...");

            string temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "GeoAutoEngine-bundle-builder-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);

            try
            {
                ExtractPayload(temporaryRoot);

                string dotnet = Path.Combine(temporaryRoot, ".dotnet", "dotnet.exe");
                string pluginProject = Path.Combine(
                    temporaryRoot,
                    "src",
                    "GeoAutoEngine.Plugin",
                    "GeoAutoEngine.Plugin.csproj");
                string temporaryBundle = Path.Combine(
                    temporaryRoot,
                    "generated",
                    BundleName);

                RequireFile(dotnet, "O .NET portátil não foi encontrado dentro do gerador.");
                RequireFile(pluginProject, "O projeto GeoAutoEngine.Plugin não foi encontrado dentro do gerador.");

                string propertyArguments =
                    " -p:AutodeskAutoCADVersion=" + selected.Version +
                    " " + Quote("-p:AutodeskAutoCAD" + selected.Version + "Directory=" + selected.Directory) +
                    " " + Quote("-p:BundleRoot=" + temporaryBundle);

                Console.WriteLine("Restaurando dependências...");
                RunDotNet(
                    dotnet,
                    "restore " + Quote(pluginProject) + " --nologo" + propertyArguments,
                    temporaryRoot,
                    logPath);

                Console.WriteLine();
                Console.WriteLine("Compilando GeoAutoEngine " + selected.Version + "...");
                RunDotNet(
                    dotnet,
                    "build " + Quote(pluginProject) +
                    " --configuration Release --no-restore --nologo --no-incremental" +
                    propertyArguments,
                    temporaryRoot,
                    logPath);

                ValidateBundle(temporaryBundle, selected.Version);

                string outputBundle = Path.Combine(desktop, BundleName);
                if (Directory.Exists(outputBundle))
                {
                    string backup = outputBundle + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    Directory.Move(outputBundle, backup);
                    Console.WriteLine();
                    Console.WriteLine("Bundle anterior movido para:");
                    Console.WriteLine(backup);
                }

                CopyDirectory(temporaryBundle, outputBundle);

                Console.WriteLine();
                Console.WriteLine("SUCESSO.");
                Console.WriteLine();
                Console.WriteLine("O bundle foi criado em:");
                Console.WriteLine(outputBundle);
                Console.WriteLine();
                Console.WriteLine("Agora basta copiar a pasta GeoAutoEngine.bundle para:");
                Console.WriteLine("%AppData%\\Autodesk\\ApplicationPlugins\\");
                File.AppendAllText(
                    logPath,
                    Environment.NewLine + "SUCESSO: " + outputBundle + Environment.NewLine);
            }
            finally
            {
                TryDeleteDirectory(temporaryRoot);
            }

            Pause();
            return 0;
        }
        catch (Exception exception)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("FALHA:");
            Console.WriteLine(exception.Message);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Log:");
            Console.WriteLine(logPath);

            try
            {
                File.AppendAllText(
                    logPath,
                    Environment.NewLine + "FALHA:" + Environment.NewLine + exception + Environment.NewLine);
            }
            catch
            {
            }

            Pause();
            return 1;
        }
    }

    private static List<CadInstallation> DetectCompatibleInstallations()
    {
        List<CadInstallation> result = new List<CadInstallation>();
        string[][] series =
        {
            new[] { "R24.3", "2024" },
            new[] { "R25.0", "2025" },
            new[] { "R25.1", "2026" },
        };

        try
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey autoCad = baseKey.OpenSubKey(@"SOFTWARE\Autodesk\AutoCAD"))
            {
                if (autoCad != null)
                {
                    foreach (string[] item in series)
                    {
                        using (RegistryKey release = autoCad.OpenSubKey(item[0]))
                        {
                            if (release == null)
                            {
                                continue;
                            }

                            foreach (string productKeyName in release.GetSubKeyNames())
                            {
                                using (RegistryKey product = release.OpenSubKey(productKeyName))
                                {
                                    string installPath = Convert.ToString(
                                        product == null ? null : product.GetValue("ACADLOCATION"));

                                    AddInstallationIfValid(result, item[1], installPath);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // A detecção por pasta abaixo continua disponível.
        }

        foreach (string version in new[] { "2024", "2025", "2026" })
        {
            string defaultDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Autodesk",
                "AutoCAD " + version);
            AddInstallationIfValid(result, version, defaultDirectory);
        }

        return result
            .GroupBy(
                item => item.Version + "|" + Path.GetFullPath(item.Directory),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Version, StringComparer.Ordinal)
            .ToList();
    }

    private static void AddInstallationIfValid(
        ICollection<CadInstallation> installations,
        string version,
        string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        foreach (string assembly in RequiredAutodeskAssemblies)
        {
            if (!File.Exists(Path.Combine(directory, assembly)))
            {
                return;
            }
        }

        installations.Add(new CadInstallation(version, directory));
    }

    private static CadInstallation SelectInstallation(
        List<CadInstallation> installations,
        string[] args)
    {
        if (installations.Count == 0)
        {
            throw new InvalidOperationException(
                "Não foram encontradas referências compatíveis do AutoCAD/Civil 3D 2024, 2025 ou 2026 neste computador.");
        }

        string requestedVersion = ArgumentAfter(args, "/version");
        if (!string.IsNullOrWhiteSpace(requestedVersion))
        {
            CadInstallation requested = installations.FirstOrDefault(
                item => string.Equals(item.Version, requestedVersion, StringComparison.Ordinal));
            if (requested == null)
            {
                throw new InvalidOperationException(
                    "A versão " + requestedVersion + " foi solicitada, mas as DLLs Autodesk dessa versão não foram encontradas.");
            }

            return requested;
        }

        if (installations.Count == 1)
        {
            return installations[0];
        }

        Console.WriteLine("Foram encontradas várias versões compatíveis:");
        Console.WriteLine();
        for (int index = 0; index < installations.Count; index++)
        {
            Console.WriteLine(
                (index + 1) + " - AutoCAD/Civil 3D " + installations[index].Version +
                " (" + installations[index].Directory + ")");
        }

        Console.WriteLine();
        Console.Write("Digite o número da versão que deseja compilar: ");
        string input = Console.ReadLine();
        int selectedIndex;
        if (!int.TryParse(input, out selectedIndex) ||
            selectedIndex < 1 ||
            selectedIndex > installations.Count)
        {
            throw new InvalidOperationException("Seleção inválida.");
        }

        return installations[selectedIndex - 1];
    }

    private static void ExtractPayload(string destination)
    {
        string archivePath = Path.Combine(destination, "payload.zip");
        using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName))
        {
            if (source == null)
            {
                throw new InvalidOperationException(
                    "O ambiente de compilação embutido não foi encontrado no executável.");
            }

            using (FileStream output = File.Create(archivePath))
            {
                source.CopyTo(output);
            }
        }

        ZipFile.ExtractToDirectory(archivePath, destination);
        File.Delete(archivePath);
    }

    private static void RunDotNet(
        string dotnet,
        string arguments,
        string workingDirectory,
        string logPath)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = dotnet;
        startInfo.Arguments = arguments;
        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.EnvironmentVariables["DOTNET_CLI_HOME"] = Path.Combine(workingDirectory, ".dotnet_cli_home");
        startInfo.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.EnvironmentVariables["DOTNET_NOLOGO"] = "1";

        using (Process process = new Process())
        {
            process.StartInfo = startInfo;
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs eventArgs)
            {
                if (eventArgs.Data != null)
                {
                    Console.WriteLine(eventArgs.Data);
                    AppendLog(logPath, eventArgs.Data);
                }
            };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs eventArgs)
            {
                if (eventArgs.Data != null)
                {
                    Console.WriteLine(eventArgs.Data);
                    AppendLog(logPath, eventArgs.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "A compilação falhou com o código " + process.ExitCode +
                    ". Consulte o arquivo GeoAutoEngine_BundleBuilder.log na Área de Trabalho.");
            }
        }
    }

    private static void ValidateBundle(string bundlePath, string version)
    {
        RequireFile(
            Path.Combine(bundlePath, "PackageContents.xml"),
            "O PackageContents.xml não foi gerado.");
        RequireFile(
            Path.Combine(bundlePath, "Contents", version, "GeoAutoEngine.Plugin.dll"),
            "A DLL do GeoAutoEngine " + version + " não foi gerada.");
        RequireFile(
            Path.Combine(bundlePath, "Contents", "Assets", "Templates", "folha_padrao.dwt"),
            "O template folha_padrao.dwt não foi incluído no bundle.");
    }

    private static void RequireFile(string path, string message)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(message, path);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }

        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }

    private static void AppendLog(string logPath, string text)
    {
        try
        {
            File.AppendAllText(logPath, text + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static string ArgumentAfter(string[] args, string name)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.WriteLine("Pressione ENTER para fechar.");
        Console.ReadLine();
    }

    private sealed class CadInstallation
    {
        public CadInstallation(string version, string directory)
        {
            Version = version;
            Directory = directory;
        }

        public string Version { get; private set; }

        public string Directory { get; private set; }
    }
}
