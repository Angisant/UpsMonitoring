namespace UPS_Monitor.Services;

using Blazored.LocalStorage;
using Newtonsoft.Json.Linq;

public class AuthService
{
    public event Action AuthenticationStateChanged;
    private readonly ILocalStorageService _localStorage;
    private readonly ApiService ApiService;
    private string Username { get; set; }
    private string Password { get; set; }
    private bool LoginFailed { get; set; } 
    private string Token { get; set; } 
    private string LoginErrorMessage { get; set; }

    public AuthService(ILocalStorageService localStorageService, ApiService apiService)
    {
        _localStorage = localStorageService;
        ApiService = apiService;
    }

    public async Task Init(){
        Token = await _localStorage.GetItemAsync<string>("authToken").ConfigureAwait(false);
    }

    public async Task Login(string username, string password)
    {
        ClearCredentials();

        if(string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) 
        {
            Fail("Fill out all fields");
        }
        else
        {
            try 
            {   
                var apiResponse = await ApiService.GetApiResponse("authentication", new string[] {username, password}).ConfigureAwait(false);
                var json = JObject.Parse(apiResponse);

                if(json.ContainsKey("error"))
                {
                    Fail("Incorrect Username or Password");
                    Console.WriteLine("Error auth: " + json);
                    ChangeAuthenticationState();
                    return;
                }
                
                Token = json["result"].ToString();
                await _localStorage.SetItemAsync("authToken", Token).ConfigureAwait(false);

                Username = username;
                Password = password;
            }
            catch(TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {   
                Fail("Failed to connect with the server");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }   
        }
        ChangeAuthenticationState();
    }

    public async Task Logout()
    {
        ClearCredentials();
        Token = "";
        await _localStorage.RemoveItemAsync("authToken").ConfigureAwait(false);
        ChangeAuthenticationState();
    }

    public bool IsUserLoggedIn()
    {
        return !string.IsNullOrEmpty(Token);
    }

    public bool DidLoginFail()
    {
        return LoginFailed;
    }
    
    public string GetErrorMessage()
    {
        return LoginErrorMessage;
    }

    private async Task ChangeAuthenticationState()
    {
        AuthenticationStateChanged?.Invoke();
    }

    private void ClearCredentials(){
        Username = "";
        Password = "";
        LoginFailed = false;
        LoginErrorMessage = ""; 
    }

    private void Fail(string error) {
        LoginFailed = true;
        LoginErrorMessage = error; 
    }
}