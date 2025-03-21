// Stubs.cs
namespace Community.PowerToys.Run.Plugin.VideoDownloader.Stubs
{
    public enum Key
    {
        C,
        // Add other keys as needed
    }

    public enum ModifierKeys
    {
        Control,
        // Add other modifiers as needed
    }

    public static class Clipboard
    {
        public static void SetDataObject(object data) 
        { 
            // Stub implementation for Linux build
            System.Console.WriteLine($"Would copy to clipboard: {data}");
        }
    }
}