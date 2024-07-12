using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components.Routing;


namespace test.Services
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider, IAsyncDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        private readonly NavigationManager _navigation;
        private string _token;
        private bool _tokenStored;
        private ConcurrentQueue<Func<Task>> _afterRenderActions;

        public CustomAuthenticationStateProvider(HttpClient httpClient, IJSRuntime jsRuntime, NavigationManager navigation)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
            _navigation = navigation;
            _afterRenderActions = new ConcurrentQueue<Func<Task>>();
            _navigation.LocationChanged += HandleLocationChanged;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            _token = await GetTokenAsync();
            Console.WriteLine($"Token récupéré : {_token}");

            var identity = string.IsNullOrEmpty(_token) ? new ClaimsIdentity() : new ClaimsIdentity(JwtParser.ParseClaimsFromJwt(_token), "jwt");
            var user = new ClaimsPrincipal(identity);

            Console.WriteLine($"User authenticated: {user.Identity.IsAuthenticated}");

            return await Task.FromResult(new AuthenticationState(user));
        }

        public async Task<AuthenticationState> GetAuthenticationStateAsyncT(string token)
        {
            _token = token;
            Console.WriteLine($"Token récupéré : {_token}");

            var identity = string.IsNullOrEmpty(_token) ? new ClaimsIdentity() : new ClaimsIdentity(JwtParser.ParseClaimsFromJwt(_token), "jwt");
            var user = new ClaimsPrincipal(identity);

            Console.WriteLine($"User authenticated: {user.Identity.IsAuthenticated}");

            return await Task.FromResult(new AuthenticationState(user));
        }

        public async Task Login(string username, string password)
        {
            var response = await _httpClient.PostAsJsonAsync("http://localhost:5208/api/User/login", new { username = username, password = password });
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            _token = result.Token;
            _tokenStored = false;

            Console.WriteLine($"Token reçu après connexion : {_token}");

            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            await SecureToken();

            var storedToken = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
            Console.WriteLine($"Token from localStorage after login: {storedToken}");

            QueueTokenStorage();
        }
        private void HandleLocationChanged(object sender, LocationChangedEventArgs args)
        {
            // Déconnecter l'utilisateur et supprimer le token du localStorage lorsqu'il quitte la page
            if (!args.Location.Contains("/login")) // Vérifiez que l'utilisateur ne navigue pas vers la page de login
            {
                Logout(); // Déconnexion de l'utilisateur
            }
        }

        public async Task Logout()
        {
            _token = null;
            _tokenStored = false;

            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            QueueTokenStorage();
        }

        public async Task<string> GetTokenAsync()
        {
            var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
            Console.WriteLine($"Token from localStorage: {token}");
            return token;
        }

        private void QueueTokenStorage()
        {
            _afterRenderActions.Enqueue(async () =>
            {
                await SecureToken();
                _tokenStored = true;
            });
        }

        private async Task SecureToken()
        {
            if (string.IsNullOrEmpty(_token))
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
                Console.WriteLine("Token removed from localStorage");
            }
            else
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", _token);
                Console.WriteLine("Token stored in localStorage");
            }
        }


        public async ValueTask DisposeAsync()
        {
            if (_tokenStored)
            {
                await SecureToken();
            }
        }

        public async Task OnAfterRenderAsync(bool firstRender)
        {
            if (_afterRenderActions.TryDequeue(out var action))
            {
                await action();
            }
        }
        private async Task<string> GetRefreshTokenFromServer()
        {
            var response = await _httpClient.GetStringAsync("http://localhost:5208/api/User/get-refresh-token");
            return response;
        }
        public async Task<bool> TryRefreshTokenAsync()
        {
            var refreshRequest = new { RefreshToken = await GetRefreshTokenFromServer() };

            var refreshTokenResponse = await _httpClient.PostAsJsonAsync("http://localhost:5208/api/User/refresh-token", refreshRequest);

            if (refreshTokenResponse.IsSuccessStatusCode)
            {
                var result = await refreshTokenResponse.Content.ReadFromJsonAsync<AuthResponse>();
                _token = result.Token;

                await SecureToken();
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                return true;
            }

            return false;
        }
    }

    public class AuthResponse
    {
        public string Token { get; set; }
    }

    public static class JwtParser
    {

        public static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var claims = new List<Claim>();

            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

            // Ajouter les revendications nécessaires
            if (keyValuePairs.ContainsKey("nameid"))
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, keyValuePairs["nameid"].ToString()));
            }

            if (keyValuePairs.ContainsKey("unique_name"))
            {
                claims.Add(new Claim(ClaimTypes.Name, keyValuePairs["unique_name"].ToString()));
            }

            // Ajouter d'autres revendications selon vos besoins
            // Exemple pour ajouter des revendications de rôle
            if (keyValuePairs.ContainsKey("role"))
            {
                var roles = JsonSerializer.Deserialize<string[]>(keyValuePairs["role"].ToString());
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }
            claims.AddRange(keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString())));
            return claims;
        }

        private static byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
    public class JwtAuthorizationHandler : DelegatingHandler
    {
        private readonly CustomAuthenticationStateProvider _authenticationStateProvider;

        public JwtAuthorizationHandler(CustomAuthenticationStateProvider authenticationStateProvider)
        {
            _authenticationStateProvider = authenticationStateProvider;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = await _authenticationStateProvider.GetTokenAsync();
            var jwtHandler = new JwtSecurityTokenHandler();
            var jwtToken = jwtHandler.ReadToken(token) as JwtSecurityToken;
            var tokenExpired = jwtToken.ValidTo < DateTime.UtcNow;

            if (tokenExpired)
            {
                var refreshTokenSuccess = await _authenticationStateProvider.TryRefreshTokenAsync();
                if (refreshTokenSuccess)
                {
                    token = await _authenticationStateProvider.GetTokenAsync();
                }
                else
                {
                    // Token refresh failed, handle accordingly
                    throw new UnauthorizedAccessException("Session expired. Please log in again.");
                }
            }

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            return await base.SendAsync(request, cancellationToken);
        }



    }

}
