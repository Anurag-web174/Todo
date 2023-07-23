using System;
using System.Linq;
using System.Fabric;
using System.Net.Http;
using Newtonsoft.Json;
using System.Fabric.Query;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Text;
using System.Net.Http.Headers;

namespace TodoWebApp.Controllers
{
    [Produces("application/json")]
    [Route("api/Todo")]
    public class TodoController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly FabricClient _fabricClient;
        private readonly StatelessServiceContext _serviceContext;
        private readonly string reverseProxyBaseUri;

        public TodoController(
            HttpClient httpClient,
            StatelessServiceContext context,
            FabricClient fabricClient)
        {
            _httpClient = httpClient;
            _serviceContext = context;
            _fabricClient = fabricClient;
        }

        public IActionResult Index()
        {
            return View();
        }

        // GET: api/Todo
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            List<TodoList> todoList = new List<TodoList>();
            Uri serviceName = TodoWebApp.GetToDoDataServiceName(_serviceContext);
            Uri proxyAddress = this.GetProxyAddress(serviceName);

            ServicePartitionList partitions
                = await this._fabricClient.QueryManager.GetPartitionListAsync(serviceName);

            foreach (Partition partition in partitions)
            {
                string proxyUrl =
                    $"{proxyAddress}/api/TodoData?PartitionKey={((Int64RangePartitionInformation)partition.PartitionInformation).LowKey}&PartitionKind=Int64Range";

                using (HttpResponseMessage response = await _httpClient.GetAsync(proxyUrl))
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        continue;
                    }

                    todoList.AddRange(JsonConvert.DeserializeObject<List<TodoList>>(await response.Content.ReadAsStringAsync()));
                }
            }
            return this.Json(todoList);
        }


        [HttpPut("{name}")]
        public async Task<IActionResult> Put(string name)
        {
            Uri serviceName = TodoWebApp.GetToDoDataServiceName(_serviceContext);

            Uri proxyAddress = this.GetProxyAddress(serviceName);

            long partitionKey = this.GetPartitionKey(name); 
            
            string proxyUrl = $"{proxyAddress}/api/TodoData/{name}?PartitionKey={partitionKey}&PartitionKind=Int64Range";

            StringContent putContent = new StringContent($"{{ 'name' : '{name}' }}", Encoding.UTF8, "application/json");
            putContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using (HttpResponseMessage response = await _httpClient.PutAsync(proxyUrl, putContent))
            {
                return new ContentResult()
                {
                    StatusCode = (int)response.StatusCode,
                    Content = await response.Content.ReadAsStringAsync()
                };
            }
        }

        private Uri GetProxyAddress(Uri serviceName)
        {
            return new Uri($"http://localhost:19081{serviceName.AbsolutePath}");
        }

        private long GetPartitionKey(string name)
        {
            return Char.ToUpper(name.First()) - 'A';
        }
    }

    public class TodoList
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
