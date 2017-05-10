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
using Newtonsoft.Json;

namespace ConstraintExecutor
{
    public class Utility
    {
        private static readonly char[] cmdSplitChars = new char[] { ' ' };
        private static bool exeOptions = false;
        private static CancellationTokenSource canceler = new CancellationTokenSource();

        private static int CompareModules(AST<Node> mod1, AST<Node> mod2)
        {
            var prog1 = (Program)mod1.GetPathParent();
            var prog2 = (Program)mod2.GetPathParent();
            var cmp = ProgramName.Compare(prog1.Name, prog2.Name);
            if (cmp != 0)
            {
                return cmp;
            }

            string name1, name2;
            mod1.Node.TryGetStringAttribute(AttributeKind.Name, out name1);
            mod2.Node.TryGetStringAttribute(AttributeKind.Name, out name2);
            return string.CompareOrdinal(name1, name2);
        }

        public static AST<Node>[] GetModulesByName(string partialModuleName, string partialProgramName, Env env)
        {
            partialProgramName = partialProgramName == null ? string.Empty : partialProgramName.ToLowerInvariant();
            partialModuleName = partialModuleName == null ? string.Empty : partialModuleName;
            var sorted = new Set<AST<Node>>((x, y) => CompareModules(x, y));
            var root = env.FileRoot;


            NodePred[] queries = new NodePred[]
            {
                NodePredFactory.Instance.Star,
                NodePredFactory.Instance.Module,
            };

            root.FindAll(
            queries,
            (path, node) =>
            {
                var progName = ((Program)((LinkedList<ChildInfo>)path).Last.Previous.Value.Node).Name;
                string modName;
                node.TryGetStringAttribute(AttributeKind.Name, out modName);

                if (!string.IsNullOrEmpty(partialProgramName) &&
                    !progName.ToString().Contains(partialProgramName))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(partialModuleName) &&
                    !modName.Contains(partialModuleName))
                {
                    return;
                }

                sorted.Add(Factory.Instance.FromAbsPositions(root.Node, path));
            },
            canceler.Token);
          
            
           

            root = env.EnvRoot;
            root.FindAll(
                new NodePred[]
                {
                    NodePredFactory.Instance.Star,
                    NodePredFactory.Instance.Module
                },
                (path, node) =>
                {
                    var progName = ((Program)((LinkedList<ChildInfo>)path).Last.Previous.Value.Node).Name;
                    string modName;
                    node.TryGetStringAttribute(AttributeKind.Name, out modName);

                    if (!string.IsNullOrEmpty(partialProgramName) &&
                        !progName.ToString().Contains(partialProgramName))
                    {
                        return;
                    }

                    if (!string.IsNullOrEmpty(partialModuleName) &&
                        !modName.Contains(partialModuleName))
                    {
                        return;
                    }

                    sorted.Add(Factory.Instance.FromAbsPositions(root.Node, path));
                },
                canceler.Token);

            return sorted.ToArray();
        }

        public static AST<Program>[] GetProgramsByName(string partialName, Env env)
        {
            partialName = partialName == null ? string.Empty : partialName.ToLowerInvariant();
            var sorted = new Set<AST<Program>>((x, y) => ProgramName.Compare(x.Node.Name, y.Node.Name));
            env.FileRoot.FindAll(
                new NodePred[]
                {
                    NodePredFactory.Instance.Star,
                    NodePredFactory.Instance.MkPredicate(NodeKind.Program),
                },
                (path, node) =>
                {
                    if (string.IsNullOrEmpty(partialName) ||
                        ((Program)node).Name.ToString().Contains(partialName))
                    {
                        sorted.Add((AST<Program>)Factory.Instance.ToAST(node));
                    }
                },
                canceler.Token);

            env.EnvRoot.FindAll(
                new NodePred[]
                {
                    NodePredFactory.Instance.Star,
                    NodePredFactory.Instance.MkPredicate(NodeKind.Program),
                },
                (path, node) =>
                {
                    if (string.IsNullOrEmpty(partialName) ||
                        ((Program)node).Name.ToString().Contains(partialName))
                    {
                        sorted.Add((AST<Program>)Factory.Instance.ToAST(node));
                    }
                },
                canceler.Token);

            return sorted.ToArray();
        }

