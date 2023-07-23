using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;

namespace TodoAppData.Controllers
{

    [Route("api/[controller]")]
    public class TodoDataController : Controller
    {
        private readonly IReliableStateManager _reliableStateManager;

        public TodoDataController(IReliableStateManager reliableStateManager)
        {
            _reliableStateManager = reliableStateManager;
        }

        [HttpGet]
        // api/Get
        public async Task<IActionResult> Get()
        {
            CancellationToken ct = new CancellationToken();
            List<TodoList> todoList
                 = new List<TodoList>();

            IReliableDictionary<int, string> votesDictionary 
                    = await this._reliableStateManager.GetOrAddAsync<IReliableDictionary<int, string>>("counts");

            using (ITransaction tx = this._reliableStateManager.CreateTransaction())
            {
                Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<int,string>> list
                    = await votesDictionary.CreateEnumerableAsync(tx);

                Microsoft.ServiceFabric.Data.IAsyncEnumerator<KeyValuePair<int, string>> enumerator
                    = list.GetAsyncEnumerator();

                List<KeyValuePair<int, string>> result = new List<KeyValuePair<int,string>>();

                while (await enumerator.MoveNextAsync(ct))
                {
                    result.Add(enumerator.Current);
                }

                if (result.Any())
                {
                    result.ForEach(x => todoList.Add(new TodoList()
                    {
                        Id = Convert.ToInt32(x.Key),
                        Name= x.Value
                    }));
                }

                return this.Json(todoList);
            }
        }

        [HttpPut("{name}")]
        public async Task<IActionResult> Put(string name)
        {
            try
            {
                IReliableDictionary<int,string> votesDictionary
                      = await this._reliableStateManager.GetOrAddAsync<IReliableDictionary<int,string>>("counts");

                using (ITransaction tx = this._reliableStateManager.CreateTransaction())
                {
                    int totalElements = Convert.ToInt32(await votesDictionary.GetCountAsync(tx));

                    await votesDictionary.AddAsync(tx, totalElements + 1, name);
                    await tx.CommitAsync();
                }
            }
            catch (Exception ex) 
            {
                throw;
            }

            return new OkResult();
        }

        // DELETE api/TodoData/name
        [HttpDelete("{name}")]
        public async Task<IActionResult> Delete(string name)
        {
            IReliableDictionary<string, int> votesDictionary = 
                await this._reliableStateManager.GetOrAddAsync<IReliableDictionary<string, int>>("counts");

            using (ITransaction tx = this._reliableStateManager.CreateTransaction())
            {
                if (await votesDictionary.ContainsKeyAsync(tx, name))
                {
                    await votesDictionary.TryRemoveAsync(tx, name);
                    await tx.CommitAsync();
                    return new OkResult();
                }
                else
                {
                    return new NotFoundResult();
                }
            }
        }
    }

    public class TodoList
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
