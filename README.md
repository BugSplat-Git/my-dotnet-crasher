# MyDotnetCrasher

A sample .NET application for generating various types of crash reports and testing BugSplat crash reporting integration.

## Overview

MyDotnetCrasher is a demonstration application that simulates different types of .NET exceptions and crashes, automatically capturing them with BugSplat's crash reporting service. This project is useful for:

- Testing BugSplat integration in .NET applications
- Demonstrating various crash types and stack traces
- Learning about exception handling and crash reporting workflows
- Validating symbol upload and crash analysis features

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- A [BugSplat](https://www.bugsplat.com/) account
- Windows operating system (for symbol uploads)

## Getting Started

### 1. Clone the Project

```bash
git clone https://github.com/BugSplat-Git/my-dotnet-crasher.git
cd my-dotnet-crasher
```

### 2. Configure BugSplat Settings

You need to update the BugSplat database credentials in two files:

#### Program.cs

Open `Program.cs` and update the `Reporter` initialization with your BugSplat credentials:

```csharp
private static Reporter reporter = new Reporter("your-database", "MyDotnetCrasher", "1.0.0");
```

Replace:
- `"your-database"` - Your BugSplat database name
- `"MyDotnetCrasher"` - Your application name (can be customized)
- `"1.0.0"` - Your application version

#### MyDotnetCrasher.csproj

Open `MyDotnetCrasher.csproj` and update the symbol upload configuration in the `UploadSymbols` target:

```xml
<Exec Command=".\Tools\symbol-upload-windows.exe -b your-database -a MyDotnetCrasher -v 1.0.0 -u your-email@example.com -p your-password -f &quot;**/*.{pdb,exe,dll}&quot; -d &quot;./bin&quot;"/>
```

Replace:
- `-b your-database` - Your BugSplat database name
- `-a MyDotnetCrasher` - Your application name (should match Program.cs)
- `-v 1.0.0` - Your application version (should match Program.cs)
- `-u your-email@example.com` - Your BugSplat login email
- `-p your-password` - Your BugSplat password

> **Note:** The symbol upload step automatically runs after each build to ensure your crash reports include file names and line numbers.

### 3. Build the Project

Build the application using the .NET CLI:

```bash
dotnet build
```

This will:
1. Restore NuGet packages (including [BugSplatDotNetStandard](https://github.com/BugSplat-Git/bugsplat-dotnet-standard))
2. Compile the application
3. Automatically upload debug symbols (PDB files) to BugSplat

The executable will be located at: `bin\Debug\net8.0\MyDotnetCrasher.exe`

## Generating Crash Reports

The application supports multiple crash types that can be triggered via command-line arguments.

### Available Crash Types

Run the application with one of the following crash type arguments:

```bash
# Generic exception (default if no argument provided)
dotnet run exception

# Null reference exception
dotnet run nullref

# Divide by zero exception
dotnet run divzero

# Index out of range exception
dotnet run index

# Aggregate exception (multiple errors)
dotnet run aggregate

# Unobserved task exception (async exception not awaited)
dotnet run unobserved
```

Or run the compiled executable directly:

```bash
.\bin\Debug\net8.0\MyDotnetCrasher.exe nullref
```

### What Happens During a Crash

When a crash occurs, the application will:

1. Catch the exception using global exception handlers
2. Print exception details to the console
3. Generate a Windows minidump file
4. Upload the crash report and minidump to BugSplat
5. Exit with error code 1

## Viewing Crashes on BugSplat

After generating crash reports, you can view and analyze them on the BugSplat dashboard:

### 1. Access the Dashboard

1. Log in to your [BugSplat](https://app.bugsplat.com/v2/dashboard) account
2. Select your database from the dropdown menu

### 2. View Crash Reports

Navigate to the [**Crashes**](https://app.bugsplat.com/v2/crashes) page to see all reported crashes. You'll see:

- **Crash ID** - Unique identifier for each crash
- **Application** - "MyDotnetCrasher"
- **Version** - The version you configured (e.g., "1.0.0")
- **Exception Type** - The type of exception that occurred
- **Date/Time** - When the crash occurred
- **User** - User identifier (if configured)

### 3. Analyze Individual Crashes

Click on the **ID** of any crash to view detailed information:

- **Call Stack** - Full stack trace with file names and line numbers (thanks to uploaded symbols)
- **Exception Message** - The error message from the exception
- **Minidump** - Download the native minidump for advanced debugging
- **System Information** - OS version, .NET version, etc.
- **Custom Metadata** - Any additional data attached to the crash

### 4. Group Similar Crashes

BugSplat automatically groups similar crashes together, making it easy to:

- Identify which crashes affect the most users
- Track crash trends over time
- Prioritize bug fixes based on impact

## Project Structure

```
my-dotnet-crasher/
├── Program.cs                      # Main application entry point and crash simulation
├── Reporter.cs                     # BugSplat integration and crash reporting logic
├── MyDotnetCrasher.csproj          # Project configuration and symbol upload
├── Tools/
│   └── symbol-upload-windows.exe   # BugSplat symbol upload utility
└── bin/
    └── Debug/
        └── net8.0/                 # Build output and executable
```

## Key Features

- **Multiple Crash Types** - Demonstrates 6 different exception scenarios including unobserved task exceptions
- **Automatic Symbol Upload** - PDB files uploaded on every build
- **Minidump Generation** - Creates native Windows minidumps for detailed debugging
- **Comprehensive Exception Handling** - Catches both synchronous and asynchronous unhandled exceptions
- **Nested Call Stack** - Simulates realistic application structure for better stack traces

## Troubleshooting

### Symbols Not Appearing in Stack Traces

- Verify the symbol upload completed successfully during build
- Check that database name, application name, and version match between Program.cs and MyDotnetCrasher.csproj
- Ensure you're using the correct BugSplat credentials

### Crashes Not Appearing in Dashboard

- Verify your BugSplat credentials are correct
- Check your internet connection
- Look for error messages in the console output
- Ensure the database name is correct

### Symbol Upload Fails

- Verify the `Tools\symbol-upload-windows.exe` file exists
- Check that your BugSplat login credentials are correct
- Ensure you have an active internet connection

## Learn More

- [BugSplat Documentation](https://docs.bugsplat.com/)
- [BugSplat .NET SDK](https://github.com/BugSplat-Git/bugsplat-dotnet-standard)
- [Symbol Upload Guide](https://docs.bugsplat.com/introduction/development/working-with-symbol-files)

## License

This is a sample application provided for demonstration purposes.
