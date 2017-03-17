using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            List<string> constraints;

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
            var progName = new ProgramName(formulaFile);
            Console.WriteLine("Start loading Formula file from {0}", formulaFileInfo.FullName);
            executor = new CommandExecutor(formulaFile);


            ParseJSON(jsonFile, out constraints);
            foreach (string constraint in constraints)
            {
                TaskManager.TaskData taskdata;
                int id = executor.DoConstraintQuery(query1, progName);
                executor.taskManager.TryGetTaskdata(id, out taskdata);
                Console.WriteLine(taskdata.ResultSummary);
            }

            Console.ReadLine();
        }
    }
}
