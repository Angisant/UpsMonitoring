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

    // To be called in first render of a page, to establish if the token already exists or not
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
                /**** Send authentication request to Zabbix API ****
                 * Send: Username and Password
                 * Get: Authentication token
                 ***************************************************/
                var apiResponse = await ApiService.GetApiResponse("authentication", new string[] {username, password}).ConfigureAwait(false);
                
                var json = JObject.Parse(apiResponse); // String to JSON object


                if(json.ContainsKey("error"))
                {
                    Fail("Incorrect Username or Password");
                    ChangeAuthenticationState();
                    return;
                }
                
                Token = json["result"].ToString();  // extract token from json response
                await _localStorage.SetItemAsync("authToken", Token).ConfigureAwait(false); // Save token

                // Set new login credentials
                SetCredentials(username, password);
            }
            catch(TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {   
                Fail("Failed to connect with the server");  // Timeout occured
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

        await _localStorage.RemoveItemAsync("authToken").ConfigureAwait(false); // Remove saved token 

        ChangeAuthenticationState(); // Announce login status change
    }

    // Checks for the existence of an authentication token
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

    /****** Alerts that changes in this service have occured *******************
     !  To be called everytime an attribute is altered, at the end the method
     ***************************************************************************/
    private async Task ChangeAuthenticationState()
    {
        AuthenticationStateChanged?.Invoke();
    }

    // Sets login credentials
    private void SetCredentials(string username, string password)
    {
        Username = username;
        Password = password;
    }

    // Resets login status and credentials
    private void ClearCredentials()
    {
        SetCredentials("", "");
        LoginFailed = false;
        LoginErrorMessage = ""; 
        Token = "";
    }

    // Sets failed login status
    private void Fail(string error) 
    {
        LoginFailed = true;
        LoginErrorMessage = error; 
    }
}