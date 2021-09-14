using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventLoop
{
    internal class EventLoop
    {
        // TODO: Really need to figure out the startup model.

        public static Task Start(Action action)
        {
            return Task.CompletedTask;
        }

        // TODO: Take async actions too, i.e. Func<Task>

        public void On(Task t, Action action)
        {

        }

        public void On(Task t, Func<Task> action)
        {

        }

        public void On<T>(Task<T> t, Action<T> action)
        {

        }

        public void On<T>(Task<T> t, Func<T, Task> action)
        {

        }

        public void OnEvery(Func<Task> f, Action action)
        {

        }

        public void OnEvery(Func<Task> f, Func<Task> action)
        {

        }

        public void OnEvery<T>(Func<Task<T>> t, Action<T> action)
        {

        }

        public void OnEvery<T>(Func<Task<T>> t, Func<T, Task> action)
        {

        }

        public void After(int milliseconds, Action action)
        {

        }

        public void After(int milliseconds, Func<Task> action)
        {

        }

        public void AfterEvery(int milliseconds, Action action)
        {

        }

        public void AfterEvery(int milliseconds, Func<Task> action)
        {

        }

        public void Run(Action action)
        {

        }

        public void Run(Func<Task> action)
        {

        }
    }
}
