namespace DCE.FileSorter;

public class FileSorter
{
    private long _maxUnsortedLines;
    private string[] _unsortedLines;
    private double _totalFilesToMerge;
    private int _mergeFilesProcessed;
    private readonly FileSorterOptions _options;
    private const string UnsortedFileExtension = ".unsorted";
    private const string SortedFileExtension = ".sorted";
    private const string TempFileExtension = ".tmp";

    public FileSorter() : this(new FileSorterOptions()) { }

    public FileSorter(FileSorterOptions options)
    {
        _totalFilesToMerge = 0;
        _mergeFilesProcessed = 0;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _unsortedLines = Array.Empty<string>();
    }

    public async Task Sort(Stream source, Stream target, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.FileLocation);

        var files = await SplitFile(source, cancellationToken);
        _unsortedLines = new string[_maxUnsortedLines];
        if (files.Count == 1)
        {
            var unsortedFilePath = Path.Combine(_options.FileLocation, files.First());
            await SortFile(File.OpenRead(unsortedFilePath), target);
            return;
        }
        var sortedFiles = await SortFiles(files);

        var done = false;
        var size = _options.Merge.FilesPerRun;
        _totalFilesToMerge = sortedFiles.Count;
        var result = sortedFiles.Count / size;

        while (!done)
        {
            if (result <= 0)
            {
                done = true;
            }
            _totalFilesToMerge += result;
            result /= size;
        }

