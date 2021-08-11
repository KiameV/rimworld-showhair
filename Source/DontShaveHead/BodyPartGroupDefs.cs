using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace ShowHair
{
    public static class Enums
    {
        public enum Coverage
        {
            None = 0,
            Jaw = 1,
            UpperHead = 2,
            FullHead = 3
        }
    }
    public class BodyPartGroupDefExtension : DefModExtension
    {
        //extra defs added to BodyPartGroupDef via Patches/DefExtension
        public bool IsHeadDef; //defines if the body part group relates to the head
        public Enums.Coverage CoverageLevel; //numeric/enum value to define which def covers the most head
    }

}
