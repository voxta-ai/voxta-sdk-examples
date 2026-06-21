using System.Net.Http.Headers;
using System.Net.Http.Json;
using Voxta.Client;
using Voxta.Model.ApiMessages.Responses;
using Voxta.Model.Serialization;

namespace Voxta.ConsoleChat.Api;

public class CharactersApiClient(HttpClient httpClient, IVoxtaTokenProvider tokenProvider)
{
    public async Task<IReadOnlyList<CharactersItemResponse>> GetCharactersAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/characters/?assistant=true");
        var token = tokenProvider.GetToken();
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CharactersResponse>(VoxtaJsonSerializer.SerializeOptions, cancellationToken);
        return result?.Characters ?? [];
    }
}
