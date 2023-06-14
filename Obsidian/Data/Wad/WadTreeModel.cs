﻿using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance.Helpers;
using LeagueToolkit.Core.Wad;
using Obsidian.Services;
using Obsidian.Shared;
using Serilog;
using PathIO = System.IO.Path;

namespace Obsidian.Data.Wad;

public class WadTreeModel : IWadTreeParent, IDisposable {
    public HashtableService Hashtable { get; }
    public Config Config { get; }

    public IWadTreeParent Parent => null;
    public int Depth => 0;
    public string Name => string.Empty;
    public string Path => string.Empty;
    public bool IsWadArchive => false;

    public bool UseRegexFilter { get; set; }
    public string Filter { get; set; }

    public WadFilePreviewType CurrentPreviewType { get; set; }

    public Dictionary<string, WadTreeItemModel> Items { get; set; } = new();

    public IEnumerable<WadTreeFileModel> CheckedFiles =>
        this.TraverseFlattenedCheckedItems()
            .Where(x => x is WadTreeFileModel)
            .Select(x => x as WadTreeFileModel);

    public WadTreeFileModel SelectedFile => this.SelectedFiles.FirstOrDefault();

    public IEnumerable<WadTreeFileModel> SelectedFiles =>
        this.TraverseFlattenedItems()
            .Where(x => x.IsSelected && x is WadTreeFileModel)
            .Select(x => x as WadTreeFileModel);

    private readonly Dictionary<string, WadFile> _mountedWadFiles = new();

    public bool IsDisposed { get; private set; }

    public WadTreeModel(HashtableService hashtable, Config config, IEnumerable<string> wadFiles) {
        Guard.IsNotNull(wadFiles, nameof(wadFiles));

        this.Hashtable = hashtable;
        this.Config = config;

        foreach (string wadFilePath in wadFiles) {
            try {
                MountWadFile(wadFilePath);
            } catch (Exception exception) {
                Log.Error(exception, "Failed to mount Wad file: {WadFile}", wadFilePath);
            }
        }

        SortItems();
    }

    private void MountWadFile(string path) {
        WadFile wad = new(path);
        string relativeWadPath = PathIO
            .GetRelativePath(this.Config.GameDataDirectory, path)
            .Replace(PathIO.DirectorySeparatorChar, '/');

        this._mountedWadFiles.Add(relativeWadPath, wad);

        CreateTreeForWadFile(wad, relativeWadPath);
    }

    public void CreateTreeForWadFile(WadFile wad, string wadFilePath) {
        IEnumerable<string> wadFilePathComponents = wadFilePath.Split('/');

        foreach (var (_, chunk) in wad.Chunks) {
            string path = this.Hashtable.TryGetChunkPath(chunk, out path) switch {
                true => path,
                false => HashtableService.GuessChunkPath(chunk, wad),
            };

            this.AddWadFile(wadFilePathComponents.Concat(path.Split('/')), wad, chunk);
        }
    }

    public void SortItems() {
        Log.Information($"Sorting wad tree");

        this.Items = new(this.Items.OrderBy(x => x.Value));

        foreach (var (_, item) in this.Items) {
            if (item.Type is WadTreeItemType.Directory)
                item.SortItems();
        }
    }

    public void Dispose() {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if (this.IsDisposed)
            return;

        if (disposing)
            foreach (var (_, wad) in this._mountedWadFiles)
                wad?.Dispose();

        this.IsDisposed = true;
    }
}

public enum WadFilePreviewType {
    None,
    Image,
    Viewport,
    Text
}