using Rage;

[assembly: Rage.Attributes.Plugin("AboveTheLaw", Author = "Gerhart, GerhartRTFO, PapaGee", Description = "Corrupt Cop Plugin")]

namespace AboveTheLaw
{
    public static class EntryPoint
    {
        public static ModController Controller { get; private set; }

        public static void Main()
        {
            Controller = new ModController();
            Controller.Start();
        }

        public static void OnUnload(bool isTerminating)
        {
            Controller?.Stop();
        }
    }
}