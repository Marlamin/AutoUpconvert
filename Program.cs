using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace AutoUpconvert
{
    internal class Program
    {
        private static bool isBusy = false;
        private static string monitorDir;
        private static string frenchConverterPath;
        private static string mtxpConverterPath;
        private static string outputDir;
        private static string frenchConverterBaseDir;
        private static string mtxpConverterBaseDir;
        private static string epsilonDir;

        private static string replaceMapNameIn;
        private static string replaceMapNameOut;

        private static Dictionary<uint, string> Listfile = new();

        private static Process frenchConverter;
        private static Process mtxpConverter;

        private static FileSystemWatcher monitorDirWatcher;

        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("settings.json", true, true).Build();

            monitorDir = config["MonitorDir"];
            frenchConverterPath = config["FrenchConverterPath"];
            mtxpConverterPath = config["MTXPConverterPath"];
            outputDir = config["OutputDir"];
            replaceMapNameIn = config["ReplaceMapNameIn"];
            replaceMapNameOut = config["ReplaceMapNameOut"];
            epsilonDir = config["EpsilonDir"];

            if (!File.Exists("listfile.csv"))
            {
                Console.WriteLine("listfile.csv not found in current directory. Exiting.");
                return;
            }
            else
            {
                Console.Write("Loading listfile..");
                foreach (var line in File.ReadAllLines("listfile.csv"))
                {
                    var parts = line.Split(';');
                    Listfile[uint.Parse(parts[0])] = parts[1];
                }
                Console.WriteLine("..done!");

                if (File.Exists("custom-listfile.csv"))
                {

                   Console.Write("Loading custom listfile..");
                    foreach (var line in File.ReadAllLines("custom-listfile.csv"))
                    {
                        var parts = line.Split(';');
                        Console.WriteLine("\nAdded file from custom listfile: " + line);
                        Listfile[uint.Parse(parts[0])] = parts[1];
                    }
                    Console.WriteLine("..done!");
                }
            }

            if (!Directory.Exists(monitorDir))
            {
                Console.WriteLine("Input directory to monitor not found. Exiting.");
                return;
            }

            if (!File.Exists(frenchConverterPath))
            {
                Console.WriteLine("SLFiledataADTConverter not found. Exiting.");
                return;
            }

            frenchConverterBaseDir = Path.GetDirectoryName(frenchConverterPath);

            if (!File.Exists(mtxpConverterPath))
            {
                Console.WriteLine("7x_TexAdt_MTXP_Adder not found. Exiting.");
                return;
            }

            mtxpConverterBaseDir = Path.GetDirectoryName(mtxpConverterPath);

            if (!string.IsNullOrEmpty(epsilonDir) && !Directory.Exists(epsilonDir))
            {
                Console.WriteLine("Epsilon directory not found. Exiting.");
                return;
            }

            #region Listfile generation/copying
            Console.Write("Generating temp listfile and copying to SLFiledataADTConverter directory..");

            // Generate temp listfile with only the files french converter needs
            var frenchConverterExts = new string[] { ".blp", ".wmo", ".adt", ".wdt", ".wdl", ".m2" };
            File.WriteAllLines("temp-listfile.csv", Listfile.Where(x => frenchConverterExts.Contains(Path.GetExtension(x.Value))).Select(x => x.Key + ";" + x.Value));

            // Append Noggit error textures
            string[] noggitErrorTextures = { "100;error_0_s.blp", "101;error_1_s.blp", "102;error_2_s.blp", "103;error_3_s.blp", "104;error_4_s.blp" };
            File.AppendAllLines("temp-listfile.csv", noggitErrorTextures);

            // Copy listfile to SLFiledataADTConverter
            File.Copy("temp-listfile.csv", Path.Combine(frenchConverterBaseDir, "listfile", "listfile.csv"), true);

            // Delete temp listfile
            File.Delete("temp-listfile.csv");

            Console.WriteLine("..done!");

            Console.Write("Syncing tileset-only listfile to MTXP Adder...");
            File.Delete(Path.Combine(mtxpConverterBaseDir, "listfile.csv"));
            File.WriteAllLines(Path.Combine(mtxpConverterBaseDir, "listfile.csv"), Listfile.Where(x => x.Value.StartsWith("tileset/")).Select(x => x.Key + ";" + x.Value));
            Console.WriteLine("..done!");

            if (!string.IsNullOrEmpty(epsilonDir))
            {
                Console.Write("Generating temp listfile and copying to Epsilon..");
                var epsilonExtBlacklist = new string[] { ".unk", ".pd4", ".pm4", ".meta", ".dat", ".col" };
                var epsilonListfilePath = Path.Combine(epsilonDir, "_retail_", "Tools", "listfile.csv");
                File.Delete(epsilonListfilePath);
                File.WriteAllLines(epsilonListfilePath, Listfile.Where(x => !epsilonExtBlacklist.Contains(Path.GetExtension(x.Value))).Select(x => x.Key + ";" + x.Value));
                Console.WriteLine("..done!");
            }
            #endregion

            Console.Write("Watching for changes in Noggit save directory (" + monitorDir + ")..");

            monitorDirWatcher = new FileSystemWatcher();
            monitorDirWatcher.Path = monitorDir;
            monitorDirWatcher.NotifyFilter = NotifyFilters.LastWrite;
            monitorDirWatcher.Filter = "*.adt";
            monitorDirWatcher.Changed += new FileSystemEventHandler(OnFileChanged);
            monitorDirWatcher.EnableRaisingEvents = true;

            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("File changed: " + e.FullPath);

            if (isBusy)
            {
                Console.WriteLine("Already processing a file. Skipping.");
                return;
            }

            try
            {
                monitorDirWatcher.EnableRaisingEvents = false;
                ProcessMap();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing map: " + ex.Message);
                monitorDirWatcher.EnableRaisingEvents = true;
            }

        }

        private static void ProcessMap()
        {
            isBusy = true;

            Console.WriteLine("Processing map update...");

            // Clear SLFiledataADTConverter dirs
            var frenchInputDir = Path.Combine(frenchConverterBaseDir, "INPUT");

            foreach (var file in Directory.GetFiles(frenchInputDir))
            {
                Console.WriteLine("Deleting " + file);
                File.Delete(file);
            }

            var frenchOutputDir = Path.Combine(frenchConverterBaseDir, "OUTPUT");
            foreach (var file in Directory.GetFiles(frenchOutputDir))
            {
                Console.WriteLine("Deleting " + file);
                File.Delete(file);
            }

            //  Copy all files to SLFiledataADTConverter\input
            foreach (var inputFile in Directory.GetFiles(monitorDir, "*.*"))
            {
                var outputFile = Path.Combine(frenchInputDir, Path.GetFileName(inputFile));

                if (replaceMapNameIn != "" && replaceMapNameOut != "")
                {
                    outputFile = Path.Combine(frenchInputDir, Path.GetFileName(inputFile).Replace(replaceMapNameIn, replaceMapNameOut));
                }

                File.Copy(inputFile, outputFile);
            }

            //  Run converter
            frenchConverter = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = frenchConverterPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = frenchConverterBaseDir
                }
            };

            frenchConverter.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            frenchConverter.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);

            frenchConverter.Start();
            frenchConverter.BeginOutputReadLine();
            frenchConverter.BeginErrorReadLine();
            frenchConverter.WaitForExit();

            //  Copy all output ADTs from SLFiledataADTConverter\OUTPUT to output folder (overwrite)
            foreach (var outputFile in Directory.GetFiles(Path.Combine(frenchConverterBaseDir, "OUTPUT"), "*.adt"))
            {
                var outputFileName = Path.GetFileName(outputFile);
                var outputFilePath = Path.Combine(outputDir, outputFileName);

                Console.WriteLine("Copying " + outputFileName + " to " + outputFilePath);
                File.Copy(outputFile, outputFilePath, true);
            }

            //  Clear 7x_TexAdt_MTXP_Adder dirs
            var mtxpInputDir = Path.Combine(mtxpConverterBaseDir, "Input");
            foreach (var file in Directory.GetFiles(mtxpInputDir))
            {
                Console.WriteLine("Deleting " + file);
                File.Delete(file);
            }

            var mtxpOutputDir = Path.Combine(mtxpConverterBaseDir, "Output");
            foreach (var file in Directory.GetFiles(mtxpOutputDir))
            {
                Console.WriteLine("Deleting " + file);
                File.Delete(file);
            }

            //  Copy tex0 ADTs from output folder to 7x_TexAdt_MTXP_Adder\Input
            foreach (var outputFile in Directory.GetFiles(outputDir, "*_tex0.adt"))
            {
                var outputFileName = Path.GetFileName(outputFile);
                var outputFilePath = Path.Combine(mtxpInputDir, outputFileName);

                Console.WriteLine("Copying " + outputFileName + " to " + outputFilePath);
                File.Copy(outputFile, outputFilePath);
            }

            mtxpConverter = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = mtxpConverterPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = mtxpConverterBaseDir
                }
            };

            mtxpConverter.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            mtxpConverter.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);

            mtxpConverter.Start();
            mtxpConverter.BeginOutputReadLine();
            mtxpConverter.BeginErrorReadLine();
            mtxpConverter.WaitForExit();

            //  Copy all output ADTs from 7x_TexAdt_MTXP_Adder to output folder
            foreach (var outputFile in Directory.GetFiles(mtxpOutputDir, "*.*"))
            {
                var outputFileName = Path.GetFileName(outputFile);
                var outputFilePath = Path.Combine(outputDir, outputFileName);

                Console.WriteLine("Copying " + outputFileName + " to " + outputFilePath);
                File.Copy(outputFile, outputFilePath, true);
            }

            // TODO: Update Epsilon patch JSONs where applicable

            isBusy = false;
            monitorDirWatcher.EnableRaisingEvents = true;

            Console.WriteLine("Done processing map!");
        }

        static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Console.WriteLine(outLine.Data);

            if (outLine.Data != null && outLine.Data.Contains("Press any key to exit the program"))
            {
                frenchConverter.Kill();
            }

            if (outLine.Data != null && outLine.Data.Contains("All done!"))
            {
                mtxpConverter.Kill();
            }
        }

        // patches.json
        private struct EpsilonPatchList
        {
            public string Name { get; set; }
            public bool IsIncluded { get; set; }
            public bool IsBanned { get; set; }
            public bool IsDev { get; set; }
            public bool IsIncorrectlyConfigured { get; set; }
            public string ErrorMessage { get; set; }
        }

        // patch.json
        private struct EpsilonPatchManifest
        {
            public string name { get; set; }
            public string version { get; set; }
            public string url { get; set; }
            public List<EpsilonPatchManifestFile> files { get; set; }
        }

        private struct EpsilonPatchManifestFile
        {
            public uint id { get; set; }
            public string file { get; set; }
        }
    }
}
