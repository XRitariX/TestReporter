using System;

namespace TestReporter.Models
{
    public class LoadedFile
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Format { get; set; }
        public string Status { get; set; }
    }
}