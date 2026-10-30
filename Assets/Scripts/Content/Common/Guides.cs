using System.Collections.Generic;

namespace Epacenur.Content
{
    /// The encyclopedia's guides: how the game works, a page a topic (the rest of the encyclopedia is made from
    /// the content itself). Titles here are what other pages link to ("guide:" + title).
    public static class Guides
    {
        public static IEnumerable<KeyValuePair<string, string[]>> All => new[]
        {
            Guide("Getting started",
                "You wake in Epacenur, a city on safe ground. A bank, a general store, a chapel, a smithy and a workshop stand round its square.",
                "Chop trees with a hatchet, mine in quarries with a pickaxe, fish at pond shores, and cook what you catch on a fire you light with a tinderbox.",
                "Just east of Epacenur is a shrine with an air altar; just west, the field agility course; just north, a quarry with copper and tin.",
                "Every skill goes up to level 999. Press H for the controls, J for this encyclopedia, G to build (with a hammer)."),
            Guide("Controls",
                "Tile mode (the default): left-click to walk, chop, attack or take; right-click anything for all its options. WASD walks too.",
                "First person (V to switch): mouse to look, WASD to walk, left-click to chop or hit; Esc frees the mouse.",
                "Hold Shift to run while you have run energy. Keys 1-9 use melee abilities. M opens the world map, C hides the chat, B the side panel.",
                "G opens the build menu (with a hammer), J this encyclopedia, H the help, F1 the dev tools."),
            Guide("Towns and cities",
                "Towns and cities lie far apart, at most one in each stretch of 5 x 5 regions, and some stretches have none. About one in four is a city, with more houses.",
                "Each has a bank, a general store, a chapel with an altar, a smithy with a furnace and an anvil, a workshop with a spinning wheel and tanning rack, and two market stalls.",
                "Their people can be pickpocketed; towns further from Epacenur have richer stalls and tougher guards.",
                "When you die, you wake up in the nearest town, unless you've slept in a bed."),
            Guide("The wilderness",
                "Within 48 tiles of a town's square (80 for a city) is safe ground: only chickens, cows and goblins, and none of them aggressive.",
                "Beyond it the wilderness begins: wilderness level 1 at the edge, one more every 6 tiles further from the nearest town, up to level 60, falling again as you near the next town.",
                "Deeper in, the monsters are fiercer (wolves, wizards, bandits, hill giants) and the resources better: maples, yews, coal, mithril, adamantite, the better fishing spots and altars.",
                "From level 24 the land turns to wasteland: the deep wilderness. Your level shows under the minimap, and the world map tints the wilderness red."),
            Guide("Biomes",
                "Meadows are open, with flax fields and few trees. Woodland is thick forest. Marshes are wet, with many ponds. Highlands lie up in the hills. Wasteland covers the deep wilderness.",
                "Each has its favourite trees and rocks: willows in marshes, oaks and maples in woodland, coal and mithril in the highlands, yews and adamantite in the wasteland."),
            Guide("Quarries",
                "Quarries are the only places with rocks to mine: gravel pits with rows of rocks, spoil heaps and a mine cart.",
                "Most of a quarry's rocks are one kind (it's named for it on the world map, e.g. Iron quarry); the rest are a mix. Better ores lie in quarries deeper in the wilderness.",
                "Limestone, for building, is mined in quarries near towns."),
            Guide("Shrines and runecrafting",
                "Shrines are rings of standing stones with an altar in the middle and rune essence boulders round them. Mine the essence (it never runs out).",
                "Craft-rune at the altar turns all your essence into its runes at once. Better altars lie deeper in the wilderness. Runes power the spells in the Magic tab."),
            Guide("Combat",
                "Attack a monster to fight it: choose an attack style in the Worn tab (each trains a different skill). Melee abilities on the action bar (1-9) hit harder, with cooldowns.",
                "Bows shoot from range (they need arrows); a staff and runes cast spells. Prayers boost you while they drain your prayer points; pray at an altar to recharge.",
                "Eat cooked food to heal. Aggressive monsters attack you if you come close, unless your combat level is more than twice theirs."),
            Guide("Death and gravestones",
                "When you die you keep your 3 most valuable items; the rest go into a gravestone where you fell. Your money pouch and tool belt are always safe.",
                "Get back to it within 15 minutes and Reclaim items, or it collapses and leaves them on the ground.",
                "You wake up beside your bed if you've slept in one, otherwise in the nearest town."),
            Guide("Building a base",
                "With a hammer, press G (or Build under the minimap) and pick a piece. Its ghost shows where it goes: green if it can, red with the reason if not. Click to place it; R turns it.",
                "Your first foundation can go anywhere outside towns, at any angle; everything after snaps to that base's grid. Walls, doorways and windows go on foundations' edges, roofs over them, furniture in them.",
                "Planks come from a saw on logs, nails from an anvil or the general store, limestone from quarries. Stone pieces need Construction 20.",
                "Build a storage chest to keep things, a bed to wake up in, and stations (a furnace, an anvil, an altar...) to work at home. Right-click a piece with a hammer to take it down for half its cost."),
            Guide("The bank, pouch and tool belt",
                "The bank keeps every kind of item in one slot, any amount. Every town has one, and a storage chest in your base works the same way.",
                "Coins go in your money pouch, which never takes an inventory slot and is never lost when you die.",
                "Tools on your tool belt work as if they were in your backpack. Right-click a tool and Add to tool belt."),
            Guide("Run energy",
                "Running (hold Shift) uses run energy; walking or standing brings it back, faster the higher your Agility. With none left you can only walk. Energy potions restore it."),
            Guide("Day and night",
                "A day lasts 24 minutes of play. The sun rises in the east and sets in the west; at night a dim moon lights the world and the town lamps glow."),
            Guide("Sound",
                "Every sound in the game is made as it plays: there are no recordings. The Sound button left of the minimap turns the volume down a step at a time, then off, then back up."),
        };

        static KeyValuePair<string, string[]> Guide(string title, params string[] lines) => new KeyValuePair<string, string[]>(title, lines);
    }
}
