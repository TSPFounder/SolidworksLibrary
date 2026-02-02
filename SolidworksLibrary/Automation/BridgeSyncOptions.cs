namespace SolidworksLibrary.Automation
{
    public sealed class BridgeSyncOptions
    {
        public bool AllowNewParameters { get; set; } = true;
        public bool OverwriteExisting { get; set; } = true;
        public string TargetPartName { get; set; }
    }
}
