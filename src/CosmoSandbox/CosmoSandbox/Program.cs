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

            //string endpoint = ConfigurationManager.AppSettings["Endpoint"];
            //string authKey = ConfigurationManager.AppSettings["AuthKey"];
            string endpoint = "https://leetest.documents.azure.com:443/";
            string authKey = "8r3PMn7j8I2gQv5XQybdYlhxzjL3PbDuPbk66xVQLTC6nXdS2ZFFEduWIbCoSjisl8ka3BU8NeHpSi3wzJDrOA==";

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
                UriFactory.CreateDatabaseUri("foodv1"),
                new DocumentCollection { Id = "foodv1" },
                new RequestOptions { OfferThroughput = 1000 });

            await ExecuteGraphCmd(client, graph, "g.V().drop()");

            try
            {
                var recipes = JsonConvert.DeserializeObject<Recipe[]>(System.IO.File.ReadAllText("recipes.json"));
                foreach (var recipe in recipes)
                {
                    // add recipe vertice if not already there
                    await AddRecipe(client, graph, recipe.name.ToLower());
                    // add mealtype of not present
                    await AddMealType(client, graph, recipe.mealtype.ToLower());
                    // add recipe to mealtype connection
                    await AddMealTypeRecipeConnection(client, graph, recipe.name.ToLower(), recipe.mealtype.ToLower());

                    // loop all ingredients
                    foreach (var ingredient in recipe.ingredients)
                    {
                        // add ingredient vertice if not already there
                        await AddIngredient(client, graph, ingredient.name.ToLower());
                        // Add edge from recipe to ingredient if not already there
                        await AddIngredientRecipeConnection(client, graph, recipe.name.ToLower(), ingredient.name.ToLower());
                    }
                }

                var persons = JsonConvert.DeserializeObject<Person[]>(System.IO.File.ReadAllText("persons.json"));
                foreach (var person in persons)
                {
                    // add person vertice if not already there
                    await AddPerson(client, graph, person.name.ToLower());

                    // loop all ingredients
                    foreach (var ingredient in person.ingredients)
                    {
                        // add ingredient vertice if not already there
                        await AddIngredient(client, graph, ingredient.name.ToLower());
                        // Add edge from person to ingredient if not already there
                        await AddPersonIngredientConnection(client, graph, person.name.ToLower(), ingredient.name.ToLower());
                    }
                }
            }
            catch (Exception e)
            {

            }
        }

        private async Task AddRecipe(DocumentClient client, DocumentCollection graph, string recipe)
        {
            // add recipe vertice if not already there
            var existRecipeCmd = $"g.V().has('name', '{recipe}')";
            if (!(await IsPresent(client, graph, existRecipeCmd)))
            {
                var addRecipeCmd = $"g.addV('recipe').property('name', '{recipe}')";
                await ExecuteGraphCmd(client, graph, addRecipeCmd);
            }
        }

        private async Task AddMealType(DocumentClient client, DocumentCollection graph, string mealType)
        {
            // add mealtype vertice if not already there
            var existMealTypeCmd = $"g.V().has('name', '{mealType}')";
            if (!(await IsPresent(client, graph, existMealTypeCmd)))
            {
                var addMealTypeCmd = $"g.addV('mealtype').property('name', '{mealType}')";
                await ExecuteGraphCmd(client, graph, addMealTypeCmd);
            }
        }

        private async Task AddIngredient(DocumentClient client, DocumentCollection graph, string ingredient)
        {
            // add recipe vertice if not already there
            var existIngredientCmd = $"g.V().has('name', '{ingredient}')";
            if (!(await IsPresent(client, graph, existIngredientCmd)))
            {
                var addIngredientCmd = $"g.addV('ingredient').property('name', '{ingredient}').";
                await ExecuteGraphCmd(client, graph, addIngredientCmd);
            }
        }

        private async Task AddIngredientRecipeConnection(DocumentClient client, DocumentCollection graph, string recipe, string ingredient)
        {
            var addConnectionCmd = $"g.V().has('name', '{recipe}').addE('includes').to(g.V().has('name', '{ingredient}'))";
            await ExecuteGraphCmd(client, graph, addConnectionCmd);
            // Add edge from ingredient to recipe if not already there
            addConnectionCmd = $"g.V().has('name', '{ingredient}').addE('ispartof').to(g.V().has('name', '{recipe}'))";
            await ExecuteGraphCmd(client, graph, addConnectionCmd);
        }

        private async Task AddMealTypeRecipeConnection(DocumentClient client, DocumentCollection graph, string recipe, string mealType)
        {
            var addConnectionCmd = $"g.V().has('name', '{recipe}').addE('isa').to(g.V().has('name', '{mealType}'))";
            await ExecuteGraphCmd(client, graph, addConnectionCmd);
            // Add edge from ingredient to recipe if not already there
            addConnectionCmd = $"g.V().has('name', '{mealType}').addE('couldserve').to(g.V().has('name', '{recipe}'))";
            await ExecuteGraphCmd(client, graph, addConnectionCmd);
        }

        private async Task AddPerson(DocumentClient client, DocumentCollection graph, string person)
        {
            // add recipe vertice if not already there
            var existRecipeCmd = $"g.V().has('name', '{person}')";
            if (!(await IsPresent(client, graph, existRecipeCmd)))
            {
                var addRecipeCmd = $"g.addV('person').property('name', '{person}')";
                await ExecuteGraphCmd(client, graph, addRecipeCmd);
            }
        }

        private async Task AddPersonIngredientConnection(DocumentClient client, DocumentCollection graph, string person, string ingredient)
        {
            var addConnectionCmd = $"g.V().has('name', '{person}').addE('owns').to(g.V().has('name', '{ingredient}'))";
            await ExecuteGraphCmd(client, graph, addConnectionCmd);
            // Add edge from ingredient to recipe if not already there
            addConnectionCmd = $"g.V().has('name', '{ingredient}').addE('instock').to(g.V().has('name', '{person}'))";
            await ExecuteGraphCmd(client, graph, addConnectionCmd);
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
        public string mealtype { get; set; }
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

    public class Person
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
    }
}
