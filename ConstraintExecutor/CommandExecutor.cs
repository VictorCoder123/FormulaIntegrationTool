using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Formula.API;
using Microsoft.Formula.Compiler;
using Microsoft.Formula.API.ASTQueries;
using Microsoft.Formula.API.Nodes;
using Microsoft.Formula.Common.Rules;
using Microsoft.Formula.Common;
using Newtonsoft.Json;



namespace ConstraintExecutor
{
    class Debug
    {
        public List<string> Info { get; set; }
        public List<string> Error { get; set; }
    }

    public class CommandExecutor
    {
        private static readonly char[] cmdSplitChars = new char[] { ' ' };
        private string formulaFilename;
        private AST<Program> currentProgram;
        private ProgramName currentProgName;
        private Env env;
        private TaskManager taskManager;

        public CommandExecutor()
        {
            // Set flag to run task asynchronously.
            env = new Env();
            taskManager = new TaskManager();
            taskManager.IsWaitOn = false;
        }

        public string DebugInfo(InstallResult result)
        {
            var debug = new Debug();
            List<string> infos = new List<string>();
            foreach (var kv in result.Touched)
            {
                infos.Add(string.Format("({0}) {1}", kv.Status, kv.Program.Node.Name.ToString(env.Parameters)));
            }
            debug.Info = infos;

            List<string> errors = new List<string>();
            foreach (var f in result.Flags)
            {
                errors.Add(
                    string.Format("{0} ({1}, {2}): {3}",
                    f.Item1.Node.Name.ToString(env.Parameters),
                    f.Item2.Span.StartLine,
                    f.Item2.Span.StartCol,
                    f.Item2.Message));
            }
            debug.Error = errors;

            string json = JsonConvert.SerializeObject(debug, Formatting.Indented);
            return json;
        }

