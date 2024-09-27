using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;

namespace UABEAvalonia
{
    public class CommandLineHandler
    {
        public static void PrintHelp()
        {
            Console.WriteLine("UABE AVALONIA");
            Console.WriteLine("WARNING: Command line support VERY EARLY");
            Console.WriteLine("There is a high chance of stuff breaking");
            Console.WriteLine("Use at your own risk");
            Console.WriteLine("");
            Console.WriteLine("  UABEAvalonia batchexportbundle <directory>");
            Console.WriteLine("  UABEAvalonia batchimportbundle <directory>");
            Console.WriteLine("  UABEAvalonia applyemip <emip file> <directory>");
            Console.WriteLine("  UABEAvalonia exportdumps <bundle file> [-<containerName1> -<containerName2> ...]");
            Console.WriteLine("  UABEAvalonia importdumps <bundle file>");
            Console.WriteLine("");
            Console.WriteLine("Bundle import/export arguments:");
            Console.WriteLine("  -keepnames writes out to the exact file name in the bundle.");
            Console.WriteLine("      Normally, file names are prepended with the bundle's name.");
            Console.WriteLine("      Note: these names are not compatible with batchimport.");
            Console.WriteLine("  -kd keep .decomp files. When UABEA opens compressed bundles,");
            Console.WriteLine("      they are decompressed into .decomp files. If you want to");
            Console.WriteLine("      decompress bundles, you can use this flag to keep them");
            Console.WriteLine("      without deleting them.");
            Console.WriteLine("  -fd overwrite old .decomp files.");
            Console.WriteLine("  -md decompress into memory. Doesn't write .decomp files.");
            Console.WriteLine("      -kd and -fd won't do anything with this flag set.");
        }

        private static string GetMainFileName(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-"))
                    return args[i];
            }
            return string.Empty;
        }

