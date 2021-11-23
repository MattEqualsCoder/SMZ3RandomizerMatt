﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Windows.Media;

using Randomizer.SMZ3.Tracking;

namespace Randomizer.App.ViewModels
{
    /// <summary>
    /// Represents user-configurable options for the general working of the
    /// randomizer itself.
    /// </summary>
    public class GeneralOptions
    {
        /// <summary>
        /// Converts the enum descriptions into a string array for displaying in a dropdown
        /// </summary>
        public static IEnumerable<string> QuickLaunchOptions
        {
            get
            {
                var attributes = typeof(LaunchButtonOptions).GetMembers()
                    .SelectMany(member => member.GetCustomAttributes(typeof(DescriptionAttribute), true).Cast<DescriptionAttribute>())
                    .ToList();
                return attributes.Select(x => x.Description);
            }
        }

        public string Z3RomPath { get; set; }

        public string SMRomPath { get; set; }

        public string RomOutputPath { get; set; }
            = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMZ3CasRandomizer", "Seeds");

        [Range(0.0, 1.0)]
        public float TrackerRecognitionThreshold { get; set; } = 0.75f;

        [Range(0.0, 1.0)]
        public float TrackerConfidenceThreshold { get; set; } = 0.85f;

        [Range(0.0, 1.0)]
        public float TrackerConfidenceSassThreshold { get; set; } = 0.92f;

        public Color TrackerBackgroundColor { get; set; } = Color.FromRgb(0x21, 0x21, 0x21);

        public bool TrackerShadows { get; set; } = true;

        public int LaunchButton { get; set; } = (int)LaunchButtonOptions.PlayAndTrack;

        public bool Validate()
        {
            return File.Exists(Z3RomPath)
                && File.Exists(SMRomPath)
                && (Directory.Exists(RomOutputPath) || RomOutputPath == null);
        }

        public TrackerOptions GetTrackerOptions() => new()
        {
            MinimumRecognitionConfidence = TrackerRecognitionThreshold,
            MinimumExecutionConfidence = TrackerConfidenceThreshold,
            MinimumSassConfidence = TrackerConfidenceSassThreshold
        };
    }

    /// <summary>
    /// Enum for the launch button options
    /// </summary>
    public enum LaunchButtonOptions
    {
        [Description("Play Rom and Open Tracker")]
        PlayAndTrack,
        [Description("Open Folder and Tracker")]
        OpenFolderAndTrack,
        [Description("Open Tracker")]
        TrackOnly,
        [Description("Play Rom")]
        PlayOnly,
        [Description("Open Folder")]
        OpenFolderOnly
    }
}
