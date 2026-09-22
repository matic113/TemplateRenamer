using System.Text;

namespace TemplateRenamer;

public static class Program
{
    private static readonly string[] ExcludedDirectories = [$".git", ".vs", ".idea", "bin", "obj"];
    public static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // --- Argument and Directory Validation (No changes here) ---
        if (args.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Please provide the path to the template directory.");
            Console.WriteLine("""
                                Example: renamer "C:\Path\To\Your\Template"
                                """);
            Console.ResetColor();
            return;
        }
        var targetDirectory = args[0];
        if (!Directory.Exists(targetDirectory))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: The directory '{targetDirectory}' does not exist.");
            Console.ResetColor();
            return;
        }

        // --- Header ---
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(new string('=', 40));
        Console.WriteLine(".NET Solution Renamer");
        Console.WriteLine(new string('=', 40));
        Console.ResetColor();

        string? suggestedOldName = null;
        try
        {
            var slnFiles = Directory.GetFiles(targetDirectory, "*.sln", SearchOption.AllDirectories);
            if (slnFiles.Length == 1)
            {
                suggestedOldName = Path.GetFileNameWithoutExtension(slnFiles[0]);
                Console.WriteLine($"  - Found solution: {Path.GetFileName(slnFiles[0])}");
                Console.WriteLine($"  - Suggested project name: '{suggestedOldName}'");
            }
            else
            {
                Console.WriteLine("  - Found multiple or no solution files. No name suggested.");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"An error occurred while searching for files: {ex.Message}");
            Console.ResetColor();
        }
        Console.WriteLine("");

        var oldName = string.Empty;
        while (string.IsNullOrWhiteSpace(oldName))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(suggestedOldName != null ? $"Old project name (default: {suggestedOldName}): " : "Old project name: ");
            Console.ResetColor();
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input) && suggestedOldName != null)
            {
                oldName = suggestedOldName; // Use suggestion if user hits Enter
            }
            else
            {
                oldName = input;
            }
        }

        var newName = string.Empty;
        while (string.IsNullOrWhiteSpace(newName))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("New project name: ");
            Console.ResetColor();
            newName = Console.ReadLine();
        }

        RenameDirectories(targetDirectory, oldName, newName);
        Console.WriteLine("\t- Renaming directories.");

        RenameFiles(targetDirectory, oldName, newName);
        Console.WriteLine("\t- Renaming files.");

        RenameFileContents(targetDirectory, oldName, newName);
        Console.WriteLine("\t- Renaming file contents.");

        var gitDirectoryPath = Path.Combine(targetDirectory, ".git");

        if (Directory.Exists(gitDirectoryPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("\tFound a git repository do you want to delete the existing git repository for a fresh start? (y/n): ");
            Console.ResetColor();

            var response = Console.ReadLine();

            if (response?.Trim().ToLower().StartsWith("y") ?? false)
            {
                DeleteGitDirectorySafely(gitDirectoryPath);
                Console.WriteLine("\t.git directory deleted.");
            }
            else
            {
                Console.WriteLine("\tSkipping .git directory deletion.");
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine();
        Console.WriteLine($"Rename complete! Project '{oldName}' is now '{newName}'.");
        Console.ResetColor();

        Console.Write("Press any key to exit...");
        Console.ReadKey();
    }

    private static void RenameDirectories(string currentDirectory, string originalName, string newName)
    {
        // First, get a list of subdirectories to recurse into.
        var subDirectories = Directory.GetDirectories(currentDirectory);
        foreach (var dir in subDirectories)
        {
            if (ExcludedDirectories.Contains(Path.GetFileName(dir)))
            {
                continue;
            }

            // 1. Recurse into the subdirectory FIRST.
            // This ensures everything inside it is renamed before we rename the directory itself.
            RenameDirectories(dir, originalName, newName);
        }

        // Now that all sub-folders have been processed, we can safely rename the folders in THIS directory.
        // We get a fresh list because their names might have changed in the recursive calls.
        subDirectories = Directory.GetDirectories(currentDirectory);
        foreach (var dir in subDirectories)
        {
            if (ExcludedDirectories.Contains(Path.GetFileName(dir)))
            {
                continue;
            }

            // 2. Rename the directory at the current level.
            var dirName = Path.GetFileName(dir);
            if (!dirName.Contains(originalName, StringComparison.Ordinal))
            {
                continue;
            }

            var newDirectoryName = Path.Combine(Path.GetDirectoryName(dir)!, dirName.Replace(originalName, newName));

            if (newDirectoryName != dir)
            {
                try
                {
                    Directory.Move(dir, newDirectoryName);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\t[Warning] Could not rename directory '{dirName}': {ex.Message}");
                    Console.ResetColor();
                }
            }
        }
    }

    private static void RenameFiles(string currentDirectory, string originalName, string newName)
    {
        var files = Directory.GetFiles(currentDirectory);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (!fileName.Contains(originalName, StringComparison.Ordinal))
            {
                continue;
            }

            var newFileName = Path.Combine(Path.GetDirectoryName(file)!, fileName.Replace(originalName, newName));
            if (newFileName != file)
            {
                try
                {
                    File.Move(file, newFileName);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\t[Warning] Could not rename file '{fileName}': {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        var subDirectories = Directory.GetDirectories(currentDirectory);
        foreach (var directory in subDirectories)
        {
            if (ExcludedDirectories.Contains(Path.GetFileName(directory)))
            {
                continue;
            }
            RenameFiles(directory, originalName, newName);
        }
    }

    private static void RenameFileContents(string currentDirectory, string originalName, string newName)
    {
        string[] fileToSkipExtensions = [".exe", ".dll", ".runtimeconfig.json", ".json"];
        var files = Directory.GetFiles(currentDirectory);
        foreach (var file in files)
        {
            if (fileToSkipExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;

            try
            {
                var contents = File.ReadAllText(file);
                if (!contents.Contains(originalName, StringComparison.Ordinal))
                {
                    continue;
                }

                var newContents = contents.Replace(originalName, newName);

                var attributes = File.GetAttributes(file);
                var isReadOnly = (attributes & FileAttributes.ReadOnly) != 0;
                var isHidden = (attributes & FileAttributes.Hidden) != 0;

                if (isReadOnly || isHidden)
                {
                    File.SetAttributes(file, attributes & ~(FileAttributes.ReadOnly | FileAttributes.Hidden));
                }

                File.WriteAllText(file, newContents, Encoding.UTF8);

                if (isReadOnly || isHidden)
                {
                    File.SetAttributes(file, attributes);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\t[Warning] Could not update contents of '{Path.GetFileName(file)}': {ex.Message}");
                Console.ResetColor();
            }
        }

        var subDirectories = Directory.GetDirectories(currentDirectory);
        foreach (var directory in subDirectories)
        {
            if (ExcludedDirectories.Contains(Path.GetFileName(directory)))
            {
                continue;
            }
            RenameFileContents(directory, originalName, newName);
        }
    }

    private static void DeleteGitDirectorySafely(string gitDirectoryPath)
    {
        // First, get all files within the .git directory and its subdirectories
        var files = Directory.GetFiles(gitDirectoryPath, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var attributes = File.GetAttributes(file);
            // Check if the file is read-only
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                // If so, remove the read-only attribute
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
        }

        // Now that no files are read-only, the directory can be safely deleted
        Directory.Delete(gitDirectoryPath, true);
    }
}