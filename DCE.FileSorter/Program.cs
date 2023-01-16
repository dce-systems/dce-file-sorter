using DCE.FileSorter;

Console.WriteLine("---------------------------------");
Console.WriteLine("File sorter - DCE-Systems");
Console.WriteLine("---------------------------------");

Console.WriteLine("Provide the INPUT filename (or press ENTER if you want to use default name: 'input.txt')");
var inputFilename = Console.ReadLine();
if (string.IsNullOrWhiteSpace(inputFilename))
{
    inputFilename = "input.txt";
}

Console.WriteLine("Provide the OUTPUT filename (or press ENTER if you want to use default name: 'output.txt')");
var outputFilename = Console.ReadLine();
if (string.IsNullOrWhiteSpace(outputFilename))
{
    outputFilename = "output.txt";
}

var fileSorter = FileSorterBuilder.Build();

var inputFile = new FileStream(inputFilename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

var outputFile = new FileStream(outputFilename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

await fileSorter.Sort(inputFile, outputFile, CancellationToken.None);

Console.WriteLine($"Done!");

Console.ReadKey();