        public void DoApply(string transformStepName, List<string> parameters, out string debugInfo)
        {
            debugInfo = null;

            string paramString = string.Join(",", parameters.ToArray());
            string s = string.Format("R = {0}({1})", transformStepName, paramString);
            Console.WriteLine(s);
            var cmdLineName = new ProgramName("CommandLine.4ml");
            var parse = Factory.Instance.ParseText(
                cmdLineName,
                string.Format("transform system Dummy () returns (dummy:: Dummy) {{\n{0}.\n}}", s)
            );
            parse.Wait();

            if (!parse.Result.Succeeded)
            {
                Console.WriteLine("Could not parse transformation step", SeverityKind.Warning);
                return;
            }

            var step = parse.Result.Program.FindAny(
                new NodePred[]
                {
                    NodePredFactory.Instance.Star,
                    NodePredFactory.Instance.MkPredicate(NodeKind.Step)
                }) as AST<Step>;

            if (step == null)
            {
                Console.WriteLine("Start an apply task. Use: apply transformstep", SeverityKind.Warning);
                return;
            }

            AST<Node> stepModule;
            if (!Utility.TryResolveModuleByName(step.Node.Rhs.Module.Name, out stepModule, env, "step module"))
            {
                return;
            }

            AST<ModApply> rhs = null;
            if (stepModule.Node.NodeKind == NodeKind.Model)
            {
                var model = ((Model)stepModule.Node);
                if (step.Node.Rhs.Args.Count != 0)
                {
                    Console.WriteLine(
                        string.Format("Model {0} does not take arguments", model.Name),
                        SeverityKind.Warning);
                    return;
                }

                rhs = Factory.Instance.MkModApply(Utility.ToModuleRef(stepModule, stepModule.Node.Span, step.Node.Rhs.Module.Rename), stepModule.Node.Span);
            }
            else if (stepModule.Node.NodeKind == NodeKind.Transform ||
                     stepModule.Node.NodeKind == NodeKind.TSystem)
            {
                string name;
                var inputs = stepModule.Node.NodeKind == NodeKind.Transform
                                    ? ((Transform)stepModule.Node).Inputs
                                    : ((TSystem)stepModule.Node).Inputs;

                if (step.Node.Rhs.Args.Count != inputs.Count)
                {
                    stepModule.Node.TryGetStringAttribute(AttributeKind.Name, out name);
                    Console.WriteLine(
                        string.Format("Transform {0} requires {1} arguments, but got {2}",
                            name,
                            inputs.Count,
                            step.Node.Rhs.Args.Count),
                        SeverityKind.Warning);
                    return;
                }

                int i = 1;
                AST<Node> argModule;
                rhs = Factory.Instance.MkModApply(Utility.ToModuleRef(stepModule, stepModule.Node.Span, step.Node.Rhs.Module.Rename), stepModule.Node.Span);
                using (var itArgs = step.Node.Rhs.Args.GetEnumerator())
                {
                    using (var itInputs = inputs.GetEnumerator())
                    {
                        while (itArgs.MoveNext() && itInputs.MoveNext())
                        {
                            if (itInputs.Current.IsValueParam)
                            {
                                rhs = Factory.Instance.AddArg(rhs, Factory.Instance.ToAST(itArgs.Current));
                            }
                            else if (itArgs.Current.NodeKind == NodeKind.Id || itArgs.Current.NodeKind == NodeKind.ModRef)
                            {
                                name = itArgs.Current.NodeKind == NodeKind.Id ? ((Id)itArgs.Current).Name : ((ModRef)itArgs.Current).Name;
                                string rename = itArgs.Current.NodeKind == NodeKind.Id ? null : ((ModRef)itArgs.Current).Rename;
                                if (!Utility.TryResolveModuleByName(name, out argModule, env, "input " + i.ToString()))
                                {
                                    return;
                                }
                                else if (argModule.Node.NodeKind != NodeKind.Model)
                                {
                                    argModule.Node.TryGetStringAttribute(AttributeKind.Name, out name);
                                    Console.WriteLine(string.Format("Module {0} is not valid for this operation", name), SeverityKind.Warning);
                                    return;
                                }

                                rhs = Factory.Instance.AddArg(rhs, Utility.ToModuleRef(argModule, stepModule.Node.Span, rename));
                            }
                            else
                            {
                                Console.WriteLine(string.Format("Input {0} should be a model.", i), SeverityKind.Warning);
                                return;
                            }

                            ++i;
                        }
                    }
                }
            }
            else
            {
                string name;
                stepModule.Node.TryGetStringAttribute(AttributeKind.Name, out name);
                Console.WriteLine(string.Format("Module {0} is not valid for this operation", name), SeverityKind.Warning);
                return;
            }

            var resolvedStep = Factory.Instance.MkStep(rhs, rhs.Node.Span);
            foreach (var id in step.Node.Lhs)
            {
                resolvedStep = Factory.Instance.AddLhs(resolvedStep, Factory.Instance.MkId(id.Name, id.Span));
            }

            List<Flag> flags;
            ExecuterStatistics stats;
            System.Threading.Tasks.Task<ApplyResult> task;
            var applyCancel = new CancellationTokenSource();
            var result = env.Apply(
                resolvedStep,
                true,
                true,
                out flags,
                out task,
                out stats,
                applyCancel.Token
            );

            if (!result)
            {
                Console.WriteLine("Could not start operation; environment is busy", SeverityKind.Warning);
                return;
            }

            if (task != null)
            {
                var id = taskManager.StartTask(task, stats, applyCancel);
                Console.WriteLine(string.Format("Started apply task with Id {0}.", id), SeverityKind.Info);
                task.Wait();
            }
            else
            {
                Console.WriteLine("Failed to start apply task.", SeverityKind.Warning);
            }
        }

        // Load a Formula file into env.
        public void DoLoadFile(string filename, out AST<Program> program, out string debugInfo)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                Console.WriteLine("Cannot load empty filename.");
                program = null;
                debugInfo = null;
                return;
            }

