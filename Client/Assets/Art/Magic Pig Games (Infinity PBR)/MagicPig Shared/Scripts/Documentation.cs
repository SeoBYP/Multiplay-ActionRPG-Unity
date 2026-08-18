using System;

namespace MagicPigGames.Shared
{
    /*
     * Add the [Documentation] attribute to a class to provide a description and a URL to the documentation.
     */

    [AttributeUsage(AttributeTargets.Class)]
    public class Documentation : Attribute
    {
        public Documentation(string description = "[No class description provided]",
            string url = "https://infinitypbr.gitbook.io/infinity-pbr")
        {
            URL = url;
            Description = description;
        }

        public string URL { get; private set; }
        public string Description { get; private set; }
    }
}