        private static HashSet<string> GetFlags(string[] args)
        {
            HashSet<string> flags = new HashSet<string>();
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                    flags.Add(args[i]);
            }
            return flags;
        }

        private static AssetBundleFile DecompressBundle(string file, string? decompFile)
        {
            AssetBundleFile bun = new AssetBundleFile();

            Stream fs = File.OpenRead(file);
            AssetsFileReader r = new AssetsFileReader(fs);

            bun.Read(r);
            if (bun.Header.GetCompressionType() != 0)
            {
                Stream nfs;
                if (decompFile == null)
                    nfs = new MemoryStream();
                else
                    nfs = File.Open(decompFile, FileMode.Create, FileAccess.ReadWrite);

                AssetsFileWriter w = new AssetsFileWriter(nfs);
                bun.Unpack(w);

                nfs.Position = 0;
                fs.Close();

                fs = nfs;
                r = new AssetsFileReader(fs);

                bun = new AssetBundleFile();
                bun.Read(r);
            }

            return bun;
        }

        private static string GetNextBackup(string affectedFilePath)
        {
            for (int i = 0; i < 10000; i++)
            {
                string bakName = $"{affectedFilePath}.bak{i.ToString().PadLeft(4, '0')}";
                if (!File.Exists(bakName))
                {
                    return bakName;
                }
            }

            Console.WriteLine("Too many backups, exiting for your safety.");
            return null;
        }

        private static void BatchExportBundle(string[] args)
        {
            string exportDirectory = GetMainFileName(args);
            if (!Directory.Exists(exportDirectory))
            {
                Console.WriteLine("Directory does not exist!");
                return;
            }

            HashSet<string> flags = GetFlags(args);
            foreach (string file in Directory.EnumerateFiles(exportDirectory))
            {
                string decompFile = $"{file}.decomp";

                if (flags.Contains("-md"))
                    decompFile = null;

                if (!File.Exists(file))
                {
                    Console.WriteLine($"File {file} does not exist!");
                    return;
                }

                DetectedFileType fileType = FileTypeDetector.DetectFileType(file);
                if (fileType != DetectedFileType.BundleFile)
                {
                    continue;
                }

                Console.WriteLine($"Decompressing {file}...");
                AssetBundleFile bun = DecompressBundle(file, decompFile);

                int entryCount = bun.BlockAndDirInfo.DirectoryInfos.Length;
                for (int i = 0; i < entryCount; i++)
                {
                    string name = bun.BlockAndDirInfo.DirectoryInfos[i].Name;
                    byte[] data = BundleHelper.LoadAssetDataFromBundle(bun, i);
                    string outName;
                    if (flags.Contains("-keepnames"))
                        outName = Path.Combine(exportDirectory, name);
                    else
                        outName = Path.Combine(exportDirectory, $"{Path.GetFileName(file)}_{name}");
                    Console.WriteLine($"Exporting {outName}...");
                    File.WriteAllBytes(outName, data);
                }

                bun.Close();

                if (!flags.Contains("-kd") && !flags.Contains("-md") && File.Exists(decompFile))
                    File.Delete(decompFile);

                Console.WriteLine("Done.");
            }
        }

        private static void BatchImportBundle(string[] args)
        {
            string importDirectory = GetMainFileName(args);
            if (!Directory.Exists(importDirectory))
            {
                Console.WriteLine("Directory does not exist!");
                return;
            }

            HashSet<string> flags = GetFlags(args);
            foreach (string file in Directory.EnumerateFiles(importDirectory))
            {
                string decompFile = $"{file}.decomp";

                if (flags.Contains("-md"))
                    decompFile = null;

                if (!File.Exists(file))
                {
                    Console.WriteLine($"File {file} does not exist!");
                    return;
                }

                DetectedFileType fileType = FileTypeDetector.DetectFileType(file);
                if (fileType != DetectedFileType.BundleFile)
                {
                    continue;
                }

                Console.WriteLine($"Decompressing {file} to {decompFile}...");
                AssetBundleFile bun = DecompressBundle(file, decompFile);

                List<BundleReplacer> reps = new List<BundleReplacer>();
                List<Stream> streams = new List<Stream>();

                int entryCount = bun.BlockAndDirInfo.DirectoryInfos.Length;
                for (int i = 0; i < entryCount; i++)
                {
                    string name = bun.BlockAndDirInfo.DirectoryInfos[i].Name;
                    string matchName = Path.Combine(importDirectory, $"{Path.GetFileName(file)}_{name}");

                    if (File.Exists(matchName))
                    {
                        FileStream fs = File.OpenRead(matchName);
                        long length = fs.Length;
                        reps.Add(new BundleReplacerFromStream(name, name, true, fs, 0, length));
                        streams.Add(fs);
                        Console.WriteLine($"Importing {matchName}...");
                    }
                }

                //I guess uabe always writes to .decomp even if
                //the bundle is already decompressed, that way
                //here it can be used as a temporary file. for
                //now I'll write to memory since having a .decomp
                //file isn't guaranteed here
                byte[] data;
                using (MemoryStream ms = new MemoryStream())
                using (AssetsFileWriter w = new AssetsFileWriter(ms))
                {
                    bun.Write(w, reps);
                    data = ms.ToArray();
                }
                Console.WriteLine($"Writing changes to {file}...");

                //uabe doesn't seem to compress here

                foreach (Stream stream in streams)
                    stream.Close();

                bun.Close();

                File.WriteAllBytes(file, data);

                if (!flags.Contains("-kd") && !flags.Contains("-md") && File.Exists(decompFile))
                    File.Delete(decompFile);

                Console.WriteLine("Done.");
            }
        }

        private static void ApplyEmip(string[] args)
        {
            HashSet<string> flags = GetFlags(args);
            string emipFile = args[1];
            string rootDir = args[2];

            if (!File.Exists(emipFile))
            {
                Console.WriteLine($"File {emipFile} does not exist!");
                return;
            }

            InstallerPackageFile instPkg = new InstallerPackageFile();
            FileStream fs = File.OpenRead(emipFile);
            AssetsFileReader r = new AssetsFileReader(fs);
            instPkg.Read(r, true);

            Console.WriteLine($"Installing emip...");
            Console.WriteLine($"{instPkg.modName} by {instPkg.modCreators}");
            Console.WriteLine(instPkg.modDescription);

            foreach (var affectedFile in instPkg.affectedFiles)
            {
                string affectedFileName = Path.GetFileName(affectedFile.path);
                string affectedFilePath = Path.Combine(rootDir, affectedFile.path);

                if (affectedFile.isBundle)
                {
                    string decompFile = $"{affectedFilePath}.decomp";
                    string modFile = $"{affectedFilePath}.mod";
                    string bakFile = GetNextBackup(affectedFilePath);

                    if (bakFile == null)
                        return;

                    if (flags.Contains("-md"))
                        decompFile = null;

                    Console.WriteLine($"Decompressing {affectedFileName} to {decompFile ?? "memory"}...");
                    AssetBundleFile bun = DecompressBundle(affectedFilePath, decompFile);
                    List<BundleReplacer> reps = new List<BundleReplacer>();

                    foreach (var rep in affectedFile.replacers)
                    {
                        var bunRep = (BundleReplacer)rep;
                        if (bunRep is BundleReplacerFromAssets)
                        {
                            //read in assets files from the bundle for replacers that need them
                            string assetName = bunRep.GetOriginalEntryName();
                            var bunRepInf = BundleHelper.GetDirInfo(bun, assetName);
                            long pos = bunRepInf.Offset;
                            bunRep.Init(bun.DataReader, pos, bunRepInf.DecompressedSize);
                        }
                        reps.Add(bunRep);
                    }

                    Console.WriteLine($"Writing {modFile}...");
                    FileStream mfs = File.Open(modFile, FileMode.Create);
                    AssetsFileWriter mw = new AssetsFileWriter(mfs);
                    bun.Write(mw, reps, instPkg.addedTypes); //addedTypes does nothing atm

                    mfs.Close();
                    bun.Close();

                    Console.WriteLine($"Swapping mod file...");
                    File.Move(affectedFilePath, bakFile);
                    File.Move(modFile, affectedFilePath);

                    if (!flags.Contains("-kd") && !flags.Contains("-md") && File.Exists(decompFile))
                        File.Delete(decompFile);

                    Console.WriteLine($"Done.");
                }
                else //isAssetsFile
                {
                    string modFile = $"{affectedFilePath}.mod";
                    string bakFile = GetNextBackup(affectedFilePath);

                    if (bakFile == null)
                        return;

                    FileStream afs = File.OpenRead(affectedFilePath);
                    AssetsFileReader ar = new AssetsFileReader(afs);
                    AssetsFile assets = new AssetsFile();
                    assets.Read(ar);
                    List<AssetsReplacer> reps = new List<AssetsReplacer>();

                    foreach (var rep in affectedFile.replacers)
                    {
                        var assetsReplacer = (AssetsReplacer)rep;
                        reps.Add(assetsReplacer);
                    }

                    Console.WriteLine($"Writing {modFile}...");
                    FileStream mfs = File.Open(modFile, FileMode.Create);
                    AssetsFileWriter mw = new AssetsFileWriter(mfs);
                    assets.Write(mw, 0, reps, instPkg.addedTypes);

                    mfs.Close();
                    ar.Close();

                    Console.WriteLine($"Swapping mod file...");
                    File.Move(affectedFilePath, bakFile);
                    File.Move(modFile, affectedFilePath);

                    Console.WriteLine($"Done.");
                }
            }

            return;
        }

        public static void CLHMain(string[] args)
        {
            if (args.Length < 2)
            {
                PrintHelp();
                return;
            }

            string command = args[0];

            if (command == "batchexportbundle")
            {
                BatchExportBundle(args);
            }
            else if (command == "batchimportbundle")
            {
                BatchImportBundle(args);
            }
            else if (command == "applyemip")
            {
                ApplyEmip(args);
            }
            else if (command == "exportdumps") // exportdumps C:\Users\User\source\repos\UABEA-7\ReleaseFiles\modelist.bundle -TextMeshProUGUI
            {
                BatchExportAndDumpByName(args);
            }
            else if (command == "importdumps") // importdumps C:\Users\User\source\repos\UABEA-7\ReleaseFiles\modelist.bundle
            {
                BatchImportDumpToBundle(args);
            }
        }







        private static bool isDumped = false;
        private static bool needSave = false;
        public static List<string> dumpByNames = new List<string>();
        public static List<string> allTxt = new List<string>();
        public static AssetWorkspace? Workspace { get; private set; }
        public static AssetsManager am { get; private set; }
        private static ObservableCollection<AssetInfoDataGridItem> dataGridItems;
        public static List<Tuple<AssetsFileInstance, byte[]>> ChangedAssetsDatas { get; set; }

        private static bool PrepareWorkspace(string file)
        {
            am = new AssetsManager();

            string classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
            if (File.Exists(classDataPath))
            {
                am.LoadClassPackage(classDataPath);
            }

            AssetsFileInstance fileInst = am.LoadAssetsFile(file, true);
            if (!LoadOrAskTypeData(fileInst))
            {
                Console.WriteLine("Failed to load file.");
                return false;
            }

            //Console.WriteLine("Loaded file: " + fileInst.path);

            Workspace = new AssetWorkspace(am, false);

            Workspace.LoadAssetsFile(fileInst, true);

            if (!SetupContainers())
            {
                Console.WriteLine("Failed to setup containers.");
                return false;
            }

            return true;
        }

        private static bool LoadOrAskTypeData(AssetsFileInstance fileInst)
        {
            string uVer = fileInst.file.Metadata.UnityVersion;
            if (uVer == "0.0.0" && fileInst.parentBundle != null)
            {
                uVer = fileInst.parentBundle.file.Header.EngineVersion;
            }

            am.LoadClassDatabaseFromPackage(uVer);
            return true;
        }

        // настройка контейнеров
        private static bool SetupContainers()
        {
            if (Workspace == null || Workspace.LoadedFiles.Count == 0)
            {
                return false;
            }

            UnityContainer ucont = new UnityContainer();
            foreach (AssetsFileInstance file in Workspace.LoadedFiles)
            {
                AssetsFileInstance? actualFile;
                AssetTypeValueField? ucontBaseField;
                if (UnityContainer.TryGetBundleContainerBaseField(Workspace, file, out actualFile, out ucontBaseField))
                {
                    ucont.FromAssetBundle(am, actualFile, ucontBaseField);
                }
                else if (UnityContainer.TryGetRsrcManContainerBaseField(Workspace, file, out actualFile, out ucontBaseField))
                {
                    ucont.FromResourceManager(am, actualFile, ucontBaseField);
                }
            }

            foreach (var asset in Workspace.LoadedAssets)
            {
                AssetPPtr pptr = new AssetPPtr(asset.Key.fileName, 0, asset.Key.pathID);
                string? path = ucont.GetContainerPath(pptr);
                if (path != null)
                {
                    asset.Value.Container = path;
                }
            }

            return true;
        }

        // создание элементов таблицы данных
        private static ObservableCollection<AssetInfoDataGridItem> MakeDataGridItems()
        {
            dataGridItems = new ObservableCollection<AssetInfoDataGridItem>();

            if (Workspace == null || Workspace.LoadedFiles.Count == 0)
            {
                return dataGridItems;
            }

            Workspace.GenerateAssetsFileLookup();

            foreach (AssetContainer cont in Workspace.LoadedAssets.Values)
            {
                AddDataGridItem(cont);
            }

            // если не было дампа, то вывести все имена
            if (!isDumped)
            {
                foreach (AssetInfoDataGridItem item in dataGridItems)
                {
                    Console.WriteLine(item.Name);
                }
            }

            // если был дамп, то сохранить файл
            if (needSave)
            {
                SaveAssetsFile();
            }

            return dataGridItems;
        }




        // экспорт из бандла
        private static void BatchExportAndDumpByName(string[] args)
        {

            HashSet<string> flags = GetFlags(args);
            flags.Remove("-md");
            flags.Remove("-keepnames");
            flags.Remove("-kd");
            flags.Remove("-fd");

            string file = args[1];
            if (!File.Exists(file))
            {
                Console.WriteLine("File does not exist!");
                return;
            }

            string decompFile = $"{file}.decomp";

            AssetBundleFile bun = DecompressBundle(file, decompFile);

            int entryCount = bun.BlockAndDirInfo.DirectoryInfos.Length;
            string outName = "";
            for (int i = 0; i < entryCount; i++)
            {
                string name = bun.BlockAndDirInfo.DirectoryInfos[i].Name;
                byte[] data = BundleHelper.LoadAssetDataFromBundle(bun, i);

                outName = Path.Combine(Path.GetDirectoryName(file), $"{Path.GetFileName(file)}_{name}");
                Console.WriteLine($"Exporting {outName}...");
                File.WriteAllBytes(outName, data);
            }

            bun.Close();

            if (File.Exists(decompFile))
                File.Delete(decompFile);

            // добавить оставшиеся флаги в dumpByNames
            foreach (string flag in flags)
            {
                string dumpByName = flag.Replace("-", "");
                dumpByNames.Add(dumpByName);
                Console.WriteLine($"Dump: {dumpByName}");
            }

            // удалить все после последней точки в имени файла
            outName = outName.Substring(0, outName.LastIndexOf('.'));
            PrepareWorkspace(outName);

            MakeDataGridItems();

            Console.WriteLine("Done.");
        }

        // добавление элемента таблицы данных
        private static AssetInfoDataGridItem AddDataGridItem(AssetContainer cont, bool isNewAsset = false)
        {
            AssetsFileInstance thisFileInst = cont.FileInstance;

            string name;
            string container;
            string type;
            int fileId;
            long pathId;
            int size;
            string modified;

            container = cont.Container;
            fileId = Workspace.LoadedFiles.IndexOf(thisFileInst);
            pathId = cont.PathId;
            size = (int)cont.Size;
            modified = "";

            AssetNameUtils.GetDisplayNameFast(Workspace, cont, true, out name, out type);

            if (name.Length > 100)
                name = name[..100];
            if (type.Length > 100)
                type = type[..100];

            var item = new AssetInfoDataGridItem
            {
                TypeClass = (AssetClassID)cont.ClassId,
                Name = name,
                Container = container,
                Type = type,
                TypeID = cont.ClassId,
                FileID = fileId,
                PathID = pathId,
                Size = size,
                Modified = modified,
                assetContainer = cont
            };

            //Console.WriteLine(item.Name);

            if (!isNewAsset)
                dataGridItems.Add(item);
            else
                dataGridItems.Insert(0, item);

            // если в item.Name содержится dumpByNames
            foreach (string dumpByName in dumpByNames)
            {
                if (dumpByName != null && item.Name.Contains(dumpByName))
                {
                    SingleExportDump(item.Name, cont, thisFileInst);
                    isDumped = true;
                }
            }

            foreach (string fileTxt in allTxt)
            {
                // fileTxt = modelist.bundle_CAB-ce8a676bf4c36383835289e02833cef7-TextMeshProUGUI--5415249613762791440.txt

                // Регулярное выражение для извлечения частей
                string pattern = @"-(?<hash>[a-f0-9]+)-(?<name>[^-]+)-(?<suffix>-?\d+)";
                var match = Regex.Match(fileTxt, pattern);

                isDumped = true;

                if (match.Success)
                {
                    //string fHash = match.Groups["hash"].Value; // "ce8a676bf4c36383835289e02833cef7"
                    //string fName = match.Groups["name"].Value; // "TextMeshProUGUI"
                    long fId = long.Parse(match.Groups["suffix"].Value); // -5415249613762791440

                    if (fId == pathId)
                    {
                        SingleImportDump(fileTxt, cont, thisFileInst);
                        needSave = true;

                    }
                }
            }

            return item;
        }

        // экспорт дампа
        private static void SingleExportDump(string name, AssetContainer selectedCont, AssetsFileInstance selectedInst)
        {
            string selectedFilePath = $"{Path.GetDirectoryName(selectedInst.path)}/{Path.GetFileName(selectedInst.path)}-{name}-{selectedCont.PathId}.txt";

            using (FileStream fs = File.Open(selectedFilePath, FileMode.Create))
            using (StreamWriter sw = new StreamWriter(fs))
            {
                AssetTypeValueField? baseField = Workspace.GetBaseField(selectedCont);

                if (baseField == null)
                {
                    sw.WriteLine("Asset failed to deserialize.");
                    return;
                }

                AssetImportExport dumper = new AssetImportExport();

                if (selectedFilePath.EndsWith(".json"))
                    dumper.DumpJsonAsset(sw, baseField);
                else //if (extension == "txt")
                    dumper.DumpTextAsset(sw, baseField);
            }
        }




        // импорт в бандл
        private static void BatchImportDumpToBundle(string[] args)
        {
            string fileCAB = "";

            string file = GetMainFileName(args);
            if (file == null || !File.Exists(file))
            {
                Console.WriteLine("File does not exist!");
                return;
            }

            string[] files = Directory.GetFiles(Path.GetDirectoryName(file), $"{Path.GetFileName(file)}_CAB-*");

            foreach (string f in files)
            {
                string ext = Path.GetExtension(f);

                // если в имени файта есть txt - добавить в allTxt
                if (ext.Contains("txt"))
                {
                    allTxt.Add(f);
                    //Console.WriteLine(f);
                }
                else if (ext.Contains("_CAB-"))
                {
                    fileCAB = f;
                }

            }

            if (fileCAB == "" || !File.Exists(fileCAB))
            {
                Console.WriteLine("No files to import.");
                return;
            }

            PrepareWorkspace(fileCAB);
            MakeDataGridItems(); // создание элементов таблицы данных и импорт дампов в Assets

            BatchImportSingleBundle(file);
        }

        // импорт дампа
        private static void SingleImportDump(string file, AssetContainer selectedCont, AssetsFileInstance selectedInst)
        {

            using (FileStream fs = File.OpenRead(file))
            using (StreamReader sr = new StreamReader(fs))
            {
                AssetImportExport importer = new AssetImportExport();

                byte[]? bytes = null;
                string? exceptionMessage = null;
                if (file.EndsWith(".json"))
                {
                    AssetTypeTemplateField tempField = Workspace.GetTemplateField(selectedCont);
                    bytes = importer.ImportJsonAsset(tempField, sr, out exceptionMessage);
                }
                else
                {
                    bytes = importer.ImportTextAsset(sr, out exceptionMessage);
                }

                if (bytes == null)
                {
                    Console.WriteLine("Something went wrong when reading the dump file:");
                    Console.WriteLine(exceptionMessage);
                    return;
                }

                AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(selectedCont, bytes);
                Workspace.AddReplacer(selectedInst, replacer, new MemoryStream(bytes));
            }
        }

        // сохранение файла Assets
        private static void SaveAssetsFile()
        {
            var fileToReplacer = new Dictionary<AssetsFileInstance, List<AssetsReplacer>>();
            var changedFiles = Workspace.GetChangedFiles();

            foreach (var newAsset in Workspace.NewAssets)
            {
                AssetID assetId = newAsset.Key;
                AssetsReplacer replacer = newAsset.Value;
                string fileName = assetId.fileName;

                if (Workspace.LoadedFileLookup.TryGetValue(fileName.ToLower(), out AssetsFileInstance? file))
                {
                    if (!fileToReplacer.ContainsKey(file))
                        fileToReplacer[file] = new List<AssetsReplacer>();

                    fileToReplacer[file].Add(replacer);
                }
            }

            // false
            if (Workspace.fromBundle)
            {
                ChangedAssetsDatas.Clear();
                foreach (var file in changedFiles)
                {
                    List<AssetsReplacer> replacers;
                    if (fileToReplacer.ContainsKey(file))
                        replacers = fileToReplacer[file];
                    else
                        replacers = new List<AssetsReplacer>(0);

                    try
                    {
                        using (MemoryStream ms = new MemoryStream())
                        using (AssetsFileWriter w = new AssetsFileWriter(ms))
                        {
                            file.file.Write(w, 0, replacers);
                            ChangedAssetsDatas.Add(new Tuple<AssetsFileInstance, byte[]>(file, ms.ToArray()));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("There was a problem while writing the file:");
                        Console.WriteLine(ex.ToString());
                    }
                }

                Console.WriteLine("File saved. To complete changes, exit this window and File->Save in bundle window.");
            }
            else
            {
                List<int> changedFileIds = new List<int>();

                foreach (var file in changedFiles)
                {
                    //Console.WriteLine($"file: {file.name}");

                    List<AssetsReplacer> replacers;
                    if (fileToReplacer.ContainsKey(file))
                        replacers = fileToReplacer[file];
                    else
                        replacers = new List<AssetsReplacer>(0);

                    string? filePath;

                    string newName = "~" + file.name;
                    string dir = Path.GetDirectoryName(file.path)!;
                    filePath = Path.Combine(dir, newName);

                    try
                    {
                        using (FileStream fs = File.Open(filePath, FileMode.Create))
                        using (AssetsFileWriter w = new AssetsFileWriter(fs))
                        {
                            file.file.Write(w, 0, replacers);
                        }

                        string origFilePath = file.path;

                        // "overwrite" the original
                        file.file.Reader.Close();
                        File.Delete(file.path);
                        File.Move(filePath, origFilePath);
                        file.file = new AssetsFile();
                        file.file.Read(new AssetsFileReader(File.OpenRead(origFilePath)));
                        file.file.GenerateQuickLookup();

                        changedFileIds.Add(Workspace.LoadedFiles.IndexOf(file));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("There was a problem while writing the file:");
                        Console.WriteLine(ex.ToString());
                    }
                }

                foreach (AssetInfoDataGridItem item in dataGridItems)
                {
                    int fileId = item.FileID;
                    if (changedFileIds.Contains(fileId))
                    {
                        item.assetContainer.SetNewFile(Workspace.LoadedFiles[fileId]);
                    }
                }
            }
        }

        // импорт данных в бандл
        private static void BatchImportSingleBundle(string file)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine("File does not exist!");
                return;
            }

            string decompFile = $"{file}.decomp";

            AssetBundleFile bun = DecompressBundle(file, decompFile);

            List<BundleReplacer> reps = new List<BundleReplacer>();
            List<Stream> streams = new List<Stream>();

            int entryCount = bun.BlockAndDirInfo.DirectoryInfos.Length;
            for (int i = 0; i < entryCount; i++)
            {
                string name = bun.BlockAndDirInfo.DirectoryInfos[i].Name;
                string matchName = Path.Combine(Path.GetDirectoryName(file), $"{Path.GetFileName(file)}_{name}");

                if (File.Exists(matchName))
                {
                    FileStream fs = File.OpenRead(matchName);
                    long length = fs.Length;
                    reps.Add(new BundleReplacerFromStream(name, name, true, fs, 0, length));
                    streams.Add(fs);
                    Console.WriteLine($"Importing {matchName}...");
                }
            }

            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            using (AssetsFileWriter w = new AssetsFileWriter(ms))
            {
                bun.Write(w, reps);
                data = ms.ToArray();
            }
            Console.WriteLine($"Writing changes to {file}...");

            foreach (Stream stream in streams)
                stream.Close();

            bun.Close();

            File.WriteAllBytes(file, data);

            if (File.Exists(decompFile))
                File.Delete(decompFile);

            Console.WriteLine("Done.");
        }
    }
}
