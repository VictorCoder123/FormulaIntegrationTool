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
            // List<string> constraints;

            // Read in formula and JSON filename from either command line args or user input.
            if (args.Length != 0)
            {
                formulaFile = args[0];
                formulaFileInfo = new FileInfo(formulaFile);
                jsonFile = args[1];
                jsonFileInfo = new FileInfo(jsonFile);

                if (!formulaFileInfo.Exists || formulaFileInfo.Extension != ".4ml" || 
                    !jsonFileInfo.Exists || jsonFileInfo.Extension != ".json")
                {
                    Console.WriteLine("Invalid Formula or JSON file and exit...");
                    return;
                }
                   
            }
            else {
                ReadInFile(".4ml", out formulaFile, out formulaFileInfo);
                ReadInFile(".json", out jsonFile, out jsonFileInfo);
            }

            // Create executor with Formula file loaded.
            Console.WriteLine("Start loading Formula file from {0}", formulaFileInfo.FullName);
            executor = new CommandExecutor(formulaFile);

            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Restart();
            progName = new ProgramName(formulaFile);
            executor.DoLoadDomain();
            stopwatch.Stop();
            Console.WriteLine("Time for loading domain is {0}", stopwatch.Elapsed);

            stopwatch.Restart();
            var modelFile1 = "graph_model_1.4ml";
            executor.DoLoadFile(modelFile1);
            progName = new ProgramName(modelFile1);
            stopwatch.Stop();
            Console.WriteLine("Time for loading model is {0}", stopwatch.Elapsed);

            stopwatch.Restart();
            var modelFile2 = "graph_model_2.4ml";
            executor.DoLoadFile(modelFile2);
            progName = new ProgramName(modelFile2);
            stopwatch.Stop();
            Console.WriteLine("Time for loading model is {0}", stopwatch.Elapsed);

            // executor.DoSearchConstraints();

            // ParseJSON(jsonFile, out constraints);
            // constraints = executor.constraintList;

            List<Task<LiftedBool>> tasks = new List<Task<LiftedBool>>();
            /**foreach (string constraint in constraints)
            {
                // TaskManager.TaskData taskdata;
                Task<LiftedBool> task = executor.DoConstraintQuery(query1, progName);
                tasks.Add(task);
            }**/

            string query = "M2 constraint1";
            Task<LiftedBool> task = executor.DoConstraintQuery(query, progName);
            tasks.Add(task);

            Task.WaitAll(tasks.ToArray());

            foreach (Task<LiftedBool> result in tasks)
            {
                Console.WriteLine(result.Result);
            }

            Console.ReadLine();
        }
    }
}
