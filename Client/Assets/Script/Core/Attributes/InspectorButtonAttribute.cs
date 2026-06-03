using System;

namespace Game.Core
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class InspectorButtonAttribute : Attribute
    {
        public string Label { get; }
        public bool PlayModeOnly { get; }
        
        public InspectorButtonAttribute(string label, bool playModeOnly = false)
        {
            Label = label;
            PlayModeOnly = playModeOnly;
        }
    }
}