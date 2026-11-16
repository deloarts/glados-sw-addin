using System;

namespace GladosSwAddin
{
    [AttributeUsage(AttributeTargets.Class)]
    public class SwAddinAttribute : Attribute
    {
        public string Title { get; set; }
        public string Description { get; set; }

        public bool LoadAtStartup { get; set; } = true;

        public int MajorVersion { get; set; } = 1;
        public int MinorVersion { get; set; } = 0;
    }
}