        public static bool TryResolveModuleByName(string partialModAndProgName, out AST<Node> module, Env env, string prompt = null)
        {
            bool result;
            if (string.IsNullOrWhiteSpace(partialModAndProgName))
            {
                result = TryResolveModuleByName(null, null, out module, env, prompt);
            }
            else
            {
                var parts = partialModAndProgName.Split(cmdSplitChars, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                {
                    result = TryResolveModuleByName(parts[0], null, out module, env, prompt);
                }
                else
                {
                    result = TryResolveModuleByName(parts[0], parts[1], out module, env, prompt);
                }
            }

            return result;
        }

        public static bool TryResolveModuleByName(string partialModName, string partialProgName, out AST<Node> module, Env env, string prompt)
        {
            var candidates = GetModulesByName(partialModName, partialProgName, env);
            if (candidates.Length == 0)
            {
                Console.WriteLine("No module with that name", SeverityKind.Warning);
                module = null;
                return false;
            }
            else if (candidates.Length == 1 || exeOptions)
            {
                module = candidates[0];
                return true;
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                Console.WriteLine("Choose:");
            }
            else
            {
                Console.WriteLine(string.Format("Choose {0}:", prompt));
            }

            var max = Math.Min(9, candidates.Length - 1);
            string canProgName, canModName;
            for (var i = 0; i <= max; ++i)
            {
                canProgName = ((Program)candidates[i].GetPathParent()).Name.ToString();
                candidates[i].Node.TryGetStringAttribute(AttributeKind.Name, out canModName);
                Console.WriteLine(string.Format("  {0}. {1} at \"{2}\"", i, canModName, canProgName));
            }

            if (max < candidates.Length - 1)
            {
                Console.WriteLine(
                    string.Format("{0} choice(s) not shown. Provide a more specific name.",
                        candidates.Length - 1 - max),
                    SeverityKind.Warning);
            }

            int choice = 0;
            module = candidates[(int)choice];
            return true;
        }

        public static bool TryResolveProgramByName(string partialName, out AST<Program> program, Env env)
        {
            var candidates = GetProgramsByName(partialName, env);
            if (candidates.Length == 0)
            {
                Console.WriteLine("No file with that name", SeverityKind.Warning);
                program = null;
                return false;
            }
            else if (candidates.Length == 1 || exeOptions)
            {
                program = candidates[0];
                return true;
            }

            Console.WriteLine("Choose:");
            var max = Math.Min(9, candidates.Length - 1);
            for (var i = 0; i <= max; ++i)
            {
                Console.WriteLine(string.Format("  {0}. {1}", i, candidates[i].Node.Name));
            }

            if (max < candidates.Length - 1)
            {
                Console.WriteLine(
                    string.Format("{0} choice(s) not shown. Provide a more specific name.",
                        candidates.Length - 1 - max),
                    SeverityKind.Warning);
            }

            int choice = 0;

            program = candidates[(int)choice];
            return true;
        }

        public static AST<ModRef> ToModuleRef(AST<Node> module, Span span, string rename = null)
        {
            Contract.Requires(module != null && module.Node.IsModule);
            foreach (var n in module.Path.Reverse<ChildInfo>())
            {
                if (n.Node.NodeKind == NodeKind.Program)
                {
                    string name;
                    module.Node.TryGetStringAttribute(AttributeKind.Name, out name);
                    return Factory.Instance.MkModRef(name, rename, ((Program)n.Node).Name.ToString(), span);
                }
            }

            //// Module should have a path to its program.
            throw new InvalidOperationException();
        }
    }
}
