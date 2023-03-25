﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TheSacrifice
{
    internal static partial class Hooks
    {
        private static bool IsObjectStorable(AbstractPhysicalObject abstractObject)
        {
            if (abstractObject.type == AbstractPhysicalObject.AbstractObjectType.DataPearl) return true;
            if (abstractObject.type == AbstractPhysicalObject.AbstractObjectType.PebblesPearl) return true;
            if (abstractObject.type == MoreSlugcats.MoreSlugcatsEnums.AbstractObjectType.HalcyonPearl) return true;
            if (abstractObject.type == MoreSlugcats.MoreSlugcatsEnums.AbstractObjectType.Spearmasterpearl) return true;

            return false;
        }

        private const float HIGHLIGHT_COLOR_MULTIPLIER = 1.5f;

        private static List<Color> GetObjectAccentColors(AbstractPhysicalObject abstractObject)
        {
            List<Color> colors = new List<Color>();

            if (abstractObject.realizedObject == null) return colors;

            if (abstractObject is DataPearl.AbstractDataPearl dataPearl)
            {
                if (dataPearl.dataPearlType == MoreSlugcats.MoreSlugcatsEnums.DataPearlType.DM)
                    colors.Add(((DataPearl)dataPearl.realizedObject).color);

                else
                    colors.Add(ItemSymbol.ColorForItem(dataPearl.type, 0));
            }

            if (colors.Count > 0)
                colors.Add(colors[0] * HIGHLIGHT_COLOR_MULTIPLIER);

            return colors;
        }
    }
}
