namespace LESs
{
    public class LessMod
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Library { get; set; }
        public bool DisabledByDefault { get; set; }
        public string Directory { get; set; }
        public LessPatch[] Patches { get; set; }
    }
}