        await MergeFiles(sortedFiles, target, cancellationToken);
    }

    private async Task<IReadOnlyCollection<string>> SplitFile(
        Stream sourceStream,
        CancellationToken cancellationToken)
    {
        var fileSize = _options.Split.FileSize;
        var buffer = new byte[fileSize];
        var extraBuffer = new List<byte>();
        var filenames = new List<string>();
        var totalFiles = Math.Ceiling(sourceStream.Length / (double)_options.Split.FileSize);

        await using (sourceStream)
        {
            var currentFile = 0L;
            while (sourceStream.Position < sourceStream.Length)
            {
                var totalLines = 0;
                var runBytesRead = 0;
                while (runBytesRead < fileSize)
                {
                    var value = sourceStream.ReadByte();
                    if (value == -1)
                    {
                        break;
                    }

                    var @byte = (byte)value;
                    buffer[runBytesRead] = @byte;
                    runBytesRead++;
                    if (@byte == _options.Split.NewLineSeparator)
                    {
                        totalLines++;
                    }
                }

                var extraByte = buffer[fileSize - 1];

                while (extraByte != _options.Split.NewLineSeparator)
                {
                    var flag = sourceStream.ReadByte();
                    if (flag == -1)
                    {
                        break;
                    }
                    extraByte = (byte)flag;
                    extraBuffer.Add(extraByte);
                }

                var filename = $"{++currentFile}.unsorted";
                await using var unsortedFile = File.Create(Path.Combine(_options.FileLocation, filename));
                await unsortedFile.WriteAsync(buffer.AsMemory(0, runBytesRead), cancellationToken);
                if (extraBuffer.Count > 0)
                {
                    totalLines++;
                    await unsortedFile.WriteAsync(extraBuffer.ToArray(), 0, extraBuffer.Count, cancellationToken);
                }

                if (totalLines > _maxUnsortedLines)
                {
                    _maxUnsortedLines = totalLines;
                }

                _options.Split.ProgressHandler?.Report(currentFile / totalFiles);
                filenames.Add(filename);
                extraBuffer.Clear();
            }

            return filenames;
        }
    }

    private async Task<IReadOnlyList<string>> SortFiles(
        IReadOnlyCollection<string> unsortedFiles)
    {
        var sortedFiles = new List<string>(unsortedFiles.Count);
        double totalFiles = unsortedFiles.Count;
        foreach (var unsortedFile in unsortedFiles)
        {
            var sortedFilename = unsortedFile.Replace(UnsortedFileExtension, SortedFileExtension);
            var unsortedFilePath = Path.Combine(_options.FileLocation, unsortedFile);
            var sortedFilePath = Path.Combine(_options.FileLocation, sortedFilename);
            await SortFile(File.OpenRead(unsortedFilePath), File.OpenWrite(sortedFilePath));
            File.Delete(unsortedFilePath);
            sortedFiles.Add(sortedFilename);
            _options.Sort.ProgressHandler?.Report(sortedFiles.Count / totalFiles);
        }
        return sortedFiles;
    }

    private async Task SortFile(Stream unsortedFile, Stream target)
    {
        using var streamReader = new StreamReader(unsortedFile, bufferSize: _options.Sort.InputBufferSize);
        var counter = 0;
        while (!streamReader.EndOfStream)
        {
            _unsortedLines[counter++] = (await streamReader.ReadLineAsync())!;
        }

        Array.Sort(_unsortedLines, _options.Sort.Comparer);
        await using var streamWriter = new StreamWriter(target, bufferSize: _options.Sort.OutputBufferSize);

        foreach (var line in _unsortedLines.Where(x => x is not null))
        {
            await streamWriter.WriteLineAsync(line);
        }

        Array.Clear(_unsortedLines, 0, _unsortedLines.Length);
    }

    private async Task MergeFiles(
        IReadOnlyList<string> sortedFiles, Stream target, CancellationToken cancellationToken)
    {
        var done = false;
        while (!done)
        {
            var runSize = _options.Merge.FilesPerRun;
            var finalRun = sortedFiles.Count <= runSize;

            if (finalRun)
            {
                await Merge(sortedFiles, target, cancellationToken);
                return;
            }

            var runs = sortedFiles.Chunk(runSize);
            var chunkCounter = 0;
            foreach (var files in runs)
            {
                var outputFilename = $"{++chunkCounter}{SortedFileExtension}{TempFileExtension}";
                if (files.Length == 1)
                {
                    OverwriteTempFile(files.First(), outputFilename);
                    continue;
                }

                var outputStream = File.OpenWrite(GetFullPath(outputFilename));
                await Merge(files, outputStream, cancellationToken);
                OverwriteTempFile(outputFilename, outputFilename);

                void OverwriteTempFile(string from, string to)
                {
                    File.Move(
                        GetFullPath(from),
                        GetFullPath(to.Replace(TempFileExtension, string.Empty)), true);
                }
            }

            sortedFiles = Directory.GetFiles(_options.FileLocation, $"*{SortedFileExtension}")
                .OrderBy(x =>
                {
                    var filename = Path.GetFileNameWithoutExtension(x);
                    return int.Parse(filename);
                })
                .ToArray();

            if (sortedFiles.Count > 1)
            {
                continue;
            }

            done = true;
        }
    }

    private async Task Merge(
        IReadOnlyList<string> filesToMerge,
        Stream outputStream,
        CancellationToken cancellationToken)
    {
        var (streamReaders, lines) = await InitializeStreamReaders(filesToMerge);
        var finishedStreamReaders = new List<int>(streamReaders.Length);
        var done = false;
        await using var outputWriter = new StreamWriter(outputStream, bufferSize: _options.Merge.OutputBufferSize);

        while (!done)
        {
            lines.Sort((line1, line2) => _options.Sort.Comparer.Compare(line1.Value, line2.Value));
            var valueToWrite = lines[0].Value;
            var streamReaderIndex = lines[0].StreamReader;
            await outputWriter.WriteLineAsync(valueToWrite.AsMemory(), cancellationToken);

            if (streamReaders[streamReaderIndex].EndOfStream)
            {
                var indexToRemove = lines.FindIndex(x => x.StreamReader == streamReaderIndex);
                lines.RemoveAt(indexToRemove);
                finishedStreamReaders.Add(streamReaderIndex);
                done = finishedStreamReaders.Count == streamReaders.Length;
                _options.Merge.ProgressHandler?.Report(++_mergeFilesProcessed / _totalFilesToMerge);
                continue;
            }

            var value = await streamReaders[streamReaderIndex].ReadLineAsync();
            lines[0] = new Line { Value = value!, StreamReader = streamReaderIndex };
        }

        CleanupRun(streamReaders, filesToMerge);
    }

    private async Task<(StreamReader[] StreamReaders, List<Line> lines)> InitializeStreamReaders(
        IReadOnlyList<string> sortedFiles)
    {
        var streamReaders = new StreamReader[sortedFiles.Count];
        var lines = new List<Line>(sortedFiles.Count);
        for (var i = 0; i < sortedFiles.Count; i++)
        {
            var sortedFilePath = GetFullPath(sortedFiles[i]);
            var sortedFileStream = File.OpenRead(sortedFilePath);
            streamReaders[i] = new StreamReader(sortedFileStream, bufferSize: _options.Merge.InputBufferSize);
            var value = await streamReaders[i].ReadLineAsync();

            var line = new Line
            {
                Value = value!,
                StreamReader = i
            };
            lines.Add(line);
        }

        return (streamReaders, lines);
    }

    private void CleanupRun(StreamReader[] streamReaders, IReadOnlyList<string> filesToMerge)
    {
        for (var i = 0; i < streamReaders.Length; i++)
        {
            streamReaders[i].Dispose();
            var temporaryFilename = $"{filesToMerge[i]}.removal";
            File.Move(GetFullPath(filesToMerge[i]), GetFullPath(temporaryFilename));
            File.Delete(GetFullPath(temporaryFilename));
        }
    }

    private string GetFullPath(string filename)
    {
        return Path.Combine(_options.FileLocation, Path.GetFileName(filename));
    }
}
