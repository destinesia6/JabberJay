using System.Diagnostics;
using SevenZipExtractor;

namespace Installer;

class Program
{
    public static int Main(string[] args)
    {
        /*if (args.Length == 0) // Check if file path is passed
        {
            Console.WriteLine("Error: No update zip file path provided.");
            return 1;
        }*/

        string downloadZipPath = args[0];
        
        string installationDirectory = Environment.CurrentDirectory; // Sets installationDirectory to JabberJay folder
        Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory; // Moves current directory to installation folder
        string tempExtractionPath = Path.Combine(Path.GetTempPath(), "TempJJPath");
        
        Console.WriteLine($"Downloaded ZIP: {downloadZipPath}");
        Console.WriteLine($"Installation Dir: {installationDirectory}");
        Console.WriteLine($"Temp Extraction Dir: {tempExtractionPath}");

        if (!File.Exists(downloadZipPath)) // Check if file exists
        {
            Console.WriteLine($"Error: ZIP file does not exist at {downloadZipPath}");
            return 2;
        }
        
        Console.WriteLine("Waiting for JabberJay to close");
        Thread.Sleep(1000); // Wait for app to close (can be adjusted as needed)
        var x = Environment.CurrentDirectory;
        var y = AppDomain.CurrentDomain.BaseDirectory;
        Console.WriteLine(y);
        try
        {
            Console.WriteLine("Extracting update...");
            if (!Directory.Exists(tempExtractionPath)) Directory.CreateDirectory(tempExtractionPath); // Create temp extraction directory
            using (ArchiveFile archiveFile = new(downloadZipPath))
            {
                archiveFile.Extract(tempExtractionPath);
            } // Extract ZIP files
            Console.WriteLine("Extracted Update.");

            Console.WriteLine("Deleting old files...");
            Directory.Delete(installationDirectory, true); // Delete old files
            File.Delete(downloadZipPath); // Delete ZIP file (not needed anymore)
            
            Console.WriteLine("Moving new files...");
            Directory.Move(Path.Combine(tempExtractionPath, "JabberJay"), installationDirectory); // Move new files
            
            Console.WriteLine("Cleaning up...");
            Directory.Delete(tempExtractionPath, true); // Delete temp extraction directory

            Console.WriteLine("Relaunching JabberJay...");
            string jabberJayFilePath = Path.Combine(installationDirectory, "JabberJay.exe");
            if (File.Exists(jabberJayFilePath))
            {
                Process.Start(jabberJayFilePath);
                Console.WriteLine("JabberJay has been updated!");
                return 0;
            }
            else
            {
                Console.WriteLine("Error: JabberJay.exe not found in installation directory.");
                return 3;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error has occurred: {e.Message}");
            if (!Directory.Exists(installationDirectory))
            {
                Directory.CreateDirectory(installationDirectory);
            }
            if (Directory.Exists(tempExtractionPath))
            {
                try
                {
                    Directory.Delete(tempExtractionPath, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during cleanup: {ex.Message}");
                    return 4;
                }
            }
            return 5;
        }
    }
}