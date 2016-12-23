using System.Linq;
using System.Reflection;
using Verse;

namespace AlcoholV
{
    [StaticConstructorOnStartup]
    internal static class ACOverrideInjector
    {
        static ACOverrideInjector()
        {
            //LongEventHandler.QueueLongEvent(Inject, "Initializing", true, null);
            Inject();
        }

        private static Assembly Assembly => Assembly.GetAssembly(typeof (ACOverrideInjector));
        private static string AssemblyName => Assembly.FullName.Split(',').First();

        private static void Inject()
        {
            Log.Message(AssemblyName + " injected.");
            var o = new LoadedOverride();
            o.OverrideIntoDefs();
        }
    }
}