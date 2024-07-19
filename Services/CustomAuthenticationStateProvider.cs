using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.JSInterop;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading;

namespace test.Services
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider, IAsyncDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        private readonly NavigationManager _navigation;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CustomAuthenticationStateProvider> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private string _token;
        private bool _tokenStored;
        private bool _isPrerendering = true;
        private ConcurrentQueue<Func<Task>> _afterRenderActions;
        private readonly IHttpClientFactory _httpClientFactory;
        public CustomAuthenticationStateProvider(
            HttpClient httpClient,
            IJSRuntime jsRuntime,
            NavigationManager navigation,
            IHttpContextAccessor httpContextAccessor,
            ILogger<CustomAuthenticationStateProvider> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
            _navigation = navigation;
            _afterRenderActions = new ConcurrentQueue<Func<Task>>();
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }



        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            _logger.LogInformation("Getting authentication state...");
            _token = await GetTokenAsync();
            var identity = new ClaimsIdentity();
            if (!string.IsNullOrEmpty(_token))
            {
                try
                {
                    identity = new ClaimsIdentity(JwtParser.ParseClaimsFromJwt(_token), "jwt");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing JWT claims.");
                }
            }


            var current_user = new ClaimsPrincipal(identity);
            _logger.LogInformation($"User authenticated: {current_user.Identity.IsAuthenticated}");
            return new AuthenticationState(current_user);
        }


        public async Task Login(string username, string password)
        {
            _logger.LogInformation("Attempting to log in user: {Username}", username);

            try
            {
                var client = _httpClientFactory.CreateClient("noauthClientAPI");

                _logger.LogInformation("Sending login request...");
                var response = await client.PostAsJsonAsync($"{_configuration["ApiBaseUrl"]}/api/User/login", new { username, password });

                _logger.LogInformation("Login response received...");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Login failed with status code: {StatusCode}", response.StatusCode);
                    return;
                }

                var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (string.IsNullOrEmpty(result.Token))
                {
                    _logger.LogWarning("No token received for user: {Username}", username);
                    return;
                }
                _token = result.Token;
                await SecureToken();
                var identity = new ClaimsIdentity();
                if (!string.IsNullOrEmpty(_token))
                {
                    try
                    {
                        identity = new ClaimsIdentity(JwtParser.ParseClaimsFromJwt(_token), "jwt");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing JWT claims.");
                    }
                }

                var current_user = new ClaimsPrincipal(identity);
                _logger.LogInformation($"User authenticated: {current_user.Identity.IsAuthenticated}");


                var authProperties = new AuthenticationProperties
                {
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60),
                    IsPersistent = true
                };

                _logger.LogInformation("Signing in the user...");
                if (_httpContextAccessor.HttpContext.Response.HasStarted)
                {
                    _logger.LogWarning("Response has already started. Deferring sign-in to OnAfterRenderAsync.");
                    _afterRenderActions.Enqueue(async () =>
                    {

                        // await _httpContextAccessor.HttpContext.SignInAsync("Cookies", current_user, authProperties);
                        await _httpContextAccessor.HttpContext.SignInAsync("jwt", current_user, authProperties);
                        _logger.LogInformation("User signed in.");

                        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                    });
                }
                else
                {
                    await _httpContextAccessor.HttpContext.SignInAsync("jwt", current_user, authProperties);
                    // await _httpContextAccessor.HttpContext.SignInAsync("Cookies", current_user, authProperties);
                    _logger.LogInformation("User signed in.");
                    NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                }

                _logger.LogInformation("User logged in successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for user: {Username}", username);
            }
        }

        public async Task Logout()
        {
            _token = null;
            await SecureToken();
            try
            {
                _logger.LogInformation("Logging out user...");
                if (_httpContextAccessor.HttpContext.Response.HasStarted)
                {
                    _logger.LogWarning("Response has already started. Deferring sign-out to OnAfterRenderAsync.");
                    _afterRenderActions.Enqueue(async () =>
                    {
                        await _httpContextAccessor.HttpContext.SignOutAsync("Cookies");
                        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                        _logger.LogInformation("User logged out successfully.");
                    });
                }
                else
                {
                    _token = null;
                    await SecureToken();
                    await _httpContextAccessor.HttpContext.SignOutAsync("Cookies");
                    NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                    _logger.LogInformation("User logged out successfully.");
                }

                _logger.LogInformation("User logged out  in successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loggin out  failed for user");
            }
        }


        public async Task<string> GetTokenAsync()
        {
            if (_isPrerendering) return null;
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
        }

        private async Task SecureToken()
        {
            if (string.IsNullOrEmpty(_token))
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
                _logger.LogInformation("Token removed from localStorage");
            }
            else
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", _token);
                _logger.LogInformation("Token stored in localStorage");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_tokenStored)
            {
                await SecureToken();
            }
        }

        public async Task NotifyPostPrerender()
        {
            _logger.LogInformation("Notifying post prerender");
            _isPrerendering = false;
            while (_afterRenderActions.TryDequeue(out var action))
            {
                _logger.LogInformation("Executing post prerender action");
                await action();
            }
        }

        public async Task AddAfterRenderAction(Func<Task> action)
        {
            _afterRenderActions.Enqueue(action);
            _logger.LogInformation("Action added to post prerender queue");

            if (!_isPrerendering)
            {
                _logger.LogInformation("Prerendering complete, executing action immediately");
                await action();
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
        public async Task<bool> GetRendering()
        {

            return _isPrerendering;
        }
        public async Task<string> GetToken()
        {
            return _token;
        }
        public async Task<string> GetAccessToken()
        {
            var authState = await GetAuthenticationStateAsync();
            var user = authState.User;
            string token = null;
            if (user.Identity.IsAuthenticated)
            {
                token = user.FindFirst("access_token")?.Value;
            }
            return token;
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

            if (keyValuePairs != null)
            {
                foreach (var kvp in keyValuePairs)
                {
                    claims.Add(new Claim(kvp.Key, kvp.Value.ToString()));
                }
            }

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



}
