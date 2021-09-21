﻿using System.Collections.Generic;

using static Randomizer.SMZ3.Reward;

namespace Randomizer.SMZ3.Regions.Zelda.DarkWorld
{
    public class DarkWorldNorthEast : Z3Region
    {

        public override string Name => "Dark World North East";
        public override string Area => "Dark World";

        public DarkWorldNorthEast(World world, Config config) : base(world, config)
        {
            Locations = new List<Location> {
                new Location(this, 256+78, 0x1DE185, LocationType.Regular, "Catfish",
                    items => items.MoonPearl && items.CanLiftLight()),
                new Location(this, 256+79, 0x308147, LocationType.Regular, "Pyramid"),
                new Location(this, 256+80, 0x1E980, LocationType.Regular, "Pyramid Fairy - Left",
                    items => World.CanAquireAll(items, CrystalRed) && items.MoonPearl && World.DarkWorldSouth.CanEnter(items) &&
                        (items.Hammer || items.Mirror && World.CanAquire(items, Agahnim))),
                new Location(this, 256+81, 0x1E983, LocationType.Regular, "Pyramid Fairy - Right",
                    items => World.CanAquireAll(items, CrystalRed) && items.MoonPearl && World.DarkWorldSouth.CanEnter(items) &&
                        (items.Hammer || items.Mirror && World.CanAquire(items, Agahnim)))
            };
        }

        public override bool CanEnter(Progression items)
        {
            return World.CanAquire(items, Agahnim) || items.MoonPearl && (
                items.Hammer && items.CanLiftLight() ||
                items.CanLiftHeavy() && items.Flippers ||
                items.CanAccessDarkWorldPortal(Config) && items.Flippers
            );
        }

    }

}
