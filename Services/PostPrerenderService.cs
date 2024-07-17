
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace test.Services
{
    public class PostPrerenderService
    {
        private readonly Queue<Func<Task>> _afterRenderActions = new Queue<Func<Task>>();

        public void AddAfterRenderAction(Func<Task> action)
        {
            _afterRenderActions.Enqueue(action);
        }

        public async Task ExecuteAfterRenderActionsAsync()
        {
            while (_afterRenderActions.TryDequeue(out var action))
            {
                await action();
            }
        }
    }

}
