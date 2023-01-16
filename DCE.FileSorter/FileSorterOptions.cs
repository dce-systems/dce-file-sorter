namespace DCE.FileSorter;

public class FileSorterOptions
{
    public FileSorterOptions()
    {
        Split = new FileSorterSplitOptions();
        Sort = new FileSorterSortOptions();
        Merge = new FileSorterMergeOptions();
    }

    public string FileLocation { get; init; } = "temp";
    public FileSorterSplitOptions Split { get; init; }
    public FileSorterSortOptions Sort { get; init; }
    public FileSorterMergeOptions Merge { get; init; }
}

public class FileSorterSplitOptions
{
    public int FileSize { get; init; } = 2 * 1024 * 1024;
    public char NewLineSeparator { get; init; } = '\n';
    public IProgress<double> ProgressHandler { get; init; } = null!;
}

public class FileSorterSortOptions
{
    public IComparer<string> Comparer { get; init; } = new CustomLineComparer();
    public int InputBufferSize { get; init; } = 65536;
    public int OutputBufferSize { get; init; } = 65536;
    public IProgress<double> ProgressHandler { get; init; } = null!;
}

public class FileSorterMergeOptions
{
    public int FilesPerRun { get; init; } = 10;
    public int InputBufferSize { get; init; } = 65536;
    public int OutputBufferSize { get; init; } = 65536;

    public IProgress<double> ProgressHandler { get; init; } = null!;
}
