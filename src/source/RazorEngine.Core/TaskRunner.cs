using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace RazorEngine
{
    /// <summary>
    /// Helper for missing net40 methods, REMOVE me when we are net45 only.
    /// </summary>
    internal class TaskRunner
    {
        /// <summary>
        /// Runs the given delegate in a new task (like Task.Run but works on net40).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        public static Task<T> Run<T>(Func<T> t)
        {
            return Task.Run(t);
        }

        /// <summary>
        /// Runs the given delegate in a new task (like Task.Run but works on net40).
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static Task Run(Action t)
        {
            return Task.Run(t);
        }
    }
}