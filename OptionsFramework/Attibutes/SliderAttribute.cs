﻿using System;

namespace MetroOverhaul.OptionsFramework.Attibutes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SliderAttribute : AbstractOptionsAttribute
    {
        public SliderAttribute(string title, string description, float min, float max, float step, string group = null, Type actionClass = null, string actionMethod = null) : base(title, description, group, actionClass, actionMethod)
        {
            Min = min;
            Max = max;
            Step = step;
        }

        public float Min { get; private set;  }

        public float Max { get; private set; }

        public float Step { get; private set; }
    }
}