            // Get AST tree from parsing result and store its reference.
            ProgramName progName = new ProgramName(filename);
            Task<ParseResult> task = Factory.Instance.ParseFile(progName);
            task.Wait();

            if (!task.Result.Succeeded)
            {
                Console.WriteLine("Failed to parse the Formula file.");
                program = null;
                debugInfo = null;
                return;
            }

            program = task.Result.Program;

            // Install program into current env.
            InstallResult result;
            env.Install(program, out result);
            debugInfo = DebugInfo(result);
        }

        // Import constraints from loaded program in parameter and return a list of query strings.
        public List<string> DoLoadConstraints(string constraintFilename, string modelName, out string debugInfo)
        {
            AST<Program> constraintProgram;
            DoLoadFile(constraintFilename, out constraintProgram, out debugInfo);
            List<string> constraintList = DoSearchConstraints(constraintProgram, modelName);
            return constraintList;
        }

        // Load language domain into env.
        public void DoLoadDomain(string domainFilename, out string debugInfo)
        {
            AST<Program> program;
            DoLoadFile(domainFilename, out program, out debugInfo);
        }

        // Load model file into env and store reference of ProgramName and Program.
        public void DoLoadModel(string modelFilename, out string debugInfo)
        {
            AST<Program> program;
            DoLoadFile(modelFilename, out program, out debugInfo);
            currentProgram = program;
            currentProgName = new ProgramName(modelFilename);
        }

        // Load transformation file into env.
        public void DoLoadTransformation(string transformFilename, out string debugInfo)
        {
            AST<Program> program;
            DoLoadFile(transformFilename, out program, out debugInfo);        
        }

        // Return a list of synthesized Query string inside program parameter.
        public List<string> DoSearchConstraints(AST<Program> program, string modelName)
        {
            List<string> constraintList = new List<string>();
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

        // Helper method to get a list of goal from constraint, and will be invoked in DoConstraintQuery().
        public void ParseConstraint(string[] cmdParts, out AST<Body>[] goals)
        {
            goals = null;

            // Parse constraint string as Formula file to get AST tree synchronously, it's not necessary to
            // make parsing here asynchronously since we suppose constraint text are small and trivial.
            var cmdLineName = new ProgramName("CommandLine.4ml");
            var parse = Factory.Instance.ParseText(
                cmdLineName,
                string.Format("domain Dummy {{q :-\n{0}\n.}}", cmdParts[1])
            );

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

        // Execute a query on a specific program based on its ProgramName.
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

            var modelName = cmdParts[0];

            ParseConstraint(cmdParts, out goals);

            List<Flag> flags;
            Task<QueryResult> task;
            ExecuterStatistics stats;
            var queryCancel = new CancellationTokenSource();

            var result = env.Query(
                progName,
                modelName,
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

        // Query all constraints in Constraint Domain and return a JSON file with result.
        // Use progName of last loaded model, which is stored in executor as reference.
        public string CheckConstraints(List<string> constraintList)
        {
            Dictionary<string, Task<LiftedBool>> tasks = new Dictionary<string, Task<LiftedBool>>();
            foreach (string constraint in constraintList)
            {
                // TaskManager.TaskData taskdata;
                Task<LiftedBool> task = DoConstraintQuery(constraint, currentProgName);
                tasks.Add(constraint, task);
            }

            Task.WaitAll(tasks.Values.ToArray());

            Dictionary<string, string> results = new Dictionary<string, string>();
            foreach (string key in tasks.Keys)
            {
                Task<LiftedBool> task;
                tasks.TryGetValue(key, out task);
                string value;
                if (task.Result == LiftedBool.True)
                {
                    value = "True";
                }
                else if (task.Result == LiftedBool.False)
                {
                    value = "False";
                }
                else
                {
                    value = "Unknown";
                }
                results.Add(key, value);
            }

            string json = JsonConvert.SerializeObject(results, Formatting.Indented);
            return json;

        }

    }

    class ConstraintResult
    {

    }
}
