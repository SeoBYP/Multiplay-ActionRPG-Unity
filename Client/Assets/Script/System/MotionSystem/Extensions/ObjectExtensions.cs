using System;

namespace Game.System.MotionSystem
{
    public static class ObjectExtensions
    {
        public static T Also<T>(this T self, Action<T> block) where T: class
        {
            block(self);
            return self;
        }
    }
}