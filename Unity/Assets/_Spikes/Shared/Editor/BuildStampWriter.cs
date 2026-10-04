using System;
using System.Diagnostics;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ParkRangers.Spikes.Shared
{
    /// <summary>
    /// Writes BuildStamp's file next to every player build: short commit (+ "-dirty") and build time.
    /// The commit is read first thing in the build: other build hooks write into Assets (the Performance
    /// Testing package adds Assets/Resources/PerformanceTestRunInfo.json), which would mark every build dirty.
    /// </summary>
    public class BuildStampWriter : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        static string commit;

        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report) => commit = ReadGit();

        public void OnPostprocessBuild(BuildReport report)
        {
            // ponytail: Windows/Linux layout (<name>_Data beside the executable); other platforms need their own path.
            var dir = Path.Combine(Path.ChangeExtension(report.summary.outputPath, null) + "_Data", "StreamingAssets");
            Directory.CreateDirectory(dir);
            File.WriteAllLines(Path.Combine(dir, BuildStamp.FileName), new[] { commit, DateTime.Now.ToString("yyyy-MM-dd HH:mm") });
            UnityEngine.Debug.Log($"[BuildStamp] {commit} -> {report.summary.outputPath}");
        }

        static string ReadGit()
        {
            try
            {
                var head = Git("rev-parse --short HEAD");
                // ponytail: only Assets/ and Packages/ count as dirty; ProjectSettings churns on its own (ProBuilder, Unity Connect).
                return Git("status --porcelain -- Assets Packages").Length > 0 ? head + "-dirty" : head;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[BuildStamp] git failed, build stamped 'nogit': {e.Message}");
                return "nogit";
            }
        }

        static string Git(string args)
        {
            using var p = Process.Start(new ProcessStartInfo("git", args) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            if (p.ExitCode != 0) throw new Exception($"git {args} exited with {p.ExitCode}");
            return output;
        }
    }
}
