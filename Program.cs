public class Program
{
    private static Reporter reporter = new Reporter("fred", "MyDotnetCrasher", "1.0.0");

    public static void Main(string[] args)
    {
        // Set up global exception handlers
        reporter.SetupGlobalExceptionHandling();

        // Your application logic
        throw new Exception("Test exception for BugSplat reporting");
    }
}