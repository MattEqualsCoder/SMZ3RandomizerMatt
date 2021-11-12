﻿using System;
using System.Collections.Generic;

namespace Randomizer.SMZ3.Tracking
{
    /// <summary>
    /// Represents the currently active tracker configuration.
    /// </summary>
    public class TrackerConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TrackerConfig"/> class
        /// with the specified item data.
        /// </summary>
        /// <param name="items">The item data to use.</param>
        /// <param name="pegs">The peg data.</param>
        /// <param name="dungeons">The dungeon data.</param>
        /// <param name="responses">The responses to use.</param>
        public TrackerConfig(IReadOnlyCollection<ItemData> items,
            IReadOnlyCollection<Peg> pegs,
            IReadOnlyCollection<ZeldaDungeon> dungeons,
            ResponseConfig responses)
        {
            Items = items;
            Pegs = pegs;
            Dungeons = dungeons;
            Responses = responses;
        }

        /// <summary>
        /// Gets a collection of trackable items.
        /// </summary>
        public IReadOnlyCollection<ItemData> Items { get; init; }

        /// <summary>
        /// Gets the peg world peg configuration.
        /// </summary>
        public IReadOnlyCollection<Peg> Pegs { get; init; }

        /// <summary>
        /// Gets a collection of Zelda dungeons.
        /// </summary>
        public IReadOnlyCollection<ZeldaDungeon> Dungeons { get; init; }

        /// <summary>
        /// Gets a collection of configured responses.
        /// </summary>
        public ResponseConfig Responses { get; init; }
    }
}
