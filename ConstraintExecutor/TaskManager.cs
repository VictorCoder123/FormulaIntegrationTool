using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using Microsoft.Formula.API;
using Microsoft.Formula.Compiler;
using Microsoft.Formula.API.ASTQueries;
using Microsoft.Formula.API.Nodes;
using Microsoft.Formula.Common.Rules;

namespace ConstraintExecutor
{
    internal enum TaskKind { Query, Apply, Solve, Unknown };

    class TaskManager
    {
        private enum TaskTableCols { Id, Kind, Status, Result, Started, Duration, nCols }
        private SortedDictionary<int, TaskData> tasks = new SortedDictionary<int, TaskData>();

        /// <summary>
        /// True if StartTask should block until task is done.
        /// </summary>
        public bool IsWaitOn { get; set;}

        public TaskManager() {}

        public bool TryGetTask(int id, out Task task, out TaskKind kind)
        {
            TaskData data;
            if (!tasks.TryGetValue(id, out data))
            {
                task = null;
                kind = TaskKind.Unknown;
                return false;
            }
            kind = data.Kind;
            task = data.Task;
            return true;
        }

        public int UnloadTasks()
        {
            var unloadCount = tasks.Count;
            tasks.Clear();
            GC.Collect();
            return unloadCount;
        }

        public bool TryUnloadTask(int id)
        {
            if (!tasks.ContainsKey(id))
            {
                return false;
            }
            tasks.Remove(id);
            GC.Collect();
            return true;
        }

        public bool TryGetTaskdata(int id, out TaskData taskdata)
        {
            TaskData data;
            if (!tasks.TryGetValue(id, out data))
            {
                taskdata = null;
                return false;
            }
            taskdata = data;
            return true;
        }

        public bool TryGetStatistics(int id, out ExecuterStatistics stats)
        {
            TaskData data;
            if (!tasks.TryGetValue(id, out data))
            {
                stats = null;
                return false;
            }  
            stats = data.Statistics;
            return true;
        }

        public int StartTask(Task<QueryResult> task, ExecuterStatistics stats, CancellationTokenSource canceller)
        {
            Contract.Requires(task != null && stats != null && canceller != null);
            var data = new TaskData(tasks.Count, TaskKind.Query, task, stats, canceller);
            tasks.Add(data.Id, data);
            if (IsWaitOn)
            {
                task.RunSynchronously();
            }
            else
            {
                task.Start();
            }
            return data.Id;
        }

        public int StartTask(Task<ApplyResult> task, ExecuterStatistics stats, CancellationTokenSource canceller)
        {
            Contract.Requires(task != null && stats != null && canceller != null);
            var data = new TaskData(tasks.Count, TaskKind.Apply, task, stats, canceller);
            tasks.Add(data.Id, data);
            if (IsWaitOn)
            {
                task.RunSynchronously();
            }
            else
            {
                task.Start();
            }
            return data.Id;
        }


        public class TaskData
        {
            public DateTime StartTime { get; private set;}
            public TaskKind Kind { get; private set;}
            public int Id { get; private set;}
            public Task Task{ get; private set;}
            public ExecuterStatistics Statistics{ get; private set;}
            public CancellationTokenSource Canceller { get; private set; }

            public TimeSpan Duration
            {
                get
                {
                    if (!Task.IsCompleted)
                    {
                        return DateTime.Now - StartTime;
                    }

                    switch (Kind)
                    {
                        case TaskKind.Query:
                            return ((Task<QueryResult>)Task).Result.StopTime - StartTime;
                        case TaskKind.Apply:
                            return ((Task<ApplyResult>)Task).Result.StopTime - StartTime;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public string ResultSummary
            {
                get
                {
                    if (!Task.IsCompleted)
                    {
                        return "?";
                    }

                    switch (Kind)
                    {
                        case TaskKind.Query:
                            return ((Task<QueryResult>)Task).Result.Conclusion.ToString();
                        case TaskKind.Apply:
                            var outs = ((Task<ApplyResult>)Task).Result.OutputNames;
                            var outStr = string.Empty;
                            int i = 1;
                            foreach (var id in outs)
                            {
                                if (i == outs.Count)
                                {
                                    outStr += id.Name;
                                }
                                else
                                {
                                    outStr += id.Name + ", ";
                                }
                            }
                            return outStr;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public TaskData(int id, TaskKind kind, Task task, ExecuterStatistics stats, CancellationTokenSource canceller)
            {
                Contract.Requires(id >= 0);
                Contract.Requires(task != null && stats != null && canceller != null);
                Id = id;
                Kind = kind;
                Task = task;
                Statistics = stats;
                Canceller = canceller;
                StartTime = DateTime.Now;
            }
        }


    }
}
