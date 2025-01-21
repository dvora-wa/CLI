using System.CommandLine;
using System.IO.Compression;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Root command for File Bundle CLI");

        // יצירת פקודת bundle
        var bundleCommand = new Command("bundle", "Bundle code files to a single file");
        rootCommand.AddCommand(bundleCommand);

        // הגדרת אפשרות השפות
        var bundleOptionLanguage = new Option<List<string>>("--language", "List of programming languages. If 'all' is chosen, all code files will be included.") { IsRequired = true };
        bundleCommand.AddOption(bundleOptionLanguage);

        // הגדרת אפשרות הפלט
        var bundleOptionOutput = new Option<FileInfo>("--output", "Output file name for the bundle.") { IsRequired = true };
        bundleCommand.AddOption(bundleOptionOutput);

        // הגדרת אפשרות ההערה
        var bundleOptionNote = new Option<bool>("--note", "Whether to include the source code as a comment in the bundle file.");
        bundleCommand.AddOption(bundleOptionNote);

        // הגדרת אפשרות הסידור
        var bundleOptionSort = new Option<string>("--sort", "Order of copying code files, by alphabetical order of the filename or by code type.");
        bundleCommand.AddOption(bundleOptionSort);

        // הגדרת אפשרות הסרת שורות ריקות
        var bundleOptionRemoveEmptyLines = new Option<bool>("--remove-empty-lines", "Whether to remove empty lines.");
        bundleCommand.AddOption(bundleOptionRemoveEmptyLines);

        // הגדרת אפשרות המחבר
        var bundleOptionAuthor = new Option<string>("--author", "Record the name of the file creator.");
        bundleCommand.AddOption(bundleOptionAuthor);

        // הגדרת הידולים עבור כל אפשרות
        bundleCommand.SetHandler(HandleBundleCommand, bundleOptionLanguage, bundleOptionOutput, bundleOptionNote, bundleOptionSort, bundleOptionRemoveEmptyLines, bundleOptionAuthor);

        // יצירת פקודת create-rsp
        var createRspCommand = new Command("create-rsp", "Create a response file with the command options.");
        rootCommand.AddCommand(createRspCommand);

        createRspCommand.SetHandler(async () =>
        {
            // קלט מהמשתמש עבור כל אפשרות
            var languages = await PromptUser("Enter the programming languages (comma separated or 'all'):");
            var outputFileName = await PromptUser("Enter the output file name:");
            var includeSourceCode = bool.Parse(await PromptUser("Include source code as comment? (true/false):"));
            var sortOption = await PromptUser("Sort files by (filename/type):");
            var removeEmptyLines = bool.Parse(await PromptUser("Remove empty lines? (true/false):"));
            var authorName = await PromptUser("Enter author's name:");

            // יצירת פקודת תגובה
            var responseFilePath = "response.rsp";
            var commandText = $"dotnet run --bundle --language {languages} --output {outputFileName} " +
                              $"--note {includeSourceCode} --sort {sortOption} " +
                              $"--remove-empty-lines {removeEmptyLines} --author \"{authorName}\"\n";

            await File.WriteAllTextAsync(responseFilePath, commandText);
            Console.WriteLine($"Response file created at: {responseFilePath}");
        });

        return await rootCommand.InvokeAsync(args);
    }

    // פונקציה לטיפול בפקודת bundle
    static void HandleBundleCommand(List<string> languages, FileInfo output, bool note, string sort, bool removeEmptyLines, string author)
    {
        if (languages == null || languages.Count == 0 || (languages.Count == 1 && languages[0].ToLower() != "all" && !IsValidLanguage(languages[0])))
        {
            Console.WriteLine("Error: At least one programming language or the word 'all' must be provided.");
            return;
        }

        HashSet<string> filesToArchive = new HashSet<string>();

        // איסוף קבצים לפי שפה
        foreach (var lang in languages)
        {
            if (lang.ToLower() != "all" && IsValidLanguage(lang))
            {
                var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.AllDirectories)
                                     .Where(file => IsValidFile(file) && file.EndsWith("." + lang.ToLower()));
                filesToArchive.UnionWith(files);
            }
            else if (lang.ToLower() == "all")
            {
                var allFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.AllDirectories)
                                         .Where(file => IsValidFile(file));
                filesToArchive.UnionWith(allFiles);
            }
        }

        // טיפול בקובץ הפלט
        if (filesToArchive.Count > 0)
        {
            try
            {
                using (var zip = ZipFile.Open(output.FullName, ZipArchiveMode.Create))
                {
                    foreach (var file in filesToArchive)
                    {
                        zip.CreateEntryFromFile(file, Path.GetFileName(file));
                    }
                }
                Console.WriteLine($"Files have been archived into: {output.FullName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating zip file: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("No files to archive.");
        }

        // טיפול בהערות מקור הקוד
        if (note)
        {
            foreach (var file in filesToArchive)
            {
                var sourceCode = File.ReadAllText(file);
                if (removeEmptyLines)
                {
                    sourceCode = RemoveEmptyLines(sourceCode);
                }
                File.AppendAllText(output.FullName, $"// Source code from {file}\n{sourceCode}\n\n");
            }
            Console.WriteLine("Source code included as comments in the bundle.");
        }

        // טיפול בשם המחבר
        if (!string.IsNullOrEmpty(author))
        {
            File.AppendAllText(output.FullName, $"// Author: {author}\n");
            Console.WriteLine($"Author recorded: {author}");
        }
    }

    // פונקציה שמסירה שורות ריקות
    static string RemoveEmptyLines(string sourceCode)
    {
        return string.Join("\n", sourceCode.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    // פונקציה לבדיקת שפה תקפה
    static bool IsValidLanguage(string lang)
    {
        return Enum.TryParse(typeof(ProgrammingLanguages), lang, true, out _);
    }

    // פונקציה לבדוק אם קובץ תקין
    static bool IsValidFile(string filePath)
    {
        var excludedDirectories = new[] { "bin", "debug" };
        if (excludedDirectories.Any(dir => filePath.Contains(Path.Combine(Directory.GetCurrentDirectory(), dir), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var extension = Path.GetExtension(filePath).ToLower();
        var languageExtensions = new Dictionary<ProgrammingLanguages, List<string>>
        {
            { ProgrammingLanguages.C, new List<string> { ".c" } },
            { ProgrammingLanguages.CPlusPlus, new List<string> { ".cpp", ".cxx" } },
            { ProgrammingLanguages.CSharp, new List<string> { ".cs" } },
            { ProgrammingLanguages.Java, new List<string> { ".java" } },
            { ProgrammingLanguages.Python, new List<string> { ".py" } },
            { ProgrammingLanguages.JavaScript, new List<string> { ".js" } },
            { ProgrammingLanguages.Ruby, new List<string> { ".rb" } },
            { ProgrammingLanguages.PHP, new List<string> { ".php" } },
            { ProgrammingLanguages.Swift, new List<string> { ".swift" } },
            { ProgrammingLanguages.Go, new List<string> { ".go" } },
            { ProgrammingLanguages.Kotlin, new List<string> { ".kt", ".kts" } },
            { ProgrammingLanguages.Rust, new List<string> { ".rs" } },
            { ProgrammingLanguages.TypeScript, new List<string> { ".ts" } },
            { ProgrammingLanguages.Scala, new List<string> { ".scala" } },
            { ProgrammingLanguages.Perl, new List<string> { ".pl" } },
            { ProgrammingLanguages.Haskell, new List<string> { ".hs" } },
            { ProgrammingLanguages.Elixir, new List<string> { ".ex", ".exs" } },
            { ProgrammingLanguages.Dart, new List<string> { ".dart" } },
            { ProgrammingLanguages.Lua, new List<string> { ".lua" } },
            { ProgrammingLanguages.ObjectiveC, new List<string> { ".m" } },
            { ProgrammingLanguages.R, new List<string> { ".r" } },
            { ProgrammingLanguages.Shell, new List<string> { ".sh" } },
            { ProgrammingLanguages.SQL, new List<string> { ".sql" } },
            { ProgrammingLanguages.Groovy, new List<string> { ".groovy", ".gvy" } },
            { ProgrammingLanguages.FSharp, new List<string> { ".fs", ".fsi" } },
            { ProgrammingLanguages.VisualBasic, new List<string> { ".vb" } },
            { ProgrammingLanguages.Assembly, new List<string> { ".asm", ".s" } }
        };

        return languageExtensions.Any(language => language.Value.Contains(extension));
    }

    // פונקציה לבקש קלט מהמשתמש
    static async Task<string> PromptUser(string message)
    {
        Console.WriteLine(message);
        var input = await Task.FromResult(Console.ReadLine());
        return string.IsNullOrEmpty(input) ? string.Empty : input; // לוודא שהקלט לא יהיה null
    }

    enum ProgrammingLanguages
    {
        C,
        CPlusPlus,
        CSharp,
        Java,
        Python,
        JavaScript,
        Ruby,
        PHP,
        Swift,
        Go,
        Kotlin,
        Rust,
        TypeScript,
        Scala,
        Perl,
        Haskell,
        Elixir,
        Dart,
        Lua,
        ObjectiveC,
        R,
        Shell,
        SQL,
        Groovy,
        FSharp,
        VisualBasic,
        Assembly
    }
}


//var rootCommand = new RootCommand("Root command for File Bundle CLI");

//var bundleCommand = new Command("bundle", "Bundle code files to a single file");
//rootCommand.AddCommand(bundleCommand);

//var bundleOptionLanguage = new Option<List<string>>("--language", "File path and name");
//bundleCommand.AddOption(bundleOptionLanguage);

//var bundleOptionRouting = new Option<FileInfo>("--output", "File path and name");
//bundleCommand.AddOption(bundleOptionRouting);

//var bundleOptionSourceCode = new Option<bool>("--note",description: "אם לרשום את מקור הקוד כהערה בקובץ ה-bundle.");
//bundleCommand.AddOption(bundleOptionSourceCode);

////var bundleOption3 = new Option<string>( "--sort", description: "סדר העתקת קבצי הקוד, לפי א\"ב של שם הקובץ או לפי סוג הקוד.");
////bundleCommand.AddOption();

////var bundleOption1 = new Option<bool>("--remove-empty-lines",description: "האם למחוק שורות ריקות.");
////bundleCommand.AddOption();

////var bundleOption = new Option<bool>("--remove-empty-lines", description: "האם למחוק שורות ריקות.");
////bundleCommand.AddOption();

////var bundleOption = new Option<string>( "--author",description: "רישום שם יוצר הקובץ.");
////bundleCommand.AddOption();


//bundleCommand.SetHandler((output) =>
//{
//    if (bundleOptionLanguage == null || (language.Count == 1 && language[0].ToLower() != "all"))
//    {
//        Console.WriteLine("שגיאה: יש לספק לפחות שפת תכנות אחת או את המילה 'all'.");
//        return;
//    }
//    //try
//    //{
//    //    File.Create();
//    //    Console.WriteLine("File was created");
//    //}
//    //catch (DirectoryNotFoundException ex)
//    //{
//    //    Console.WriteLine("Error: File is invalid");
//    //}
//    //if (output[0] == "all")




//}, bundleOptionLanguage);



//bundleCommand.SetHandler((output) =>
//{
//    try
//    {
//        File.Create(output.FullName);
//        Console.WriteLine("File was created");
//    }
//    catch (DirectoryNotFoundException ex)
//    {
//        Console.WriteLine("Error: File is invalid");
//    }

//}, bundleOptionRouting);

//rootCommand.InvokeAsync(args);