﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ATSEngineTool.Database;

namespace ATSEngineTool
{
    public static class Mod
    {
        /// <summary>
        /// Gets the Full Mod directory path
        /// </summary>
        public static string ModPath
        {
            get
            {
                if (Program.Config.IntegrateWithMod)
                {
                    return Path.Combine(
                        Program.Config.SteamPath, "SteamApps", "workshop",
                        "content", "270880", "650050267", "latest"
                    );
                }
                else
                {
                    return CompilePath;
                }
            }
        }

        /// <summary>
        /// Gets the directory path where def files are compiled
        /// </summary>
        public static string DefCompilePath => Path.Combine(Program.RootPath, "__compile__", "def");

        /// <summary>
        /// Gets the directory path where def files are compiled
        /// </summary>
        public static string CompilePath => Path.Combine(Program.RootPath, "__compile__");

        /// <summary>
        /// Removes all temporary compiled files from the Compile directory
        /// </summary>
        public static void CleanCompileDirectory(IProgress<TaskProgressUpdate> progress)
        {
            if (Directory.Exists(CompilePath))
            {
                try
                {
                    // Report
                    ProgressUpdate(progress, "Cleaning compile directory...");

                    // Delete Directory and create a new
                    DirectoryExt.Delete(CompilePath);
                    Directory.CreateDirectory(DefCompilePath);
                }
                catch (Exception e) when (e.Message.Contains("empty"))
                {
                    throw new Exception("Unable to delete Compile directory because it is open. Please close this directory and try again.", e);
                }
            }
        }

        /// <summary>
        /// Synchronizes the compiled files with the mods files.
        /// </summary>
        /// <param name="cleanEngines">If true, the mods def folder will be deleted</param>
        /// <param name="cleanSounds">
        /// If true, all sound files will be deleted from the mod and the current sound 
        /// files will be copied over to the mod.
        /// </param>
        public static void Sync(bool cleanEngines, bool cleanSounds, IProgress<TaskProgressUpdate> progress)
        {
            // Get our folder type name
            string fname = (Program.Config.IntegrateWithMod) ? "mod" : "compile";

            // Clear old def files?
            string path = Path.Combine(ModPath, "def");
            if (cleanEngines && Directory.Exists(path))
            {
                // Show Update
                ProgressUpdate(progress, $"Deleting existing {fname} def files");
                DirectoryExt.Delete(path);
            }

            // Sync the def files
            if (Program.Config.IntegrateWithMod)
            {
                ProgressUpdate(progress, "Copying compiled def files to mod folder");
                DirectoryExt.Copy(DefCompilePath, path, true, true);
            }

            // Sync icons
            ProgressUpdate(progress, $"Copying dds graphics to {fname} folder");
            path = Path.Combine(ModPath, "material", "ui", "accessory");
            DirectoryExt.Copy(Path.Combine(Program.RootPath, "graphics"), path, true, true);

            // Sync common sound files
            path = Path.Combine(ModPath, "sound", "truck", "common");
            if (cleanSounds && Directory.Exists(path))
            {
                ProgressUpdate(progress, $"Deleting old common sound files in {fname} folder");
                DirectoryExt.Delete(path);
            }

            if (cleanSounds || !Directory.Exists(path))
            {
                ProgressUpdate(progress, $"Copying common sound files to {fname} folder");
                DirectoryExt.Copy(Path.Combine(Program.RootPath, "sounds", "common"), path, true, true);
            }

            // Sync common noise files
            path = Path.Combine(ModPath, "sound", "truck", "noises");
            if (cleanSounds && Directory.Exists(path))
            {
                ProgressUpdate(progress, $"Deleting old noise sound files in {fname} folder");
                DirectoryExt.Delete(path);
            }

            if (cleanSounds || !Directory.Exists(path))
            {
                ProgressUpdate(progress, $"Copying noise sound files to {fname} folder");
                DirectoryExt.Copy(Path.Combine(Program.RootPath, "sounds", "noises"), path, true, true);
            }

            // Sync engine sounds
            using (AppDatabase db = new AppDatabase())
            {
                foreach (SoundPackage sound in db.SoundPackages)
                {
                    // Mod folder path to the sounds
                    path = Path.Combine(ModPath, "sound", "truck", "engine", sound.FolderName);
                    if (cleanSounds && Directory.Exists(path))
                    {
                        ProgressUpdate(progress, $"Deleting old {sound.FolderName} sound files in {fname} folder");
                        DirectoryExt.Delete(path);
                    }

                    // Paths
                    string intPath = Path.Combine(path, "int");
                    string extPath = Path.Combine(path, "ext");

                    // Interior Sounds
                    string localPath = Path.Combine(Program.RootPath, "sounds", "engine", sound.FolderName, "int");
                    if (Directory.Exists(localPath) && (cleanSounds || !Directory.Exists(intPath)))
                    {
                        ProgressUpdate(progress, $"Copying exterior sound files to {fname} folder");
                        DirectoryExt.Copy(localPath, intPath, true, true);
                    }

                    // Exterior Sounds
                    localPath = Path.Combine(Program.RootPath, "sounds", "engine", sound.FolderName, "ext");
                    if (Directory.Exists(localPath) && (cleanSounds || !Directory.Exists(extPath)))
                    {
                        ProgressUpdate(progress, $"Copying exterior sound files to {fname} folder");
                        DirectoryExt.Copy(localPath, extPath, true, true);
                    }
                }
            }
        }

