using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace AlcoholV
{
    internal class LoadedOverride
    {
        public List<OverridePackage> OverrideDefs = new List<OverridePackage>();

        public void OverrideIntoDefs()
        {
            LoadData();

            foreach (var current in OverrideDefs)
            {
                current.InjectIntoDefs();
            }
        }

        public void LoadData()
        {
            foreach (var current in LoadedModManager.RunningMods)
            {
                var path = Path.Combine(current.RootDir, "Override");
                var directoryInfo = new DirectoryInfo(path);

                if (directoryInfo.Exists)
                {
                    var directories = directoryInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                    for (var j = 0; j < directories.Length; j++)
                    {
                        var directoryInfo3 = directories[j];
                        var name = directoryInfo3.Name;
                        var typeInAnyAssembly = GenTypes.GetTypeInAnyAssembly(name);
                        if (typeInAnyAssembly == null && name.Length > 3)
                        {
                            typeInAnyAssembly = GenTypes.GetTypeInAnyAssembly(name.Substring(0, name.Length - 1));
                        }
                        if (typeInAnyAssembly == null)
                        {
                            Log.Warning(string.Concat("Error loading override from ", current.Name, ": dir ", directoryInfo3.Name, " doesn't correspond to any def type. Skipping..."));
                        }
                        else
                        {
                            var files2 = directoryInfo3.GetFiles("*.xml", SearchOption.AllDirectories);
                            for (var k = 0; k < files2.Length; k++)
                            {
                                var file2 = files2[k];
                                LoadFromFile_DefInject(file2, typeInAnyAssembly);
                            }
                        }
                    }
                }
            }
        }

        public void LoadFromFile_DefInject(FileInfo file, Type defType)
        {
            var defInjectionPackage = (from di in OverrideDefs where di.defType == defType select di).FirstOrDefault();
            if (defInjectionPackage == null)
            {
                defInjectionPackage = new OverridePackage(defType);
                OverrideDefs.Add(defInjectionPackage);
            }
            defInjectionPackage.AddDataFromFile(file);
        }
    }
}