#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace OneMoreMove.EditorTools
{
    /// <summary>Info.plist entries App Store Connect asks about on every upload.</summary>
    public sealed class IosPostBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;

            var path = Path.Combine(report.summary.outputPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(path);

            // Only standard HTTPS (exempt from export documentation), so the per-upload encryption question is answered.
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(path);
        }
    }
}
#endif