        private static void ProgressUpdate(IProgress<TaskProgressUpdate> progress, string v)
        {
            // Show Update
            if (progress != null)
            {
                var update = new TaskProgressUpdate();
                update.MessageText = v;
                TaskForm.Progress.Report(update);
            }
        }

        /// <summary>
        /// Takes the engine data from the database, and compiles the mod.
        /// </summary>
        /// <param name="trucks">A list of trucks we are compiling engines for</param>
        public static void Compile(IEnumerable<Truck> trucks, IProgress<TaskProgressUpdate> progress)
        {
            // Local variables
            string truckpath, soundPath, enginePath;
            var soundData = new Dictionary<SoundPackage, List<Engine>>();
            var suitableFor = new StringBuilder();
            ProgressUpdate(progress, "Generating engine def files");

            // Connect to the database
            using (AppDatabase db = new AppDatabase())
            {
                foreach (Truck truck in trucks)
                {
                    // Clear old junk
                    soundData.Clear();

                    // Define paths we will use
                    truckpath = Path.Combine(DefCompilePath, "vehicle", "truck", truck.UnitName);
                    soundPath = Path.Combine(truckpath, "sound");
                    enginePath = Path.Combine(truckpath, "engine");

                    // Create file directories
                    if (!Directory.Exists(truckpath))
                        Directory.CreateDirectory(truckpath);

                    if (!Directory.Exists(soundPath))
                        Directory.CreateDirectory(soundPath);

                    if (!Directory.Exists(enginePath))
                        Directory.CreateDirectory(enginePath);

                    // ==============================
                    // Create engine files
                    foreach (Engine engine in truck.TruckEngines.Select(x => x.Engine))
                    {
                        string contents = engine.ToSiiFormat(truck.UnitName);
                        SoundPackage sound = engine.Series.SoundPackage;

                        // === Add engine to sound list
                        if (!soundData.ContainsKey(sound))
                            soundData[sound] = new List<Engine>() { engine };
                        else
                            soundData[sound].Add(engine);

                        // Create/Open the engine.sii file, and write the new contents
                        string path = Path.Combine(enginePath, engine.FileName);
                        using (FileStream str = File.Open(path, FileMode.Create))
                        using (StreamWriter writer = new StreamWriter(str))
                        {
                            writer.Write(contents);
                        }
                    }

                    // ==============================
                    // Create sound files
                    ProgressUpdate(progress, "Generating sound def files");
                    foreach (var soundPair in soundData)
                    {
                        SoundPackage sound = soundPair.Key;
                        List<Engine> engines = soundPair.Value;
                        suitableFor.Clear();

                        string root = Path.Combine(Program.RootPath, "sounds", "engine", sound.FolderName);
                        string filePath = Path.Combine(root, "interior.sii");

                        // Add engines
                        foreach (Engine engine in engines)
                        {
                            // Indent
                            if (suitableFor.Length > 0)
                                suitableFor.Append("\t");

                            suitableFor.AppendLine($"suitable_for[]: \"{engine.UnitName}.{truck.UnitName}.engine\"");
                        }

                        // === Interior
                        // Open the sound interrior template file
                        using (FileStream stream = File.OpenRead(filePath))
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            // Add truck name
                            string contents = reader.ReadToEnd();
                            contents = contents.Replace("{{{NAME}}}", truck.UnitName);

                            // Add suitable engines
                            contents = contents.Replace("{{{SUITABLE}}}", 
                                suitableFor.ToString().TrimEnd(Environment.NewLine.ToCharArray())
                            );

                            // Create new file
                            string path = Path.Combine(soundPath, sound.InteriorFileName);
                            using (FileStream str = File.Open(path, FileMode.Create))
                            using (StreamWriter writer = new StreamWriter(str))
                            {
                                writer.Write(contents);
                            }
                        }

                        // === Exterior
                        // Open the sound interrior template file
                        filePath = Path.Combine(root, "exterior.sii");
                        using (FileStream stream = File.OpenRead(filePath))
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            // Add truck name
                            string contents = reader.ReadToEnd();
                            contents = contents.Replace("{{{NAME}}}", truck.UnitName);

                            // Add suitable engines
                            contents = contents.Replace("{{{SUITABLE}}}",
                                suitableFor.ToString().TrimEnd(Environment.NewLine.ToCharArray())
                            );

                            // Create new file
                            string path = Path.Combine(soundPath, sound.ExteriorFileName);
                            using (FileStream str = File.Open(path, FileMode.Create))
                            using (StreamWriter writer = new StreamWriter(str))
                            {
                                writer.Write(contents);
                            }
                        }
                    } // End foreach sound

                    // == Transmissions?
                    var list = truck.TruckTransmissions.ToList();
                    if (list.Count > 0)
                    {
                        var transPath = Path.Combine(truckpath, "transmission");
                        if (!Directory.Exists(transPath))
                            Directory.CreateDirectory(transPath);

                        foreach (var transmission in list.Select(x => x.Transmission))
                        {
                            string contents = transmission.ToSiiFormat(truck.UnitName);

                            // Create/Open the engine.sii file, and write the new contents
                            string path = Path.Combine(transPath, transmission.FileName);
                            using (FileStream str = File.Open(path, FileMode.Create))
                            using (StreamWriter writer = new StreamWriter(str))
                            {
                                writer.Write(contents);
                            }
                        }
                    }

                } // End foreach truck

                // thoughts??
            }
        }
    }
}
