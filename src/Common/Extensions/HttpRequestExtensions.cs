using Common.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Common.Extensions
{
    public static class HttpRequestExtensions
    {
        public static T GetQueryParameter<T>(this HttpRequest request, string key)
        {
            T? returnVal = default;

            if (request.Query.TryGetValue(key, out StringValues value))
            {
                returnVal = (T)Convert.ChangeType(value.ToString(), typeof(T));
            }

            return returnVal ?? throw new NotSupportedException();

        }

        public static async Task<HttpResponseBody<T>> GetBodyAsync<T>(this HttpRequest request)
        {

            HttpResponseBody<T>? body = null;
            // var bodyString = await new StreamReader(request.Body).ReadToEndAsync();
            var bodyObject = await JsonSerializer.DeserializeAsync<T>(request.Body);

            if (bodyObject != null)
            {
                body = new HttpResponseBody<T>();
                body.Value = bodyObject;
                var results = new List<ValidationResult>();
                body.IsValid = Validator.TryValidateObject(body.Value, new ValidationContext(body.Value, null, null), results, true);
                body.ValidationResults = results;
            }

            return body ?? throw new NotSupportedException();
        }

        public static async Task<JsonElement> GetBody(this HttpRequest request)
        {
            return await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);

        }


    }

}
