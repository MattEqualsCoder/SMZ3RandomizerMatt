﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Randomizer.Data.Configuration.ConfigFiles;
using Randomizer.SMZ3.Tracking.Services;

namespace Randomizer.SMZ3.Tracking.VoiceCommands
{
    /// <summary>
    /// Provides voice commands for turning on Go Mode.
    /// </summary>
    public class GoModeModule : TrackerModule
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="GoModeModule"/> class.
        /// </summary>
        /// <param name="tracker">The tracker instance.</param>
        /// <param name="itemService">Service to get item information</param>
        /// <param name="worldService">Service to get world information</param>
        /// <param name="logger">Used to log information.</param>
        /// <param name="responseConfig"></param>
        public GoModeModule(Tracker tracker, IItemService itemService, IWorldService worldService, ILogger<GoModeModule> logger, ResponseConfig responseConfig)
            : base(tracker, itemService, worldService, logger)
        {
            AddCommand("Toggle Go Mode", GetGoModeRule(responseConfig.GoModePrompts), (result) =>
            {
                tracker.ToggleGoMode(result.Confidence);
            });
        }

        private GrammarBuilder GetGoModeRule(List<string> prompts)
        {
            return new GrammarBuilder()
                .Append("Hey tracker,")
                .OneOf(prompts.ToArray());
        }
    }
}
