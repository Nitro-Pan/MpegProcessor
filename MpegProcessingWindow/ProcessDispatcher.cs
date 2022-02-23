using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegProcessingWindow
{
    internal class ProcessDispatcher
    {
        List<Task> tasks;

        public ProcessDispatcher() {
            tasks = new();
        }

        public void Dispatch(Action a) {
            Task t = Task.Run(a);
            tasks.Add(t);
        }

        public void Dispatch<T>(Action<T> a, T item1) {
            Task t = Task.Run(() => { a.Invoke(item1); });
            tasks.Add(t);
        }

        public void Dispatch<T, R>(Func<T, R> f, T item1) {
            Task t = Task.Run(() => { return f.Invoke(item1); });
            tasks.Add(t);
        }
    }
}
