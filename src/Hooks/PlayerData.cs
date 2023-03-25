﻿using IL.Menu.Remix.MixedUI;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using SlugBase;
using SlugBase.DataTypes;
using SlugBase.Features;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Color = UnityEngine.Color;
using Object = UnityEngine.Object;
using Vector2 = UnityEngine.Vector2;

namespace TheSacrifice
{
    internal static partial class Hooks
    {
        public static ConditionalWeakTable<Player, PlayerModule> PlayerData = new ConditionalWeakTable<Player, PlayerModule>();


        // Constant Features
        private const float FrameShortcutColorAddition = 0.003f;

        private const int FramesToStoreObject = 80;
        private const int FramesToRetrieveObject = 80;


        // Generates a unique texture ID so that the atlases don't overlap
        private static int _textureID = 0;
        public static int TextureID => _textureID++;



        private static bool IsCustomSlugcat(Player player) => player.SlugCatClass == Enums.Slugcat.Sacrifice;

        private static List<PlayerModule> GetAllPlayerData(RainWorldGame game)
        {
            List<PlayerModule> allPlayerData = new List<PlayerModule>();
            List<AbstractCreature> players = game.Players;

            if (players == null) return allPlayerData;

            foreach (AbstractCreature creature in players)
            {
                if (creature.realizedCreature == null) continue;

                if (creature.realizedCreature is not Player player) continue;

                if (!PlayerData.TryGetValue(player, out PlayerModule playerModule)) continue;

                allPlayerData.Add(playerModule);
            }

            return allPlayerData;
        }



        // SlugBase Features
        public static Vector2 Vector2Feature(JsonAny json)
        {
            JsonList jsonList = json.AsList();
            return new Vector2(jsonList[0].AsFloat(), jsonList[1].AsFloat());
        }
    }
}
