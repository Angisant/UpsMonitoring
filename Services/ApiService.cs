namespace UPS_Monitor.Services;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Http;
using Blazored.LocalStorage;


public class ApiService
{
    private readonly ILocalStorageService _localStorage;

    private readonly HttpClient _client;

    private readonly string url =  "https://192.168.1.20/zabbix/api_jsonrpc.php";

    public ApiService(IHttpClientFactory httpClientFactory, ILocalStorageService localStorageService)
    {
        _client = httpClientFactory.CreateClient("CustomClient");   // Defined in Program.cs
        _localStorage = localStorageService;
    }

    // Gets the API response using a json request file
    public async Task<string> GetApiResponse(string requestType, string[] args)
    {
        Console.WriteLine($"Getting API response to {requestType} request...");

        // Add an Accept header for JSON format.
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(
           new MediaTypeWithQualityHeaderValue("application/json-rpc"));

        // JSON to string
        var request = await System.IO.File.ReadAllTextAsync("Requests/" + requestType + "Request.json");

        var token = "";
        try {
            token = await _localStorage.GetItemAsync<string>("authToken");  // Get saved token if it exists
        }
        catch(Exception e) {
            Console.WriteLine("No token retrieved: " + e.Message);
        }

        if (requestType.Equals("authentication"))
        {
            var json = JObject.Parse(request);  // String to JSON object

            // Set credentials in request
            json["params"]["username"] = args[0];
            json["params"]["password"] = args[1];

            request = json.ToString();  // JSON to string
            _client.DefaultRequestHeaders.Authorization = null; // Clear authentication header
        }
        else if(!string.IsNullOrEmpty(token))   // Already logged in      
        {
            Console.WriteLine("Token exists");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);   // Set autentication header with proper token
        }
        else
        {
            Console.WriteLine("Token doesnt exist");
            // Handle missing token ( prompt user to log in)
            throw new Exception("Not logged in");
        }

        if(requestType.Equals("itemTrigger"))
        {
            var json = JObject.Parse(request);
            json["params"]["hostids"] = new JArray(args);   // To request item triggers from specific batteries
            request = json.ToString();
        }
        else if(requestType.Equals("hostItem"))
        {
            var json = JObject.Parse(request);
            json["params"]["groupids"] = args[0];   // To request battery items from one specific ups
            request = json.ToString();
        }

        // Prepare the JSON-RPC request body
        var content = new StringContent(request, Encoding.UTF8, "application/json-rpc");

        // Get data response
        Console.WriteLine($"Getting {requestType} response...");
        var response = await _client.PostAsync(url, content);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();  // Return string response
        }
        else
        {
            throw new Exception($"Request failed with status code: {response.StatusCode}");
        }
    }
}
