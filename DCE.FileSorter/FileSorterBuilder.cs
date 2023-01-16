namespace DCE.FileSorter;

internal static class FileSorterBuilder
{
    public static FileSorter Build()
    {
        var splitFileProgressHandler = new Progress<double>(x =>
        {
            var percentage = x * 100;
            Console.WriteLine($"Split progress: {percentage:##.##}%");
        });
        var sortFilesProgressHandler = new Progress<double>(x =>
        {
            var percentage = x * 100;
            Console.WriteLine($"Sort progress: {percentage:##.##}%");
        });
        var mergeFilesProgressHandler = new Progress<double>(x =>
        {
            var percentage = x * 100;
            Console.WriteLine($"Merge progress: {percentage:##.##}%");
        });

        var externalMergeSorter = new FileSorter(new FileSorterOptions
        {
            FileLocation = Path.Combine(Directory.GetCurrentDirectory(), "temp"),

            Split = new FileSorterSplitOptions
            {
                ProgressHandler = splitFileProgressHandler
            },
            Sort = new FileSorterSortOptions
            {
                ProgressHandler = sortFilesProgressHandler
            },
            Merge = new FileSorterMergeOptions
            {
                ProgressHandler = mergeFilesProgressHandler
            }
        });

        return externalMergeSorter;
    }
}
