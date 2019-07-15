﻿using System;

namespace MetroOverhaul.OptionsFramework.Attibutes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class TextfieldAttribute : AbstractOptionsAttribute
    {
        public TextfieldAttribute(string title, string description, string group = null, Type actionClass = null, string actionMethod = null) : base(title, description, group, actionClass, actionMethod)
        {
        }
    }
}