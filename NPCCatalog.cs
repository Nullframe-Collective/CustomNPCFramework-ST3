namespace CustomNPCFramework
{
    public static class NPCCatalog
    {
        private static HashSet<string> _selection;

        public static int ApiVersion => 1;
        public static bool Enabled { get; set; } = true;
        public static bool IsReady => Core.IsInitialized && Core.Error == null;
        public static IReadOnlyList<NPCInfo> Items => Core.NPCInfos
            .Where(item => _selection == null || _selection.Contains(item.Id))
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();

        internal static bool Allows(NPCInfo item) => Enabled && item != null && item.IsAvailable &&
            (_selection == null || _selection.Contains(item.Id));

        public static IDisposable UseContent(string[] ids)
        {
            if (ids == null || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length ||
                ids.Any(id => !Core.NPCInfos.Any(item => item.Id == id && item.IsAvailable)))
            {
                throw new ArgumentException("The required NPC packs are unavailable.", nameof(ids));
            }

            HashSet<string> previous = _selection;
            HashSet<string> selected = new(ids, StringComparer.Ordinal);
            _selection = selected;

            return new Selection(previous, selected);
        }

        public static IDisposable Mount(string directory, string id)
        {
            NPCInfo info = NPCManifest.Read(directory);

            if (info.Id != id)
            {
                throw new System.IO.InvalidDataException("The pack ID does not match the requested ID.");
            }

            NPCInfo[] replaced = Core.NPCInfos.Where(item => item.Id == info.Id || item.Name == info.Name).ToArray();
            foreach (NPCInfo item in replaced)
            {
                Core.NPCInfos.Remove(item);
                Core.LoadedBundles.Remove(item.Name);
            }

            Core.NPCInfos.Add(info);
            Core.NPCInfos.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            return new Mounted(info, replaced);
        }

        private sealed class Mounted(NPCInfo info, NPCInfo[] replaced) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;

                _disposed = true;
                Core.NPCInfos.Remove(info);
                Core.LoadedBundles.Remove(info.Name);
                Core.NPCInfos.AddRange(replaced);
                Core.NPCInfos.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            }
        }

        private sealed class Selection : IDisposable
        {
            private readonly HashSet<string> _previous;
            private HashSet<string> _selected;

            internal Selection(HashSet<string> previous, HashSet<string> selected)
            {
                _previous = previous;
                _selected = selected;
            }

            public void Dispose()
            {
                if (_selected != null && ReferenceEquals(_selection, _selected))
                {
                    _selection = _previous;
                }

                _selected = null;
            }
        }
    }
}
