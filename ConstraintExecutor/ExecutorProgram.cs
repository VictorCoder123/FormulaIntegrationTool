using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
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
            string query1 = "M constraint1";
               
            CommandExecutor executor;
            string formulaFile;
            string jsonFile;
            FileInfo formulaFileInfo;
            FileInfo jsonFileInfo;
            Stopwatch stopwatch = new Stopwatch();

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

            stopwatch.Start();

            executor = new CommandExecutor(formulaFile);
            executor.DoLoad();

            stopwatch.Stop();
            Console.WriteLine("Total loading time of Formula file is {0} seconds", stopwatch.Elapsed.ToString());

            stopwatch.Start();
            List<Task<LiftedBool>> tasks = new List<Task<LiftedBool>>();
            foreach (string constraint in executor.constraintList)
            {
                // TaskManager.TaskData taskdata;
                Task<LiftedBool> task = executor.DoConstraintQuery(query1);
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());

            stopwatch.Stop();
            Console.WriteLine("Time for executing all queries in Constraints domain is {0} seconds", stopwatch.Elapsed.ToString());

            foreach (Task<LiftedBool> result in tasks)
            {
                Console.WriteLine(result.Result);
            }

            Console.ReadLine();
        }
    }
}
