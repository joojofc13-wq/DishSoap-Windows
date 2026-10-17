using System;
using System.IO;

namespace Microsoft.Xna.Framework.Storage
{
    public sealed class StorageDevice
    {
        public bool IsConnected => true;
        public StorageContainer OpenContainer(string displayName, bool allowTransferBetweenPlayers,
                                               out bool isTransferredFromOtherPlayer)
        {
            isTransferredFromOtherPlayer = false;
            return new StorageContainer();
        }
    }
    public sealed class StorageContainer : IDisposable
    {
        public static string TitleLocation => Dishwasher.PublicBuild.ContentRoot
            ?? throw new InvalidOperationException("Recursos ainda não importados.");
        public string Path => Dishwasher.DesktopPaths.Saves;
        public bool IsDisposed { get; private set; }
        public StorageContainer() { Directory.CreateDirectory(Path); }
        public void Dispose() { IsDisposed = true; }
    }
}
