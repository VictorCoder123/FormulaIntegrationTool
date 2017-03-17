//DISCLAIMER

//The sample scripts are Not supported under any Microsoft standard support program Or service.
//The sample scripts are provided AS Is without warranty of any kind.Microsoft further disclaims all implied warranties including, without limitation, 
//any implied warranties of merchantability Or of fitness for a particular purpose.The entire risk arising out of the use Or performance of the sample 
//scripts And documentation remains with you. In no event shall Microsoft, its authors, Or anyone else involved in the creation, production, Or delivery of 
//the scripts be liable for any damages whatsoever (including without limitation, damages for loss of business profits, business interruption, loss of business 
//information, Or other pecuniary loss) arising out of the use of Or inability to use the sample scripts Or documentation, even if Microsoft has been advised of
//the possibility of such damages.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// using Microsoft.Formula.Common;


namespace ConstraintExecutor
{
    class ExecutorProgram
    {
        static void Main(string[] args)
        {
            string formulaFile;
            FileInfo formulaFileInfo;
            CommandExecutor executor;

            string query1 = "constraint1";
            string query2 = "constraint2";

            // Read in formula filename from either command line args or user input.
            if (args.Length != 0)
            {
                formulaFile = args[0];
                formulaFileInfo = new FileInfo(formulaFile);
                if (!formulaFileInfo.Exists || formulaFileInfo.Extension != ".4ml")
                    Console.WriteLine("Invalid Formula file and exit...");
            }
            else {
                Console.WriteLine("Enter Formula .4ml file for initial loading: ");
                formulaFile = Console.ReadLine();
                formulaFileInfo = new FileInfo(formulaFile);
                Console.WriteLine(formulaFileInfo.Extension);
                while (!formulaFileInfo.Exists || formulaFileInfo.Extension != ".4ml")
                {
                    Console.WriteLine("Invalid Formula file, please enter a new file name again: ");
                    formulaFile = Console.ReadLine();
                    formulaFileInfo = new FileInfo(formulaFile);
                }
            }
            
            Console.WriteLine("Start loading Formula file from {0}", formulaFileInfo.FullName);
            executor = new CommandExecutor(formulaFile);

            executor.DoConstraintQuery(query1);
            executor.DoConstraintQuery(query2);

            Console.ReadLine();
        }
    }
}
