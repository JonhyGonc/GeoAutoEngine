using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;

internal static class GeoAutoEngineInstaller
{
    private const string BundleResourceName = "GeoAutoEngine.bundle.zip";
    private const string BundleName = "GeoAutoEngine.bundle";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GeoAutoEngine3";
    private const int MoveFileDelayUntilReboot = 0x4;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);

    [STAThread]
    private static int Main(string[] args)
    {
        var quiet = HasArgument(args, "/quiet");
        try
        {
            if (HasArgument(args, "/uninstall-run"))
            {
                return Uninstall(quiet, ArgumentAfter(args, "/uninstall-run"));
            }

            if (HasArgument(args, "/uninstall"))
            {
                return StartDetachedUninstall(quiet);
            }

            return Install(quiet);
        }
        catch (Exception exception)
        {
            ShowMessage(quiet, exception.Message, "Falha no GeoAutoEngine 3", MessageBoxIcon.Error);
            return 1;
        }
    }

    private static int Install(bool quiet)
    {
        EnsureCadClosed();
        var detectedVersions = DetectCivil3DVersions();
        if (detectedVersions.Count == 0)
        {
            throw new InvalidOperationException(
                "Nenhuma instalação compatível do Civil 3D 2024, 2025 ou 2026 foi encontrada.");
        }

        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "GeoAutoEngine-install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var archivePath = Path.Combine(temporaryDirectory, BundleResourceName);
            ExtractEmbeddedBundle(archivePath);
            ZipFile.ExtractToDirectory(archivePath, temporaryDirectory);
            var bundleSource = Path.Combine(temporaryDirectory, BundleName);
            if (!Directory.Exists(bundleSource))
            {
                bundleSource = temporaryDirectory;
            }

            ValidateBundle(bundleSource);
            var bundleInstall = InstalledBundlePath();
            Directory.CreateDirectory(Path.GetDirectoryName(bundleInstall));
            var backupPath = Path.Combine(temporaryDirectory, "previous.bundle");
            if (Directory.Exists(bundleInstall))
            {
                Directory.Move(bundleInstall, backupPath);
            }

            try
            {
                CopyDirectory(bundleSource, bundleInstall);
                ValidateBundle(bundleInstall);
            }
            catch
            {
                DeleteDirectory(bundleInstall);
                if (Directory.Exists(backupPath))
                {
                    Directory.Move(backupPath, bundleInstall);
                }

                throw;
            }

            var installedSetup = InstallSetupCopy();
            var version = ReadVersion(bundleInstall);
            RegisterUninstall(version, installedSetup);
            ShowMessage(
                quiet,
                "GeoAutoEngine " + version + " instalado com sucesso.\n\nCivil 3D detectado: " +
                string.Join(", ", detectedVersions.ToArray()) +
                "\n\nO GeoAutoEngine será carregado automaticamente ao abrir o Civil 3D.",
                "GeoAutoEngine 3",
                MessageBoxIcon.Information);
            return 0;
        }
        finally
        {
            DeleteDirectory(temporaryDirectory);
        }
    }

    private static int StartDetachedUninstall(bool quiet)
    {
        EnsureCadClosed();
        var temporarySetup = Path.Combine(Path.GetTempPath(), "GeoAutoEngine3-uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(Assembly.GetExecutingAssembly().Location, temporarySetup, true);
        var startInfo = new ProcessStartInfo
        {
            FileName = temporarySetup,
            Arguments = "/uninstall-run \"" + Assembly.GetExecutingAssembly().Location + "\"" + (quiet ? " /quiet" : string.Empty),
            UseShellExecute = false,
        };
        Process.Start(startInfo);
        return 0;
    }

    private static int Uninstall(bool quiet, string installedSetup)
    {
        EnsureCadClosed();
        DeleteDirectory(InstalledBundlePath());
        using (var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
        {
            if (key != null)
            {
                key.DeleteSubKeyTree("GeoAutoEngine3", false);
            }
        }

        if (!string.IsNullOrWhiteSpace(installedSetup))
        {
            var installerDirectory = Path.GetDirectoryName(installedSetup);
            if (File.Exists(installedSetup))
            {
                File.Delete(installedSetup);
            }

            if (!string.IsNullOrWhiteSpace(installerDirectory) && Directory.Exists(installerDirectory) &&
                !Directory.EnumerateFileSystemEntries(installerDirectory).Any())
            {
                Directory.Delete(installerDirectory);
            }
        }

        MoveFileEx(Assembly.GetExecutingAssembly().Location, null, MoveFileDelayUntilReboot);
        ShowMessage(quiet, "GeoAutoEngine 3 desinstalado com sucesso.", "GeoAutoEngine 3", MessageBoxIcon.Information);
        return 0;
    }

    private static List<string> DetectCivil3DVersions()
    {
        var result = new List<string>();
        var series = new[]
        {
            new[] { "R24.3", "2024" },
            new[] { "R25.0", "2025" },
            new[] { "R25.1", "2026" },
        };

        using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
        using (var autoCad = baseKey.OpenSubKey(@"SOFTWARE\Autodesk\AutoCAD"))
        {
            if (autoCad == null)
            {
                return result;
            }

            foreach (var item in series)
            {
                using (var release = autoCad.OpenSubKey(item[0]))
                {
                    if (release == null)
                    {
                        continue;
                    }

                    foreach (var productKeyName in release.GetSubKeyNames())
                    {
                        using (var product = release.OpenSubKey(productKeyName))
                        {
                            var productName = Convert.ToString(product == null ? null : product.GetValue("ProductName"));
                            var installPath = Convert.ToString(product == null ? null : product.GetValue("ACADLOCATION"));
                            if (!string.IsNullOrWhiteSpace(productName) &&
                                productName.IndexOf("Civil 3D", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                !string.IsNullOrWhiteSpace(installPath) &&
                                File.Exists(Path.Combine(installPath, "acad.exe")))
                            {
                                result.Add(item[1]);
                                break;
                            }
                        }
                    }
                }
            }
        }

        return result;
    }

    private static void EnsureCadClosed()
    {
        if (Process.GetProcessesByName("acad").Length > 0)
        {
            throw new InvalidOperationException("Feche o AutoCAD e o Civil 3D antes de continuar.");
        }
    }

    private static string InstalledBundlePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk",
            "ApplicationPlugins",
            BundleName);
    }

    private static string InstallSetupCopy()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GeoAutoEngine3",
            "Installer");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "GeoAutoEngine3_Setup.exe");
        var source = Assembly.GetExecutingAssembly().Location;
        if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(source, destination, true);
        }

        return destination;
    }

    private static void RegisterUninstall(string version, string installedSetup)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath))
        {
            key.SetValue("DisplayName", "GeoAutoEngine 3");
            key.SetValue("DisplayVersion", version);
            key.SetValue("Publisher", "GeoAutoEngine");
            key.SetValue("InstallLocation", InstalledBundlePath());
            key.SetValue("DisplayIcon", installedSetup);
            key.SetValue("UninstallString", "\"" + installedSetup + "\" /uninstall");
            key.SetValue("QuietUninstallString", "\"" + installedSetup + "\" /uninstall /quiet");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 0, RegistryValueKind.DWord);
        }
    }

    private static void ExtractEmbeddedBundle(string destination)
    {
        using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(BundleResourceName))
        {
            if (source == null)
            {
                throw new InvalidOperationException("O bundle incorporado não foi encontrado no instalador.");
            }

            using (var output = File.Create(destination))
            {
                source.CopyTo(output);
            }
        }
    }

    private static void ValidateBundle(string bundlePath)
    {
        var requiredFiles = new[]
        {
            Path.Combine(bundlePath, "PackageContents.xml"),
            Path.Combine(bundlePath, "Contents", "2024", "GeoAutoEngine.Plugin.dll"),
            Path.Combine(bundlePath, "Contents", "2025", "GeoAutoEngine.Plugin.dll"),
            Path.Combine(bundlePath, "Contents", "2026", "GeoAutoEngine.Plugin.dll"),
            Path.Combine(bundlePath, "Contents", "Assets", "Templates", "folha_padrao.dwt"),
        };

        foreach (var requiredFile in requiredFiles)
        {
            if (!File.Exists(requiredFile))
            {
                throw new InvalidOperationException("O pacote não contém o arquivo obrigatório: " + requiredFile);
            }
        }
    }

    private static string ReadVersion(string bundlePath)
    {
        var document = new XmlDocument();
        document.Load(Path.Combine(bundlePath, "PackageContents.xml"));
        var root = document.DocumentElement;
        return root == null ? "3" : root.GetAttribute("AppVersion");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static void DeleteDirectory(string path)
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
            // A operação principal já terminou; o Windows pode limpar temporários depois.
        }
    }

    private static bool HasArgument(IEnumerable<string> args, string value)
    {
        return args.Any(argument => string.Equals(argument, value, StringComparison.OrdinalIgnoreCase));
    }

    private static string ArgumentAfter(string[] args, string value)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], value, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static void ShowMessage(bool quiet, string message, string title, MessageBoxIcon icon)
    {
        if (!quiet)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
        }
    }
}
