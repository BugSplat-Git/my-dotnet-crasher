public class Program
{
    private static Reporter reporter = new Reporter("fred", "MyDotnetCrasher", "1.0.0");

    public static void Main(string[] args)
    {
        // Set up global exception handlers
        reporter.SetupGlobalExceptionHandling();

        // Parse crash type from command line
        string crashType = args.Length > 0 ? args[0].ToLower() : "exception";

        Console.WriteLine($"MyDotnetCrasher - Simulating crash type: {crashType}");
        Console.WriteLine("Available crash types: exception, nullref, divzero, index, aggregate, unobserved");
        Console.WriteLine();

        // Simulate application with nested function calls
        StartApplication(crashType);
    }

    private static void StartApplication(string crashType)
    {
        InitializeServices(crashType);
    }

    private static void InitializeServices(string crashType)
    {
        LoadConfiguration(crashType);
    }

    private static void LoadConfiguration(string crashType)
    {
        ProcessUserRequest(crashType);
    }

    private static void ProcessUserRequest(string crashType)
    {
        ExecuteBusinessLogic(crashType);
    }

    private static void ExecuteBusinessLogic(string crashType)
    {
        PerformDataOperation(crashType);
    }

    private static void PerformDataOperation(string crashType)
    {
        TriggerCrash(crashType);
    }

    private static void TriggerCrash(string crashType)
    {
        switch (crashType)
        {
            case "exception":
                throw new Exception("Test exception for BugSplat reporting");

            case "nullref":
                ThrowNullReferenceException();
                break;

            case "divzero":
                ThrowDivideByZeroException();
                break;

            case "index":
                ThrowIndexOutOfRangeException();
                break;

            // TODO BG: Investigate catching StackOverflowException with Windows Error Reporting (WER)
            // StackOverflowException cannot be caught by normal exception handlers
            // case "stackoverflow":
            //     ThrowStackOverflowException();
            //     break;

            case "aggregate":
                ThrowAggregateException();
                break;

            case "unobserved":
                ThrowUnobservedTaskException();
                break;

            default:
                Console.WriteLine($"Unknown crash type: {crashType}");
                Console.WriteLine("Defaulting to generic exception");
                throw new Exception("Test exception for BugSplat reporting");
        }
    }

    private static void ThrowNullReferenceException()
    {
        string nullString = null;
        Console.WriteLine(nullString.Length); // Will throw NullReferenceException
    }

    private static void ThrowDivideByZeroException()
    {
        int zero = 0;
        int result = 42 / zero; // Will throw DivideByZeroException
        Console.WriteLine(result);
    }

    private static void ThrowIndexOutOfRangeException()
    {
        int[] array = new int[5];
        int value = array[10]; // Will throw IndexOutOfRangeException
        Console.WriteLine(value);
    }

    // TODO BG: Investigate catching StackOverflowException with Windows Error Reporting (WER)
    // private static void ThrowStackOverflowException()
    // {
    //     RecursiveMethod(0);
    // }

    // private static void RecursiveMethod(int depth)
    // {
    //     Console.WriteLine($"Depth: {depth}");
    //     RecursiveMethod(depth + 1); // Infinite recursion
    // }

    private static void ThrowAggregateException()
    {
        var exceptions = new List<Exception>
        {
            new InvalidOperationException("First operation failed"),
            new ArgumentException("Invalid argument provided"),
            new TimeoutException("Operation timed out")
        };
        throw new AggregateException("Multiple errors occurred", exceptions);
    }

    private static void ThrowUnobservedTaskException()
    {
        // Create a task that will throw an exception but never get awaited/observed
        Console.WriteLine("Creating unobserved task that will throw an exception...");
        
        // Use a method scope to ensure the task reference goes out of scope
        CreateUnobservedTask();
        
        // Give the task time to complete and throw
        Thread.Sleep(500);
        
        Console.WriteLine("Forcing garbage collection to trigger UnobservedTaskException...");
        
        // Force garbage collection to trigger task finalization
        // UnobservedTaskException only fires when the task is garbage collected
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Wait for the exception handler to complete
        Thread.Sleep(2000);
        
        Console.WriteLine("Unobserved exception handling complete!");
    }
    
    private static void CreateUnobservedTask()
    {
        // Create task in separate method so it goes out of scope and becomes eligible for GC
        Task.Run(() =>
        {
            throw new InvalidOperationException("This is an unobserved task exception - the task was never awaited!");
        });
    }
}