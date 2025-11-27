using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace My.ClasStars;
internal static class HttpHelpers
{
    public static void AddOrReplace(this HttpRequestHeaders headers, string key, string value)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Header key cannot be null or empty", nameof(key));
        }

        if (headers.Contains(key))
        {
            headers.Remove(key);
        }

        headers.Add(key, value ?? string.Empty);
    }

    public static async Task<HttpResponseMessage> InvokeApiAsync(this HttpClient client,
        string endpoint, HttpMethod method, HttpRequestMessage request, IDictionary<string, string> parameters = null,
        HttpContent httpContent = null)
    {
        request.Method = method;
        request.Content = httpContent;


        var requestUri = new StringBuilder(endpoint);
        if (parameters != null && parameters.Any())
        {
            var query = HttpUtility.ParseQueryString(string.Empty);

            foreach (var keyValuePair in parameters)
            {
                query[keyValuePair.Key] = keyValuePair.Value;
            }

            requestUri.Append("?").Append(query);
        }

        request.RequestUri = new Uri(requestUri.ToString(), UriKind.Relative);
        var result = await client.SendAsync(request);
        return result;
    }
}