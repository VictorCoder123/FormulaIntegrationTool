using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Formula.Common;
using Microsoft.Formula.API;
using Newtonsoft.Json;


namespace ConstraintExecutor
{
    class ExecutorProgram
    {
        static void ParseJSON(string jsonFile, out List<string> constraints)
        {
            if (!File.Exists(jsonFile))
            {
                System.Console.WriteLine(String.Format("Constraint file doesn't exist: {0}", jsonFile));
                constraints = null;
                return;
            }

            constraints = new List<string>();
            using (var sr = new StreamReader(jsonFile))
            {
                var content = sr.ReadToEnd();
                constraints = JsonConvert.DeserializeObject<List<string>>(content);
            }
        }

        static void ReadInFile(string fileType, out string file, out FileInfo fileInfo)
        {
            Console.WriteLine("Enter {0} file for initial loading: ", fileType);
            file = Console.ReadLine();
            fileInfo = new FileInfo(file);
            Console.WriteLine(fileInfo.Extension);
            while (!fileInfo.Exists || fileInfo.Extension != fileType)
            {
                Console.WriteLine("Invalid {0} file, please enter a new file name again: ", fileType);
                file = Console.ReadLine();
                fileInfo = new FileInfo(file);
            }
        }

        static void Main(string[] args)
        {
            CommandExecutor executor;
            string formulaFile;
            string jsonFile;
            FileInfo formulaFileInfo;
            FileInfo jsonFileInfo;
            ProgramName progName;

            // Read in formula and JSON filename from either command line args or user input.
            if (args.Length != 0)
            {
                formulaFile = args[0];
                formulaFileInfo = new FileInfo(formulaFile);

                if (!formulaFileInfo.Exists || formulaFileInfo.Extension != ".4ml")
                {
                    Console.WriteLine("Invalid Formula or JSON file and exit...");
                    return;
                }
                   
            }
            else {
                ReadInFile(".4ml", out formulaFile, out formulaFileInfo);
            }

            // Create executor instance.
            string debugInfo;
            executor = new CommandExecutor();

            // Use stopwatch to count running time
            Stopwatch stopwatch = new Stopwatch();
            
            stopwatch.Restart();
            var domainFile = "Language.4ml";
            executor.DoLoadDomain(domainFile, out debugInfo);
            stopwatch.Stop();
            Console.WriteLine(debugInfo);
            Console.WriteLine("Time for loading language domain is {0}", stopwatch.Elapsed);

            stopwatch.Restart();
            var constraintFile = "Constraints.4ml";
            var modelName = "M";
            List<string> constraintList = executor.DoLoadConstraints(constraintFile, modelName, out debugInfo);
            stopwatch.Stop();
            Console.WriteLine(debugInfo);
            Console.WriteLine("Time for loading constraint domain is {0}", stopwatch.Elapsed);

            stopwatch.Restart();
            var modelFile = "Model.4ml";
            executor.DoLoadModel(modelFile, out debugInfo);
            stopwatch.Stop();
            Console.WriteLine(debugInfo);
            Console.WriteLine("Time for loading model file is {0}", stopwatch.Elapsed);

            // Check constraints and print out results.
            string json = executor.CheckConstraints(constraintList);
            Console.WriteLine(json);

            stopwatch.Restart();
            var transformFile = "Transformation.4ml";
            executor.DoTransform(transformFile);
            // executor.DoLoadModel(modelFile, out debugInfo);
            stopwatch.Stop();
            // Console.WriteLine(debugInfo);
            Console.WriteLine("Time for transformation is {0}", stopwatch.Elapsed);

            Console.ReadLine();
        }
    }
}
