using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyApp
{
    internal struct EventRegistration
    {
        public void Dispose() { }
    }

    internal class EventLoop
    {
        public static Task Start(Action<EventLoop> action)
        {
            return Task.CompletedTask;
        }

        public EventRegistration On(Task t, Action action)
        {
            return default;
        }

        public EventRegistration On(Task t, Func<Task> action)
        {
            return default;
        }

        public EventRegistration On<T>(Task<T> t, Action<T> action)
        {
            return default;
        }

        public EventRegistration On<T>(Task<T> t, Func<T, Task> action)
        {
            return default;
        }

        public EventRegistration OnEvery(Func<Task> f, Action action)
        {
            return default;
        }

        public EventRegistration OnEvery(Func<Task> f, Func<Task> action)
        {
            return default;
        }

        public EventRegistration OnEvery<T>(Func<Task<T>> t, Action<T> action)
        {
            return default;
        }

        public EventRegistration OnEvery<T>(Func<Task<T>> t, Func<T, Task> action)
        {
            return default;
        }

        public EventRegistration After(int milliseconds, Action action)
        {
            return default;
        }

        public EventRegistration After(int milliseconds, Func<Task> action)
        {
            return default;
        }

        public EventRegistration AfterEvery(int milliseconds, Action action)
        {
            return default;
        }

        public EventRegistration AfterEvery(int milliseconds, Func<Task> action)
        {
            return default;
        }

        public void Run(Action action)
        {

        }

        public void Run(Func<Task> action)
        {

        }
    }
}
