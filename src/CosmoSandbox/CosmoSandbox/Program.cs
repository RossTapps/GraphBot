using Gremlin.Net.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.Configuration;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.Graphs;

namespace CosmoSandbox
{
    class Program
    {
        static void Main(string[] args)
        {

            string endpoint = ConfigurationManager.AppSettings["Endpoint"];
            string authKey = ConfigurationManager.AppSettings["AuthKey"];

            using (DocumentClient client = new DocumentClient(
                new Uri(endpoint),
                authKey,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp }))
            {
                Program p = new Program();
                p.RunAsync(client).Wait();
            }
        }


        /// <summary>
        /// Run the get started application.
        /// </summary>
        /// <param name="client">The DocumentDB client instance</param>
        /// <returns>A Task for asynchronous execuion.</returns>
        public async Task RunAsync(DocumentClient client)
        {
            Database database = await client.CreateDatabaseIfNotExistsAsync(new Database { Id = "graphdb" });

            DocumentCollection graph = await client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri("graphdb"),
                new DocumentCollection { Id = "Persons" },
                new RequestOptions { OfferThroughput = 1000 });

            var recipes = JsonConvert.DeserializeObject<Recipe[]>(System.IO.File.ReadAllText("Recipes.json"));

            try
            {
                foreach (var recipe in recipes)
                {
                    // add recipe vertice if not already there
                    var existRecipeCmd = $"g.V().has('id', '{recipe.name}')";
                    if (!(await IsPresent(client, graph, existRecipeCmd)))
                    {
                        var addRecipeCmd = $"g.addV('recipe').property('id', '{recipe.name}').";
                        await ExecuteGraphCmd(client, graph, addRecipeCmd);
                    }

                    // loop all ingredients
                    foreach (var ingredient in recipe.ingredients)
                    {
                        // add ingredient vertice if not already there
                        var existIngredientCmd = $"g.V().has('id', '{ingredient.name}')";
                        if (!(await IsPresent(client, graph, existIngredientCmd)))
                        {
                            var addIncredientCmd = $"g.addV('ingredient').property('id', '{ingredient.name}').";
                            await ExecuteGraphCmd(client, graph, addIncredientCmd);
                        }

                        // Add edge from recipe to ingredient if not already there
                        var addConnectionCmd = $"g.V('{recipe.name}').addE('includes').to(g.V('{ingredient.name}'))";
                        await ExecuteGraphCmd(client, graph, addConnectionCmd);
                        // Add edge from ingredient to recipe if not already there
                        addConnectionCmd = $"g.V('{ingredient.name}').addE('ispartof').to(g.V('{recipe.name}'))";
                        await ExecuteGraphCmd(client, graph, addConnectionCmd);
                    }
                }
            }
            catch (Exception e)
            {

            }
        }

        private async Task<bool> IsPresent(DocumentClient client, DocumentCollection graph, string cmd)
        {
            try
            {
                IDocumentQuery<dynamic> query = client.CreateGremlinQuery<dynamic>(graph, cmd);
                while (query.HasMoreResults)
                {
                    dynamic result = await query.ExecuteNextAsync();
                    var feedResponse = (FeedResponse<object>)result;
                    return (feedResponse.Count > 0);
                }
            }
            catch (Exception e)
            {

            }
            return false;
        }

        private async Task ExecuteGraphCmd(DocumentClient client, DocumentCollection graph, string cmd)
        {
            try
            {
                IDocumentQuery<dynamic> query = client.CreateGremlinQuery<dynamic>(graph, cmd);
                while (query.HasMoreResults)
                {
                    await query.ExecuteNextAsync();
                }
            }
            catch (Exception e)
            {

            }
        }
    }

    public class IngredientsItem
    {
        /// <summary>
        /// 
        /// </summary>
        public string quantity { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string type { get; set; }
    }

    public class Recipe
    {
        /// <summary>
        /// 
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<IngredientsItem> ingredients { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<string> steps { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<int> timers { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string imageURL { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string originalURL { get; set; }
    }
}
