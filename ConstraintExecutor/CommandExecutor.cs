﻿using System;
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
        private Dictionary<ProgramName, Program> programs = new Dictionary<ProgramName, Program>();
        private Env env = new Env();
        public TaskManager taskManager = new TaskManager();

        public CommandExecutor(string filename)
        {
            // Load Formula file and create AST tree, wait until loading is finished.
            DoLoad(filename);
            // Run task asynchronously.
            taskManager.IsWaitOn = false;
        }

        public void DoLoad(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                Console.WriteLine("Cannot load empty filename.");
                return;
            }
            InstallResult result;
            env.Install(filename, out result);
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
