namespace DCE.FileSorter;

public class CustomLineComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        var text1 = x?[(x.IndexOf('.') + 2)..];
        var text2 = y?[(y.IndexOf('.') + 2)..];

        var compareResult = string.Compare(text1, text2, StringComparison.InvariantCulture);
        if (compareResult == 0)
        {
            var number1 = int.Parse(x?[..((x?.IndexOf('.') ?? 0))] ?? "0");
            var number2 = int.Parse(y?[..((y?.IndexOf('.') ?? 0))] ?? "0");

            return number1.CompareTo(number2);
        }
        return compareResult;
    }
}
