using System;
using System.IO;
using UnityEngine;

namespace ParkRangers.Spikes.Shared
{
    /// <summary>
    /// The commit a build came from, written into StreamingAssets by BuildStampWriter.
    /// NetworkBootstrap draws it and refuses joins from another commit. Build time is display only,
    /// so Windows and Linux builds of one commit can play together. Editors stamp as "editor".
    /// </summary>
    public static class BuildStamp
    {
        public const string FileName = "build_stamp.txt";

        /// <summary>Compared on join: short commit hash, plus "-dirty" if Assets/Packages had local changes.</summary>
        public static string Commit { get; }

        public static string Label { get; }

        static BuildStamp()
        {
            if (Application.isEditor)
            {
                Commit = Label = "editor";
                return;
            }
            try
            {
                var lines = File.ReadAllLines(Path.Combine(Application.streamingAssetsPath, FileName));
                Commit = lines[0];
                Label = $"{lines[0]}  built {lines[1]}";
            }
            catch (Exception)
            {
                Commit = Label = "unstamped";
            }
        }
    }
}
