using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace test.Services
{
    public class JwtAuthorizationHandler : DelegatingHandler
    {
        public JwtAuthorizationHandler()
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return await base.SendAsync(request, cancellationToken);
        }

    }
    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> AuthSendAsync(this HttpClient client, HttpRequestMessage request, string token, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await client.SendAsync(request, cancellationToken);
        }
    }
}
