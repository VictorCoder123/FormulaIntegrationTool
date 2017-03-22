using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Formula.API;
using Microsoft.Formula.Compiler;
using Microsoft.Formula.API.ASTQueries;
using Microsoft.Formula.API.Nodes;
using Microsoft.Formula.Common.Rules;
using Microsoft.Formula.Common;


namespace ConstraintExecutor
{
    class CommandExecutor
    {
        private static readonly char[] cmdSplitChars = new char[] { ' ' };
        private string formulaFilename;
        private AST<Program> program;
        private Env env = new Env();
        public TaskManager taskManager = new TaskManager();

        public CommandExecutor(string filename)
        {
            formulaFilename = filename;
            // Set flag to run task asynchronously.
            taskManager.IsWaitOn = false;
        }

        public List<string> constraintList
        {
            get;
            private set;
        }

        public void DoLoad()
        {
            if (string.IsNullOrWhiteSpace(formulaFilename))
            {
                Console.WriteLine("Cannot load empty filename.");
                return;
            }
            // Get AST tree from parsing result and store its reference.
            ProgramName progName = new ProgramName(formulaFilename);
            Task<ParseResult> task = Factory.Instance.ParseFile(progName);
            task.Wait();
            if (!task.Result.Succeeded)
            {
                Console.WriteLine("Failed to parse the Formula file.");
                return;
            }
            program = task.Result.Program;

            // Install program into current env.
            InstallResult result;
            env.Install(program, out result);

            // Import constraints from loaded program.
            constraintList = DoSearchConstraints();
        }

        public void ParseConstraint(string[] cmdParts, out AST<Body>[] goals)
        {
            goals = null;

            // Parse constraint string as Formula file to get AST tree synchronously, it's not necessary to
            // make parsing here asynchronously since we suppose constraint text are small and trivial.
            var cmdLineName = new ProgramName("CommandLine.4ml");
            var parse = Factory.Instance.ParseText(
                cmdLineName,
                string.Format("domain Dummy {{q :-\n{0}\n.}}", cmdParts[1]));
            parse.Wait();

            if (!parse.Result.Succeeded)
            {
                Console.WriteLine("Failed to parse constraint string");
                return;
            }

            var rule = parse.Result.Program.FindAny(
               new NodePred[]
               {
                    NodePredFactory.Instance.Star,
                    NodePredFactory.Instance.MkPredicate(NodeKind.Rule),
               }
            );

            Contract.Assert(rule != null);
            var bodies = ((Rule)rule.Node).Bodies;
            goals = new AST<Body>[bodies.Count];
            int i = 0;
            // Return a list of AST tree with Body Node.
            foreach (var b in bodies)
            {
                goals[i++] = (AST<Body>)Factory.Instance.ToAST(b);
            }
        }

        // Return a list of synthesized Query string.
        public List<string> DoSearchConstraints()
        {
            List<string> constraintList = new List<string>();
            string modelName = "M";
            if (program == null)
            {
                Console.WriteLine("Failed to read parsed program.");
            }

            IEnumerable<Node> domains = program.Root.Children.Where(x => {
                return x.NodeKind == NodeKind.Domain;
            });

            Node constraintDomain = domains.First(x => {
                Domain domain = (Domain)x;
                return domain.Name == "Constraints";
            });

            IEnumerable<Node> rules = constraintDomain.Children.Where(x => {
                return x.NodeKind == NodeKind.Rule;
            });

            IEnumerable<Node> constraintRules = rules.Where(x => {
                Rule rule = (Rule)x;
                return rule.Heads.First().NodeKind == NodeKind.Id;
            });

            foreach (Rule rule in constraintRules)
            {
                Id id = (Id)rule.Heads.First();
                constraintList.Add(modelName + " " + id.Name);
            }

            return constraintList;
        }

        public async Task<LiftedBool> DoConstraintQuery(string constraint, ProgramName progName)
        {
            AST<Body>[] goals;
            int id = -1;

            var cmdParts = constraint.Split(cmdSplitChars, 2, StringSplitOptions.RemoveEmptyEntries);
            if (cmdParts.Length != 2)
            {
                Console.WriteLine("Invalid constraint, must contain two parts.");
                return LiftedBool.Unknown;
            }

            var name = cmdParts[0];

            ParseConstraint(cmdParts, out goals);

            List<Flag> flags;
            System.Threading.Tasks.Task<QueryResult> task;
            ExecuterStatistics stats;
            var queryCancel = new CancellationTokenSource();

            var result = env.Query(
                progName,
                name,
                goals,
                true,
                true,
                out flags,
                out task,
                out stats,
                queryCancel.Token);

            if (!result)
            {
                Console.WriteLine("Could not start operation; environment is busy");
                return LiftedBool.Unknown;
            }

            if (task != null)
            {
                id = taskManager.StartTask(task, stats, queryCancel);
                Console.WriteLine(string.Format("Started query task with Id {0}.", id));
                task.Wait();
                return task.Result.Conclusion;
            }
            else
            {
                Console.WriteLine("Failed to generate query task.");
                return LiftedBool.Unknown;
            }

        }

    }
}
